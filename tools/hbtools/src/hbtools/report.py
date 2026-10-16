"""The console voice of the tools: plain ASCII lines a beginner can follow."""

from __future__ import annotations

import logging
import sys
from typing import TextIO

from hbtools.checks import CheckResult, Status

logger = logging.getLogger(__name__)

# Fixed-width tags so the checklist lines up in any terminal.
TAGS = {
    Status.PASS: "[PASS]",
    Status.WARN: "[WARN]",
    Status.FAIL: "[FAIL]",
    Status.PENDING: "[ -- ]",
    Status.SKIP: "[SKIP]",
}


class Reporter:
    """Writes user-facing text to the console and keeps a copy for --collect."""

    def __init__(self, stream: TextIO | None = None) -> None:
        """Write to stream (stdout by default)."""
        self._stream = stream or sys.stdout
        self.lines: list[str] = []

    def say(self, text: str = "") -> None:
        """One or more plain lines."""
        for line in text.splitlines() or [""]:
            self.lines.append(line)
            self._stream.write(line + "\n")
        self._stream.flush()

    def heading(self, text: str) -> None:
        """A section title with an underline."""
        self.say()
        self.say(text)
        self.say("=" * len(text))

    def step(self, text: str) -> None:
        """What a play command is doing right now."""
        self.say(f"--> {text}")

    def check(self, result: CheckResult) -> None:
        """One checklist line, plus numbered fix steps when it did not pass."""
        self.say(f"{TAGS[result.status]} {result.title}  ({result.id})")
        if result.detail:
            for line in result.detail.splitlines():
                self.say(f"       {line}")
        if result.fix and result.status in (Status.FAIL, Status.WARN):
            self.say("       How to fix it:")
            for number, fix in enumerate(result.fix, start=1):
                first, *rest = fix.splitlines() or [""]
                self.say(f"         {number}. {first}")
                for line in rest:
                    self.say(f"            {line}")
        if result.doc and result.status in (Status.FAIL, Status.WARN):
            self.say(f"       More help: {result.doc}")
        logger.info("check %s -> %s", result.id, result.status.value)
