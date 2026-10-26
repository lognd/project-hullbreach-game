"""doctor --collect: one zip of logs and doctor output, with every secret removed."""

from __future__ import annotations

import logging
import re
import zipfile
from collections.abc import Mapping
from datetime import UTC, datetime
from pathlib import Path

from typani import Result

from hbtools.config import SECRET_ENVS
from hbtools.errors import ProjectError
from hbtools.host import Host
from hbtools.log import log_dir

logger = logging.getLogger(__name__)

REDACTED = "<redacted>"
# Only the end of a big log matters, and it keeps the zip small enough to email.
MAX_BYTES = 2_000_000

_PATTERNS = [
    # Authorization: Bearer <token>
    (re.compile(r"(?i)(bearer\s+)[A-Za-z0-9._~+/=-]+"), r"\1" + REDACTED),
    # X-Server-Key: <key>   /  "x-server-key": "<key>"
    (
        re.compile(r"(?i)(x-server-key[\"']?\s*[:=]\s*[\"']?)[^\s\"',}]+"),
        r"\1" + REDACTED,
    ),
    # "token": "...", "password": "..."
    (
        re.compile(
            r"(?i)([\"'](?:token|password|access_token)[\"']\s*:\s*[\"'])[^\"']*"
        ),
        r"\1" + REDACTED,
    ),
    # HULLBREACH_SOMETHING_KEY=..., ..._TOKEN=..., ..._PASSWORD=...
    (
        re.compile(r"(HULLBREACH_[A-Z_]*(?:KEY|KEYS|TOKEN|PASSWORD)S?\s*[=:]\s*)\S+"),
        r"\1" + REDACTED,
    ),
]


def redact(text: str, env: Mapping[str, str]) -> str:
    """Remove secret values we know (from the environment) and secret-shaped text."""
    for name in SECRET_ENVS:
        value = env.get(name, "")
        if len(value) >= 4:
            text = text.replace(value, REDACTED)
    for pattern, replacement in _PATTERNS:
        text = pattern.sub(replacement, text)
    return text


def _tail(path: Path) -> str:
    data = path.read_bytes()
    if len(data) > MAX_BYTES:
        data = b"...(start cut)...\n" + data[-MAX_BYTES:]
    return data.decode("utf-8", errors="replace")


def gather(root: Path, host: Host) -> list[Path]:
    """Every log worth sending: tool logs, play logs, Player.log(s), Editor.log."""
    found: list[Path] = []
    for folder in (log_dir(root), root / "Logs" / "play"):
        if folder.is_dir():
            found += sorted(p for p in folder.iterdir() if p.is_file())
    if host.player_log.parent.is_dir():
        found += sorted(
            p for p in host.player_log.parent.iterdir() if p.suffix == ".log"
        )
    if host.editor_log.is_file():
        found.append(host.editor_log)
    logger.info("collect: %d log files", len(found))
    return found


def write_bundle(
    root: Path,
    host: Host,
    doctor_lines: list[str],
    env: Mapping[str, str],
    now: datetime | None = None,
) -> Result[Path, ProjectError]:
    """Write Logs/hullbreach-doctor-<time>.zip; every file is redacted first."""
    stamp = (now or datetime.now(UTC)).strftime("%Y%m%d-%H%M%S")
    target = root / "Logs" / f"hullbreach-doctor-{stamp}.zip"

    def write() -> Path:
        target.parent.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as bundle:
            bundle.writestr("doctor.txt", redact("\n".join(doctor_lines) + "\n", env))
            for index, path in enumerate(gather(root, host)):
                try:
                    text = _tail(path)
                except OSError as exc:
                    logger.info("collect: skipped %s (%s)", path, exc)
                    continue
                bundle.writestr(f"logs/{index:02d}-{path.name}", redact(text, env))
        return target

    result = Result.catch(write, OSError, on_error=lambda _e: ProjectError.Unreadable)
    return result.note(str(target))
