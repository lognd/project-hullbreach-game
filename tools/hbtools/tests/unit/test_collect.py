"""doctor --collect never ships a secret."""

from __future__ import annotations

import zipfile
from datetime import UTC, datetime
from pathlib import Path

from hbtools.collect import REDACTED, redact, write_bundle
from hbtools.config import SERVER_KEY_ENV, TOKEN_ENV

from .conftest import make_host

SECRETS = {SERVER_KEY_ENV: "placeholder-server-key", TOKEN_ENV: "placeholder-tok"}


def test_known_values_and_secret_shapes_are_removed() -> None:
    text = "\n".join(
        [
            "key is placeholder-server-key here",
            "Authorization: Bearer abc.def-ghi",
            "X-Server-Key: other-key-1",
            '{"x-server-key": "other-key-2"}',
            '{"token": "jwt-value", "password": "hunter2"}',
            "HULLBREACH_GAME_SERVER_API_KEYS=k1,k2",
            "HULLBREACH_PLATFORM_TOKEN: tok-value",
        ]
    )
    out = redact(text, SECRETS)
    for secret in [
        "placeholder-server-key",
        "abc.def-ghi",
        "other-key-1",
        "other-key-2",
        "jwt-value",
        "hunter2",
        "k1,k2",
        "tok-value",
    ]:
        assert secret not in out
    assert out.count(REDACTED) == 8


def test_harmless_text_survives() -> None:
    line = "Hullbreach platform: config url=http://127.0.0.1:8000 serverKey=set"
    assert redact(line, {}) == line


def test_bundle_holds_report_and_logs_without_secrets(tmp_path: Path) -> None:
    root = tmp_path / "game"
    play_logs = root / "Logs" / "play"
    play_logs.mkdir(parents=True)
    (play_logs / "host.log").write_text("token placeholder-tok in a log\n")
    host = make_host(tmp_path)
    host.player_log.parent.mkdir(parents=True)
    host.player_log.write_text("Bearer secret-bearer\n")
    path = write_bundle(
        root,
        host,
        ["[PASS] something", "key placeholder-server-key"],
        SECRETS,
        now=datetime(2026, 10, 15, 12, 0, tzinfo=UTC),
    ).unwrap()
    assert path.name == "hullbreach-doctor-20261015-120000.zip"
    with zipfile.ZipFile(path) as bundle:
        names = bundle.namelist()
        everything = "".join(bundle.read(n).decode() for n in names)
    assert "doctor.txt" in names
    assert any(n.endswith("host.log") for n in names)
    assert any(n.endswith("Player.log") for n in names)
    for secret in ["placeholder-tok", "placeholder-server-key", "secret-bearer"]:
        assert secret not in everything
