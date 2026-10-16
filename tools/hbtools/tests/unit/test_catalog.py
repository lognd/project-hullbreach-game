"""Individual doctor checks against faked computers, networks and platforms."""

from __future__ import annotations

from pathlib import Path

from hbtools import catalog
from hbtools.builds import STAMP_FILE, read_build
from hbtools.checks import Options, Status
from hbtools.config import SERVER_KEY_ENV
from hbtools.platform_api import Login
from hbtools.project import read_project

from .conftest import FakeHttp, World, done, make_host, reply, write_build

API = "http://127.0.0.1:8000/api/v1"


def test_wsl_fails_with_windows_steps(world: World, tmp_path: Path) -> None:
    world.host = make_host(tmp_path, "Linux", "5.15.0-microsoft-standard-WSL2")
    outcome = catalog.check_os(world.ctx())
    assert outcome.status is Status.FAIL
    assert any("doctor.cmd" in step for step in outcome.fix)


def test_plain_linux_passes(world: World) -> None:
    assert catalog.check_os(world.ctx()).status is Status.PASS


def test_git_missing_and_present(world: World) -> None:
    assert catalog.check_git(world.ctx()).status is Status.FAIL
    world.runner.rules.append((("git", "--version"), done("git version 2.45.0")))
    assert catalog.check_git(world.ctx()).status is Status.PASS


def test_read_project_facts(repo: Path) -> None:
    facts = read_project(repo).unwrap()
    assert facts.unity_version == "6000.0.43f1"
    assert facts.cloud_project_id == "abc-123"
    assert facts.scenes[0] == "Assets/Scenes/LobbyScene.unity"
    assert "Assets/Scenes/Old.unity" not in facts.scenes
    assert facts.signin_prefab_present and facts.signin_in_lobby
    assert not facts.uses_lfs


def test_signin_missing_from_lobby_fails(world: World) -> None:
    (world.root / "Assets/Scenes/LobbyScene.unity").write_text("no prefab here")
    ctx = world.ctx()
    assert catalog.check_project(ctx).status is Status.PASS
    assert catalog.check_signin_prefab(ctx).status is Status.FAIL


def test_wrong_unity_version_fails(world: World) -> None:
    (world.root / "ProjectSettings/ProjectVersion.txt").write_text(
        "m_EditorVersion: 2022.3.1f1\n"
    )
    assert catalog.check_project(world.ctx()).status is Status.FAIL


def test_services_link(world: World) -> None:
    ctx = world.ctx()
    catalog.check_project(ctx)
    assert catalog.check_services_linked(ctx).status is Status.PASS
    settings = world.root / "ProjectSettings/ProjectSettings.asset"
    settings.write_text(settings.read_text().replace("abc-123", ""))
    ctx = world.ctx()
    catalog.check_project(ctx)
    assert catalog.check_services_linked(ctx).status is Status.FAIL


def test_missing_build_tells_how_to_build(world: World) -> None:
    outcome = catalog.check_build_present(world.ctx())
    assert outcome.status is Status.FAIL
    assert any("./play.sh build" in step for step in outcome.fix)


def test_build_scenes_and_freshness(world: World) -> None:
    write_build(world.root)
    ctx = world.ctx()
    assert catalog.check_build_present(ctx).status is Status.PASS
    assert catalog.check_build_scenes(ctx).status is Status.PASS
    world.runner.rules += [
        (("git", "diff"), done("Assets/Scripts/A.cs\n")),
        (("git", "ls-files"), done("")),
    ]
    fresh = catalog.check_build_fresh(ctx)
    assert fresh.status is Status.WARN and "Assets/Scripts/A.cs" in fresh.detail


def test_build_with_lobby_not_first_fails(world: World) -> None:
    write_build(world.root, scenes=["Assets/Scenes/MultiplayerGame.unity"])
    ctx = world.ctx()
    catalog.check_build_present(ctx)
    assert catalog.check_build_scenes(ctx).status is Status.FAIL


def test_read_build_reports_malformed_stamp(tmp_path: Path) -> None:
    folder = tmp_path / "b"
    folder.mkdir()
    (folder / STAMP_FILE).write_text("{not json")
    assert read_build(folder).is_err


def test_busy_port_suggests_another(world: World) -> None:
    world.sockets.free = False
    outcome = catalog.check_port(world.ctx())
    assert outcome.status is Status.WARN
    assert any("--port 7980" in step for step in outcome.fix)


def test_lan_address_is_remembered(world: World) -> None:
    world.sockets.addresses = ["127.0.0.1", "172.20.0.2", "192.168.1.20"]
    ctx = world.ctx()
    outcome = catalog.check_lan(ctx)
    assert outcome.status is Status.PASS
    assert ctx.state["lan"] == "192.168.1.20"
    assert "join 192.168.1.20" in outcome.detail


def test_no_lan_address_warns(world: World) -> None:
    world.sockets.addresses = ["127.0.0.1"]
    assert catalog.check_lan(world.ctx()).status is Status.WARN


def test_windows_firewall_rule_added_with_consent(world: World, tmp_path: Path) -> None:
    world.host = make_host(tmp_path, "Windows", "11")
    added = done("Rule Name: Project Hullbreach UDP 7979\nAction: Allow\n")
    world.runner.rules += [
        (("powershell",), done("")),
        (("netsh",), done("No rules match the specified criteria.")),
    ]
    original = world.runner.run

    def run(argv: list[str], **kw: object):
        # Once the elevated rule-add has run, netsh reports the new rule.
        if argv[0] == "powershell":
            world.runner.rules.insert(0, (("netsh",), added))
        return original(argv, **kw)  # type: ignore[arg-type]

    world.runner.run = run  # type: ignore[method-assign]
    world.prompter.yes = True
    outcome = catalog.check_firewall(world.ctx())
    assert outcome.status is Status.PASS, outcome
    assert any(c[0] == "powershell" for c in world.runner.calls)
    assert world.prompter.asked and "UDP 7979" in world.prompter.asked[0]


def test_windows_firewall_declined_gives_exact_command(
    world: World, tmp_path: Path
) -> None:
    world.host = make_host(tmp_path, "Windows", "11")
    world.runner.rules += [(("netsh",), done("No rules match the specified criteria."))]
    outcome = catalog.check_firewall(world.ctx())
    assert outcome.status is Status.WARN
    text = "\n".join(outcome.fix)
    assert "localport=7979" in text and "--fix-firewall" in text
    assert "Allow access" in text
    assert not any(c[0] == "powershell" for c in world.runner.calls)


def test_windows_firewall_block_rule_is_called_out(
    world: World, tmp_path: Path
) -> None:
    world.host = make_host(tmp_path, "Windows", "11")
    world.runner.rules += [
        (("netsh",), done("Rule Name: Project Hullbreach\nAction: Block\n"))
    ]
    outcome = catalog.check_firewall(world.ctx(Options(fix_firewall=True)))
    assert outcome.status is Status.WARN
    assert "BLOCKS" in outcome.detail
    assert "red circle" in outcome.fix[0]


def test_linux_ufw(world: World) -> None:
    world.runner.rules += [(("ufw", "status"), done("Status: active\n22/tcp ALLOW\n"))]
    outcome = catalog.check_firewall(world.ctx())
    assert outcome.status is Status.WARN
    assert any("sudo ufw allow 7979/udp" in step for step in outcome.fix)


def test_release_build_with_http_platform_fails(world: World) -> None:
    write_build(world.root, development=False)
    ctx = world.ctx()
    catalog.check_project(ctx)
    catalog.check_build_present(ctx)
    assert catalog.check_http_dev(ctx).status is Status.FAIL
    https = world.ctx(url="https://platform.example.com")
    assert catalog.check_http_dev(https).status is Status.PASS


def test_platform_down_and_migrations(world: World) -> None:
    assert catalog.check_platform_health(world.ctx()).status is Status.FAIL
    world.http.routes[("POST", API + "/auth/login")] = reply(401, {"detail": "bad"})
    assert catalog.check_platform_migrations(world.ctx()).status is Status.PASS
    world.http.routes[("POST", API + "/auth/login")] = reply(500, None)
    outcome = catalog.check_platform_migrations(world.ctx())
    assert outcome.status is Status.FAIL
    assert any("db upgrade" in step for step in outcome.fix)


def test_ready_reports_database(world: World) -> None:
    world.http.routes[("GET", API + "/ready")] = reply(503, {"detail": "db down"})
    assert catalog.check_platform_ready(world.ctx()).status is Status.FAIL
    world.http.routes[("GET", API + "/ready")] = reply(200, {"status": "ready"})
    assert catalog.check_platform_ready(world.ctx()).status is Status.PASS


def _session(detail_by_key: dict[str | None, tuple[int, str]]) -> FakeHttp:
    def handle(method, url, headers):
        status, detail = detail_by_key[headers.get("X-Server-Key")]
        return reply(status, {"detail": detail})

    return FakeHttp(handler=handle)


def test_key_mismatch_is_a_clear_failure(world: World) -> None:
    world.env[SERVER_KEY_ENV] = "wrong-placeholder"
    world.http = _session(
        {
            None: (401, "server key missing"),
            "wrong-placeholder": (401, "invalid server key"),
        }
    )
    outcome = catalog.check_key_accepted(world.ctx())
    assert outcome.status is Status.FAIL
    assert outcome.detail.startswith("KEY MISMATCH")
    assert "wrong-placeholder" not in outcome.detail + "".join(outcome.fix)
    assert any("HULLBREACH_GAME_SERVER_API_KEYS" in step for step in outcome.fix)


def test_key_missing_warns_and_old_platform_passes(world: World) -> None:
    world.http = _session({None: (401, "server key missing")})
    assert catalog.check_key_accepted(world.ctx()).status is Status.WARN
    world.http = _session({None: (401, "not authenticated")})
    outcome = catalog.check_key_accepted(world.ctx())
    assert outcome.status is Status.PASS and "does not check" in outcome.detail


def test_key_accepted_with_real_login(world: World) -> None:
    world.env[SERVER_KEY_ENV] = "placeholder-key"

    def handle(method, url, headers):
        if "X-Server-Key" not in headers:
            return reply(401, {"detail": "server key missing"})
        if headers["Authorization"] == "Bearer real-token":
            return reply(200, {"user_id": "u", "role": "player"})
        return reply(401, {"detail": "not authenticated"})

    world.http = FakeHttp(handler=handle)
    ctx = world.ctx()
    ctx.state["login"] = Login(token="real-token", username="ann")
    outcome = catalog.check_key_accepted(ctx)
    assert outcome.status is Status.PASS and "verified your sign-in" in outcome.detail


def test_key_set_hides_the_value(world: World) -> None:
    world.env[SERVER_KEY_ENV] = "placeholder-secret"
    outcome = catalog.check_key_set(world.ctx())
    assert outcome.status is Status.PASS
    assert "placeholder-secret" not in outcome.detail


def test_login_is_opt_in_and_uses_the_prompt(world: World) -> None:
    assert catalog.check_login(world.ctx()).status is Status.SKIP
    world.prompter.answers = ["ann"]
    world.prompter.secrets = ["pw"]
    world.http.routes[("POST", API + "/auth/login")] = reply(
        200, {"token": "tok-123", "user": {"username": "ann"}}
    )
    ctx = world.ctx(Options(login=True))
    outcome = catalog.check_login(ctx)
    assert outcome.status is Status.PASS
    assert "tok-123" not in outcome.detail
    assert ctx.state["login"].username == "ann"


def test_dotnet_optional(world: World) -> None:
    assert catalog.check_dotnet(world.ctx()).status is Status.WARN
    world.runner.rules.append((("dotnet",), done("8.0.100 [/usr/share/dotnet/sdk]\n")))
    outcome = catalog.check_dotnet(world.ctx())
    assert outcome.status is Status.PASS and "8.0.100" in outcome.detail


def test_full_doctor_on_an_empty_machine_blocks_but_lists_everything(
    world: World,
) -> None:
    from hbtools.doctor import run_doctor

    results = run_doctor(world.ctx(), world.reporter, None)
    statuses = {r.id: r.status for r in results}
    assert len(results) == len(catalog.CATALOG)
    assert statuses["unity.hub"] is Status.FAIL
    assert statuses["unity.modules"] is Status.PENDING
    assert statuses["platform.health"] is Status.FAIL
    assert statuses["platform.serverkey.accepted"] is Status.PENDING


def test_loopback_advice_keeps_the_platform_port(world: World) -> None:
    ctx = world.ctx(url="http://127.0.0.1:8002")
    ctx.state["lan"] = "192.168.1.20"
    outcome = catalog.check_loopback(ctx)
    assert "--platform-url http://192.168.1.20:8002" in outcome.detail
