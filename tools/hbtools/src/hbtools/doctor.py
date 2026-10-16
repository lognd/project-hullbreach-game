"""The doctor: walk a beginner through every check, then say exactly what to run."""

from __future__ import annotations

import logging

from hbtools.catalog import CATALOG
from hbtools.checks import (
    Check,
    CheckResult,
    Ctx,
    Demo,
    Status,
    counts,
    run_checks,
    select,
)
from hbtools.config import DEFAULT_PLATFORM_URL
from hbtools.report import Reporter

logger = logging.getLogger(__name__)

DEMO_TITLES: dict[Demo, str] = {
    "online": "ONLINE demo (Relay lobby through Unity's online services)",
    "guest": "GUEST demo (play on your network, no account needed)",
    "platform": "PLATFORM demo (signed in with a Hullbreach account)",
}


def next_commands(ctx: Ctx, demo: Demo | None) -> list[str]:
    """The exact commands to type once everything passes, per demo."""
    play = ctx.host.play_cmd
    lan = ctx.state.get("lan", "<host-ip>")
    url = ctx.config.platform_url
    port = ctx.config.port
    port_arg = "" if port == 7979 else f" --port {port}"
    url_arg = "" if url == DEFAULT_PLATFORM_URL else f" --platform-url {url}"
    blocks: dict[Demo, list[str]] = {
        "guest": [
            "GUEST demo:",
            f"  Quick test on this computer:  {play} two-local{port_arg}",
            f"  Host for friends:             {play} host{port_arg}",
            f"  A friend on the same network: {play} join {lan}{port_arg}",
        ],
        "online": [
            "ONLINE demo:",
            f"  Start the game:  {play} online",
            "  Click HOST WITH RELAY and share the INVITE CODE; a friend runs the "
            "same command, types it under JOIN BY CODE and clicks JOIN CODE; then "
            "click START GAME.",
            f"  A second copy on this computer:   {play} online --profile friend2",
            f"  Headless check of Unity Services: {play} online --headless",
        ],
        "platform": [
            "PLATFORM demo (the platform must be running):",
            f"  Server that requires sign-in: {play} server --platform "
            f"--require-auth{url_arg}{port_arg}",
            f"  A player who signs in:        {play} join 127.0.0.1 --platform"
            f"{url_arg}{port_arg}",
            f"  Or host and play at once:     {play} host --platform"
            f"{url_arg}{port_arg}",
        ],
    }
    demos: list[Demo] = [demo] if demo is not None else ["guest", "online", "platform"]
    lines: list[str] = []
    for one in demos:
        lines += blocks[one]
    return lines


def summarize(reporter: Reporter, results: list[CheckResult]) -> None:
    """One line of totals and the first thing to fix."""
    tally = counts(results)
    reporter.heading("Summary")
    reporter.say(
        f"{tally[Status.PASS]} passed, {tally[Status.WARN]} warnings, "
        f"{tally[Status.FAIL]} failed, {tally[Status.PENDING]} waiting, "
        f"{tally[Status.SKIP]} skipped"
    )
    first = next((r for r in results if r.status is Status.FAIL), None)
    if first is not None:
        reporter.say(
            f"Fix the first [FAIL] first: {first.title} ({first.id}). "
            "Then run the doctor again; the checks marked [ -- ] wait for it."
        )


def run_doctor(
    ctx: Ctx,
    reporter: Reporter,
    demo: Demo | None,
    checks: list[Check] | None = None,
) -> list[CheckResult]:
    """Run the checklist for one demo (or all), print it, and say what to do next."""
    chosen = select(checks if checks is not None else CATALOG, demo)
    title = DEMO_TITLES[demo] if demo is not None else "Full checkup (every demo)"
    logger.info("doctor start: demo=%s, %d checks", demo, len(chosen))
    reporter.heading(f"Project Hullbreach doctor: {title}")
    reporter.say(
        "Each line is one check. [PASS] fine, [WARN] works but read it, "
        "[FAIL] fix this, [ -- ] waits for an earlier fix, [SKIP] not checked."
    )
    reporter.say()
    results = run_checks(chosen, ctx, on_result=reporter.check)
    summarize(reporter, results)
    if not any(r.status is Status.FAIL for r in results):
        reporter.heading("You are ready. Next, type:")
        for line in next_commands(ctx, demo):
            reporter.say(line)
    reporter.say()
    reporter.say(
        f"Stuck? {ctx.host.doctor_cmd} --collect makes a zip (passwords and keys "
        "removed) to send to a teammate."
    )
    logger.info("doctor done: %s", {k.value: v for k, v in counts(results).items()})
    return results


def exit_code(results: list[CheckResult]) -> int:
    """0 when nothing failed, 1 otherwise (warnings do not fail the doctor)."""
    return 1 if any(r.status is Status.FAIL for r in results) else 0
