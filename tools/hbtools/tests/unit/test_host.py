"""One Host per operating system; nothing else looks at the platform."""

from __future__ import annotations

from pathlib import Path

import pytest

from hbtools.host import detect_host, set_env_line

from .conftest import make_host


def test_windows_paths_and_commands(tmp_path: Path) -> None:
    host = make_host(tmp_path, "Windows", "11")
    assert host.kind == "windows" and host.target == "windows"
    assert host.doctor_cmd == ".\\doctor.cmd" and host.play_cmd == ".\\play.cmd"
    assert host.player_log.parts[-4:] == (
        "LocalLow",
        "Company of Theseus",
        "Project Hullbreach",
        "Player.log",
    )
    assert set_env_line(host, "A", "b") == "set A=b"


@pytest.mark.parametrize(
    ("system", "release", "kind", "target"),
    [
        ("Linux", "6.1.0", "linux", "linux"),
        ("Linux", "5.15.0-microsoft-standard-WSL2", "wsl", "linux"),
        ("Darwin", "23.0", "macos", "macos"),
    ],
)
def test_unix_hosts(
    tmp_path: Path, system: str, release: str, kind: str, target: str
) -> None:
    host = make_host(tmp_path, system, release)
    assert (host.kind, host.target) == (kind, target)
    assert host.play_cmd == "./play.sh"
    assert set_env_line(host, "A", "b") == "export A=b"


def test_editor_override_from_environment(tmp_path: Path) -> None:
    host = detect_host(
        env={"HULLBREACH_UNITY_EDITOR": "/opt/Unity/Editor/Unity"},
        system="Linux",
        release="6.1",
        home=tmp_path,
    )
    assert host.editor_override == Path("/opt/Unity/Editor/Unity")
