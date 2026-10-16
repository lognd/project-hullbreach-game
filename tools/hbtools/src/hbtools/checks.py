"""The doctor's checklist machinery: results, dependencies between checks, demos."""

from __future__ import annotations

import logging
from collections.abc import Callable, Iterable
from dataclasses import dataclass, field
from enum import Enum
from typing import Any, Literal

from pydantic import BaseModel

from hbtools.config import ToolConfig
from hbtools.host import Host
from hbtools.net import Http, Sockets
from hbtools.proc import Runner
from hbtools.prompt import Prompter

logger = logging.getLogger(__name__)

Demo = Literal["guest", "online", "platform"]
DEMOS: tuple[Demo, ...] = ("guest", "online", "platform")
# Tag for checks that only a full run (no --demo) shows.
DEV = "dev"


class Status(str, Enum):
    """How one check came out."""

    PASS = "pass"
    WARN = "warn"
    FAIL = "fail"
    PENDING = "pending"
    SKIP = "skip"


class Outcome(BaseModel):
    """What a check function decides; the runner adds id, title and doc."""

    status: Status
    detail: str = ""
    fix: list[str] = []


class CheckResult(BaseModel):
    """One line of the checklist, with numbered fix steps and a README link."""

    id: str
    title: str
    status: Status
    detail: str = ""
    fix: list[str] = []
    doc: str = ""


def passed(detail: str = "") -> Outcome:
    """Everything is fine."""
    return Outcome(status=Status.PASS, detail=detail)


def warned(detail: str, fix: list[str] | None = None) -> Outcome:
    """Works, but something will bite later."""
    return Outcome(status=Status.WARN, detail=detail, fix=fix or [])


def failed(detail: str, fix: list[str]) -> Outcome:
    """Blocks the demo; checks that need this one wait."""
    return Outcome(status=Status.FAIL, detail=detail, fix=fix)


def skipped(detail: str) -> Outcome:
    """Not checked this time (opt-in, or not relevant here)."""
    return Outcome(status=Status.SKIP, detail=detail)


class Options(BaseModel):
    """Doctor switches that change what is checked."""

    deep: bool = False
    login: bool = False
    fix_firewall: bool = False


@dataclass
class Ctx:
    """Everything a check may touch, injected so tests can fake all of it."""

    host: Host
    config: ToolConfig
    runner: Runner
    http: Http
    sockets: Sockets
    env: dict[str, str]
    prompter: Prompter
    options: Options = field(default_factory=Options)
    # Facts one check finds and later checks reuse (editor path, build, login).
    state: dict[str, Any] = field(default_factory=dict)


CheckFn = Callable[[Ctx], Outcome]


@dataclass(frozen=True)
class Check:
    """One checklist entry: what it needs first and which demos it serves."""

    id: str
    title: str
    fn: CheckFn
    doc: str
    needs: tuple[str, ...] = ()
    demos: frozenset[str] = frozenset(DEMOS)


def select(checks: Iterable[Check], demo: Demo | None) -> list[Check]:
    """The checks for one demo, or every check for a full run."""
    if demo is None:
        return list(checks)
    return [c for c in checks if demo in c.demos]


def run_checks(
    checks: list[Check],
    ctx: Ctx,
    on_result: Callable[[CheckResult], None] | None = None,
) -> list[CheckResult]:
    """Run in order; a check whose prerequisite failed shows as pending."""
    results: dict[str, CheckResult] = {}
    for check in checks:
        blocker = next(
            (
                need
                for need in check.needs
                if need in results
                and results[need].status in (Status.FAIL, Status.PENDING)
            ),
            None,
        )
        if blocker is not None:
            outcome = Outcome(
                status=Status.PENDING,
                detail=f"waiting for {blocker} to pass first",
            )
        else:
            logger.info("running check %s", check.id)
            outcome = check.fn(ctx)
        result = CheckResult(
            id=check.id,
            title=check.title,
            status=outcome.status,
            detail=outcome.detail,
            fix=outcome.fix,
            doc=check.doc,
        )
        results[check.id] = result
        if on_result is not None:
            on_result(result)
    return list(results.values())


def counts(results: list[CheckResult]) -> dict[Status, int]:
    """How many checks ended in each status."""
    tally = {status: 0 for status in Status}
    for result in results:
        tally[result.status] += 1
    return tally
