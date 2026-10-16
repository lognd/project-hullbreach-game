"""Checklist mechanics: ordering, pending, demo selection, report output."""

from __future__ import annotations

from hbtools.catalog import CATALOG
from hbtools.checks import Check, Status, counts, failed, passed, run_checks, select
from hbtools.doctor import exit_code, next_commands, run_doctor

from .conftest import World


def _check(cid: str, outcome, needs: tuple[str, ...] = (), demos=None) -> Check:
    kwargs = {} if demos is None else {"demos": frozenset(demos)}
    return Check(cid, cid.upper(), lambda _ctx: outcome, "README.md#x", needs, **kwargs)


def test_failed_prerequisite_leaves_dependents_pending(world: World) -> None:
    checks = [
        _check("a", failed("broken", ["fix it"])),
        _check("b", passed(), needs=("a",)),
        _check("c", passed(), needs=("b",)),
        _check("d", passed()),
    ]
    results = run_checks(checks, world.ctx())
    assert [r.status for r in results] == [
        Status.FAIL,
        Status.PENDING,
        Status.PENDING,
        Status.PASS,
    ]
    assert results[1].detail == "waiting for a to pass first"
    assert counts(results)[Status.PENDING] == 2


def test_needs_outside_the_selection_do_not_block(world: World) -> None:
    results = run_checks([_check("b", passed(), needs=("absent",))], world.ctx())
    assert results[0].status is Status.PASS


def test_select_by_demo() -> None:
    checks = [_check("a", passed(), demos={"guest"}), _check("b", passed())]
    assert [c.id for c in select(checks, "online")] == ["b"]
    assert [c.id for c in select(checks, None)] == ["a", "b"]


def test_catalog_ids_are_unique_and_needs_come_first() -> None:
    seen: set[str] = set()
    for check in CATALOG:
        assert check.id not in seen
        for need in check.needs:
            assert need in seen, f"{check.id} needs {need} which runs later"
        seen.add(check.id)
        assert check.doc.startswith("README.md#")


def test_every_demo_has_the_core_checks() -> None:
    for demo in ("guest", "online", "platform"):
        ids = {c.id for c in select(CATALOG, demo)}
        assert {"os.shell", "unity.editor", "build.present"} <= ids
    assert "signin.prefab" in {c.id for c in select(CATALOG, "platform")}
    assert "services.linked" in {c.id for c in select(CATALOG, "online")}
    assert "platform.health" not in {c.id for c in select(CATALOG, "guest")}


def test_report_shows_fix_steps_and_link_only_for_problems(world: World) -> None:
    checks = [
        _check("ok", passed("all good")),
        _check("bad", failed("it broke", ["Click A.", "Type B."])),
        _check("later", passed(), needs=("bad",)),
    ]
    results = run_doctor(world.ctx(), world.reporter, "guest", checks)
    text = world.text
    assert "[PASS] OK  (ok)" in text
    assert "[FAIL] BAD  (bad)" in text
    assert "1. Click A." in text and "2. Type B." in text
    assert text.count("More help: README.md#x") == 1
    assert "[ -- ] LATER  (later)" in text
    assert "Fix the first [FAIL] first: BAD (bad)" in text
    assert "You are ready" not in text
    assert exit_code(results) == 1


def test_all_passing_ends_with_next_commands(world: World) -> None:
    results = run_doctor(world.ctx(), world.reporter, "guest", [_check("a", passed())])
    assert exit_code(results) == 0
    assert "You are ready. Next, type:" in world.text
    assert "./play.sh two-local" in world.text
    assert "--collect" in world.text


def test_next_commands_carry_port_url_and_lan(world: World) -> None:
    ctx = world.ctx(url="http://192.168.1.5:8000")
    ctx.config = ctx.config.model_copy(update={"port": 7990})
    ctx.state["lan"] = "192.168.1.20"
    lines = "\n".join(next_commands(ctx, None))
    assert "join 192.168.1.20 --port 7990" in lines
    assert (
        "server --platform --require-auth --platform-url http://192.168.1.5:8000"
        in lines
    )
    assert "online --headless" in lines
