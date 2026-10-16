"""Finding Unity, its build modules, and running it headless for builds and probes."""

from __future__ import annotations

import logging
import re
import tempfile
from pathlib import Path

from pydantic import BaseModel
from typani import Err, Ok, Result, propagate

from hbtools.errors import ToolError
from hbtools.host import UNITY_CHANGESET, UNITY_VERSION, Host, ModuleSpec
from hbtools.proc import Runner

logger = logging.getLogger(__name__)

BUILD_METHOD = "Hullbreach.Editor.HullbreachBuild.BuildFromCommandLine"
BUILD_PREFIX = "HullbreachBuild:"
HUB_DEEP_LINK = f"unityhub://{UNITY_VERSION}/{UNITY_CHANGESET}"
COMPILE_ERROR = re.compile(r"error CS\d{4}: .*")

# A first import of this project can take a long time on a slow disk.
IMPORT_TIMEOUT = 3600.0
PROBE_TIMEOUT = 900.0


class ModuleStatus(BaseModel):
    """One Hub module and whether this editor has it."""

    spec: ModuleSpec
    installed: bool


class UnityRun(BaseModel):
    """The interesting bits of a finished headless Unity run."""

    code: int
    log_path: Path
    compile_errors: list[str]
    result_lines: list[str]

    @property
    def succeeded(self) -> bool:
        """Exit 0 and no C# compile errors."""
        return self.code == 0 and not self.compile_errors


def find_hub(host: Host) -> Path | None:
    """The Unity Hub program, if installed in a standard place."""
    return next((p for p in host.hub_candidates if p.exists()), None)


def find_editor(host: Host) -> Path | None:
    """The Unity 6000.0.43f1 executable: HULLBREACH_UNITY_EDITOR, then Hub folders."""
    if host.editor_override is not None:
        found = host.editor_override if host.editor_override.exists() else None
        logger.info("editor override %s exists=%s", host.editor_override, bool(found))
        return found
    for root in host.editor_roots:
        candidate = host.editor_path(root)
        if candidate.exists():
            logger.info("found Unity at %s", candidate)
            return candidate
    logger.info("Unity %s not found under %s", UNITY_VERSION, host.editor_roots)
    return None


def other_versions(host: Host) -> list[str]:
    """Other editor versions Hub has, to explain "you have 6000.1, need 6000.0.43f1"."""
    found: list[str] = []
    for root in host.editor_roots:
        if root.is_dir():
            found += sorted(p.name for p in root.iterdir() if p.is_dir())
    return [v for v in found if v != UNITY_VERSION]


def installed_variations(host: Host, editor: Path, engine_dir: str) -> list[str]:
    """Player variation folder names one build module installed."""
    folder = host.playback_engines(editor) / engine_dir / "Variations"
    if not folder.is_dir():
        return []
    return sorted(p.name.lower() for p in folder.iterdir() if p.is_dir())


def module_status(host: Host, editor: Path) -> list[ModuleStatus]:
    """Every relevant Hub module for this computer and whether it is installed."""
    statuses = []
    for spec in host.modules():
        variations = installed_variations(host, editor, spec.engine_dir)
        installed = any(v.startswith(spec.variation_prefix) for v in variations)
        statuses.append(ModuleStatus(spec=spec, installed=installed))
    logger.info("modules: %s", {s.spec.hub_id: s.installed for s in statuses})
    return statuses


def hub_install_command(host: Host, hub: Path | None, hub_id: str) -> str:
    """The exact command that adds one module through Unity Hub's headless mode."""
    program = str(hub) if hub else "Unity Hub"
    return (
        f'"{program}" -- --headless install-modules --version {UNITY_VERSION} '
        f"-m {hub_id}"
    )


def _scan(code: int, log_path: Path) -> UnityRun:
    text = ""
    if log_path.is_file():
        text = log_path.read_text(encoding="utf-8", errors="replace")
    errors = sorted(set(COMPILE_ERROR.findall(text)))
    results = [ln.strip() for ln in text.splitlines() if BUILD_PREFIX in ln]
    logger.info("unity exit %s, %d compile errors, log %s", code, len(errors), log_path)
    return UnityRun(
        code=code, log_path=log_path, compile_errors=errors, result_lines=results
    )


@propagate
def run_headless(
    runner: Runner,
    editor: Path,
    args: list[str],
    log_path: Path,
    timeout: float,
) -> Result[UnityRun, ToolError]:
    """Run the editor with -batchmode -nographics -quit and scan its log."""
    log_path.parent.mkdir(parents=True, exist_ok=True)
    argv = [
        str(editor),
        "-batchmode",
        "-nographics",
        "-quit",
        *args,
        "-logFile",
        str(log_path),
    ]
    done = (
        runner.run(argv, timeout=timeout)
        .map_err(lambda _e: ToolError.BuildFailed)
        .note(f"Unity log: {log_path}")
        .unwrap()
    )
    return Ok(_scan(done.code, log_path))


def build_args(
    root: Path, target: str, kind: str, output: Path, commit: str
) -> list[str]:
    """Arguments for HullbreachBuild.BuildFromCommandLine (all non-secret)."""
    return [
        "-projectPath",
        str(root),
        "-executeMethod",
        BUILD_METHOD,
        "-hullbreachBuildTarget",
        target,
        "-hullbreachBuildKind",
        kind,
        "-hullbreachBuildOutput",
        str(output),
        "-hullbreachBuildCommit",
        commit,
    ]


def compile_args(root: Path) -> list[str]:
    """Open the project once: imports assets and compiles every script."""
    return ["-projectPath", str(root)]


def license_args(scratch: Path) -> list[str]:
    """Create a throwaway empty project: only works with a usable license."""
    return ["-createProject", str(scratch / "hb-license-probe")]


def license_probe(
    runner: Runner, editor: Path, log_dir: Path
) -> Result[UnityRun, ToolError]:
    """Prove the editor starts headless with a license, in a temp folder."""
    with tempfile.TemporaryDirectory(prefix="hb-license-") as scratch:
        return run_headless(
            runner,
            editor,
            license_args(Path(scratch)),
            log_dir / "license-probe.log",
            PROBE_TIMEOUT,
        )


def require_editor(host: Host) -> Result[Path, ToolError]:
    """The editor path, or NoEditor with a note on how to install it."""
    found = find_editor(host)
    if found is None:
        return Err(ToolError.NoEditor).note(f"install it from {HUB_DEEP_LINK}")
    return Ok(found)
