"""Command lines: what a beginner types is what the tools understand."""

from __future__ import annotations

from pathlib import Path

import pytest

from hbtools import __main__ as entry


def test_play_parser_knows_every_command() -> None:
    parser = entry._build_play_parser()
    for argv in [
        ["build"],
        ["build", "server", "--backend", "mono"],
        ["online", "--headless"],
        ["host", "--platform", "--require-auth"],
        ["join", "192.168.1.20", "--platform", "--seconds", "30"],
        ["server", "--platform", "--require-auth", "--skip-key-check"],
        ["two-local", "--headless"],
        ["logs", "--follow"],
        ["--port", "7990", "--platform-url", "http://10.0.0.2:8000", "two-local"],
    ]:
        parser.parse_args(argv)


def test_secrets_are_not_command_line_options() -> None:
    texts = (
        entry._build_play_parser().format_help()
        + entry._build_doctor_parser().format_help()
    ).lower()
    for word in ["--token", "--password", "--key", "--server-key"]:
        assert word not in texts


def test_doctor_parser_demos() -> None:
    args = entry._build_doctor_parser().parse_args(["--demo", "platform", "--login"])
    assert args.demo == "platform" and args.login
    with pytest.raises(SystemExit):
        entry._build_doctor_parser().parse_args(["--demo", "nope"])


def test_require_auth_needs_platform(
    monkeypatch: pytest.MonkeyPatch,
    repo: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    monkeypatch.setattr(entry, "_repo_root", lambda: repo)
    assert entry.play_main(["server", "--require-auth"]) == 2
    assert "--require-auth needs --platform" in capsys.readouterr().out


def test_outside_the_repo_says_where_to_go(
    monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    monkeypatch.setattr(entry, "_repo_root", lambda: None)
    assert entry.doctor_main([]) == 1
    assert "not the Project Hullbreach folder" in capsys.readouterr().out


def test_common_options_work_before_or_after_the_command() -> None:
    parser = entry._build_play_parser()
    after = parser.parse_args(["host", "--port", "7980"])
    before = parser.parse_args(["--port", "7980", "host"])
    plain = parser.parse_args(["host"])
    assert after.port == 7980
    assert before.port == 7980
    assert plain.port is None
    assert parser.parse_args(["join", "1.2.3.4", "--verbose"]).verbose is True
