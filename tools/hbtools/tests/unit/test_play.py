"""play commands against fake players whose logs are scripted."""

from __future__ import annotations

from pathlib import Path

import pytest

from hbtools import play, unity
from hbtools.config import SERVER_KEY_ENV, TOKEN_ENV, URL_ENV, USERNAME_ENV
from hbtools.host import EDITOR_ENV, detect_host

from .conftest import FakeHttp, World, done, reply, write_build

API = "http://127.0.0.1:8000/api/v1"
IN_MATCH = "Hullbreach match: connected as #{n}; replicated ships: 2"


def in_match(argv: list[str]) -> list[str]:
    """Every player reaches the match; the host is #1."""
    n = 1 if play.DIRECT_ARG in argv else 2
    return ["Hullbreach direct: loading MultiplayerGame.", IN_MATCH.format(n=n)]


def test_two_local_starts_host_then_client_and_proves_both(world: World) -> None:
    write_build(world.root)
    world.runner.log_lines = in_match
    code = play.two_local(world.play(), headless=True, seconds=60)
    assert code == 0, world.text
    assert "[PASS] host: in the match" in world.text
    assert "[PASS] client: in the match" in world.text
    (host_argv, _), (client_argv, _) = world.runner.spawned
    assert play.DIRECT_ARG in host_argv and "-batchmode" in host_argv
    assert client_argv[client_argv.index(play.CONNECT_ARG) + 1] == "127.0.0.1"
    host_log = host_argv[host_argv.index("-logFile") + 1]
    client_log = client_argv[client_argv.index("-logFile") + 1]
    assert host_log != client_log
    assert all(p.terminated for p in world.runner.procs)


def test_custom_port_reaches_every_player(world: World) -> None:
    write_build(world.root)
    world.runner.log_lines = in_match
    assert play.two_local(world.play(port=7990), headless=True, seconds=60) == 0
    for argv, _ in world.runner.spawned:
        assert argv[argv.index(play.PORT_ARG) + 1] == "7990"


def test_player_that_never_arrives_fails_with_its_log(world: World) -> None:
    write_build(world.root)
    world.runner.log_lines = lambda argv: ["Hullbreach direct: starting."]
    assert play.join(world.play(), "10.0.0.5", True, 5) == 1
    assert "[FAIL] client: did not reach the match. Log:" in world.text


def test_no_build_says_how_to_build(world: World) -> None:
    assert play.two_local(world.play(), True, 5) == 1
    assert "./play.sh build" in world.text
    assert not world.runner.spawned


def test_platform_token_travels_only_in_the_environment(world: World) -> None:
    write_build(world.root)
    world.env[TOKEN_ENV] = "secret-token-value"
    world.env[USERNAME_ENV] = "ann"
    world.runner.log_lines = in_match
    assert play.join(world.play(), "127.0.0.1", True, 30, platform=True) == 0
    argv, env = world.runner.spawned[0]
    assert "secret-token-value" not in " ".join(argv)
    assert env == {TOKEN_ENV: "secret-token-value", USERNAME_ENV: "ann"}
    assert argv[argv.index(play.PLATFORM_URL_ARG) + 1] == "http://127.0.0.1:8000"
    assert "secret-token-value" not in world.text


def test_platform_join_signs_in_at_the_prompt(world: World) -> None:
    write_build(world.root)
    world.prompter.answers = ["ann"]
    world.prompter.secrets = ["pw-placeholder"]
    world.http.routes[("POST", API + "/auth/login")] = reply(
        200, {"token": "tok-1", "user": {"username": "ann"}}
    )
    world.runner.log_lines = in_match
    assert play.join(world.play(), "127.0.0.1", True, 30, platform=True) == 0
    assert world.runner.spawned[0][1] == {TOKEN_ENV: "tok-1", USERNAME_ENV: "ann"}
    assert "pw-placeholder" not in world.text


def test_bad_password_stops_before_starting(world: World) -> None:
    write_build(world.root)
    world.prompter.answers = ["ann"]
    world.prompter.secrets = ["wrong"]
    world.http.routes[("POST", API + "/auth/login")] = reply(401, {"detail": "no"})
    assert play.join(world.play(), "127.0.0.1", True, 30, platform=True) == 1
    assert "[FAIL] Platform sign-in failed" in world.text
    assert not world.runner.spawned


def test_rejected_client_shows_the_reason(world: World) -> None:
    write_build(world.root)
    world.runner.log_lines = lambda argv: [
        "Hullbreach platform: server rejected this client: sign-in required"
    ]
    assert play.join(world.play(), "127.0.0.1", True, 30) == 1
    assert "[FAIL] client: Hullbreach platform: server rejected this client" in (
        world.text
    )


def test_rejection_after_connecting_is_a_failure(world: World) -> None:
    write_build(world.root)
    world.runner.log_lines = lambda argv: [
        "Hullbreach match: connected as #1; replicated ships: 0",
        "Hullbreach platform: server rejected this client: sign-in required",
    ]
    assert play.join(world.play(), "127.0.0.1", True, 30) == 1
    assert "[PASS]" not in world.text
    assert "--demo platform" in world.text


def test_connected_without_ships_is_not_in_the_match(world: World) -> None:
    write_build(world.root)
    world.runner.log_lines = lambda argv: [
        "Hullbreach match: connected as #1; replicated ships: 0"
    ]
    assert play.join(world.play(), "127.0.0.1", True, 30) == 1
    assert "did not reach the match" in world.text


def _key_platform(world: World) -> None:
    def handle(method, url, headers):
        key = headers.get("X-Server-Key")
        if key is None:
            return reply(401, {"detail": "server key missing"})
        if key != "good-placeholder":
            return reply(401, {"detail": "invalid server key"})
        return reply(401, {"detail": "not authenticated"})

    world.http = FakeHttp(handler=handle)


def test_server_with_wrong_key_explains_and_does_not_start(world: World) -> None:
    write_build(world.root)
    _key_platform(world)
    world.env[SERVER_KEY_ENV] = "bad-placeholder"
    code = play.server(world.play(), 5, platform=True, require_auth=True)
    assert code == 1
    assert "KEY MISMATCH" in world.text
    assert "export HULLBREACH_PLATFORM_SERVER_KEY=" in world.text
    assert "bad-placeholder" not in world.text
    assert not world.runner.spawned


def test_server_skip_key_check_starts_anyway(world: World) -> None:
    write_build(world.root)
    _key_platform(world)
    world.env[SERVER_KEY_ENV] = "bad-placeholder"
    code = play.server(
        world.play(), 5, platform=True, require_auth=True, skip_key_check=True
    )
    assert code == 0
    argv, env = world.runner.spawned[0]
    assert play.SERVER_ARG in argv and play.REQUIRE_AUTH_ARG in argv
    assert "-batchmode" in argv and env is None
    assert "bad-placeholder" not in " ".join(argv)


def test_server_with_good_key_runs_headless_from_the_client_build(
    world: World,
) -> None:
    write_build(world.root)
    _key_platform(world)
    world.env[SERVER_KEY_ENV] = "good-placeholder"
    assert play.server(world.play(), 5, platform=True, require_auth=True) == 0
    assert "running the game build as a server" in world.text
    assert "sign-in REQUIRED" in world.text


def test_online_headless_smoke(world: World) -> None:
    write_build(world.root)
    world.runner.log_lines = lambda argv: [
        "Hullbreach online: smoke lobby created, join code ABC123",
        "Hullbreach online: smoke test PASSED",
    ]
    assert play.online(world.play(), True, 60) == 0
    assert world.runner.spawned[0][1] == {play.ONLINE_SMOKE_ENV: "1"}
    world.runner.log_lines = lambda argv: [
        "Hullbreach online: smoke test FAILED: no internet"
    ]
    assert play.online(world.play(), True, 60) == 1
    assert "doctor.sh --demo online" in world.text


def test_online_profile_is_passed_and_checked(world: World) -> None:
    write_build(world.root)
    world.runner.log_lines = lambda argv: ["Hullbreach online: smoke test PASSED"]
    assert play.online(world.play(), True, 60, "friend2") == 0
    argv = world.runner.spawned[0][0]
    assert argv[argv.index(play.PROFILE_ARG) + 1] == "friend2"
    assert play.PLATFORM_URL_ARG in argv and play.REQUIRE_AUTH_ARG not in argv
    assert play.online(world.play(), True, 60, None, require_auth=True) == 0
    assert play.REQUIRE_AUTH_ARG in world.runner.spawned[-1][0]
    assert play.online(world.play(), True, 60, "bad name") == 2
    assert "--profile friend2" in world.text


def test_logs_shows_the_newest(world: World) -> None:
    ctx = world.play()
    assert play.logs(ctx, follow=False) == 1
    ctx.play_logs.mkdir(parents=True)
    (ctx.play_logs / "host.log").write_text("one\ntwo\nthree\n")
    assert play.logs(ctx, follow=False, lines=2) == 0
    assert "two\nthree" in world.text and "one" not in world.text.split("Newest")[1]


def _editor_world(world: World, tmp_path: Path, lines: list[str]) -> Path:
    editor = tmp_path / "Unity"
    editor.write_text("")
    world.host = detect_host(
        env={EDITOR_ENV: str(editor)},
        system="Linux",
        release="6.1",
        home=tmp_path / "home",
    )
    world.runner.rules += [
        (("git", "rev-parse"), done("abc1234\n")),
        ((str(editor),), done("")),
    ]
    world.runner.log_lines = lambda argv: lines
    return editor


def test_build_runs_unity_headless_with_the_entry_point(
    world: World, tmp_path: Path
) -> None:
    editor = _editor_world(
        world, tmp_path, ["HullbreachBuild: OK linux client -> Builds/linux-client"]
    )
    assert play.build(world.play(), "client") == 0, world.text
    argv = next(c for c in world.runner.calls if c[0] == str(editor))
    assert "-batchmode" in argv and "-quit" in argv
    assert argv[argv.index("-executeMethod") + 1] == unity.BUILD_METHOD
    assert argv[argv.index("-hullbreachBuildKind") + 1] == "client"
    assert argv[argv.index("-hullbreachBuildCommit") + 1] == "abc1234"
    assert "[PASS] Built the linux client" in world.text
    assert "./play.sh two-local" in world.text


def test_build_for_another_target(world: World, tmp_path: Path) -> None:
    editor = _editor_world(
        world, tmp_path, ["HullbreachBuild: OK windows server -> Builds/x"]
    )
    assert play.build(world.play(), "server", target="windows") == 0, world.text
    argv = next(c for c in world.runner.calls if c[0] == str(editor))
    assert argv[argv.index("-hullbreachBuildTarget") + 1] == "windows"
    assert "windows-server" in argv[argv.index("-hullbreachBuildOutput") + 1]
    assert "Copy that whole folder" in world.text


def test_build_reports_compile_errors(world: World, tmp_path: Path) -> None:
    _editor_world(
        world,
        tmp_path,
        ["Assets/Scripts/A.cs(3,1): error CS1002: ; expected"],
    )
    assert play.build(world.play(), "server") == 1
    assert "[FAIL] The code does not compile (1 errors)" in world.text


def test_build_reports_a_missing_module(world: World, tmp_path: Path) -> None:
    _editor_world(
        world,
        tmp_path,
        ["HullbreachBuild: FAILED Linux Dedicated Server module is not installed"],
    )
    assert play.build(world.play(), "server") == 1
    assert "[FAIL] The build did not finish" in world.text


def test_build_hides_unity_stack_frames(world: World, tmp_path: Path) -> None:
    frame = (
        "0x0000015ab5d6c3ec (Mono JIT Code) Hullbreach.Editor.HullbreachBuild:"
        "BuildFromCommandLine () (at Assets/Editor/HullbreachBuild.cs:66)"
    )
    _editor_world(
        world,
        tmp_path,
        ["HullbreachBuild: OK linux client -> Builds/linux-client", frame],
    )
    assert play.build(world.play(), "client") == 0, world.text
    assert "Mono JIT Code" not in world.text


def test_headless_host_stays_up_for_friends(world: World) -> None:
    write_build(world.root)
    world.runner.log_lines = in_match
    ctx = world.play()
    assert play.host(ctx, headless=True, seconds=30) == 0, world.text
    assert ctx.clock() >= 30
    assert "[PASS] host: in the match" in world.text


def test_server_join_hint_names_a_custom_platform_url(world: World) -> None:
    write_build(world.root)
    world.env[URL_ENV] = "http://10.0.0.5:8002"
    world.http = FakeHttp(
        handler=lambda method, url, headers: reply(401, {"detail": "nope"})
    )
    assert play.server(world.play(), 5, platform=True, skip_key_check=True) == 0
    assert "join <this computer's IP> --platform --platform-url " in world.text
    assert "http://10.0.0.5:8002" in world.text


def test_second_copy_with_a_locked_log_explains(
    world: World, monkeypatch: pytest.MonkeyPatch
) -> None:
    write_build(world.root)

    def locked(self: Path, missing_ok: bool = False) -> None:
        raise PermissionError("in use by another process")

    monkeypatch.setattr(Path, "unlink", locked)
    assert play.server(world.play(), 5) == 1
    assert "still running" in world.text
    assert not world.runner.spawned
