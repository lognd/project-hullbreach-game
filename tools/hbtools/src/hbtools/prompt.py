"""Asking the person at the keyboard, behind a protocol so tests can answer."""

from __future__ import annotations

import getpass
import logging
import sys
from typing import Protocol

logger = logging.getLogger(__name__)


class Prompter(Protocol):
    """Questions the tools may ask; secrets are read without echo."""

    def ask(self, question: str) -> str:
        """A plain answer ("" when nobody can answer)."""
        ...

    def secret(self, question: str) -> str:
        """A password or key, never echoed, logged or stored."""
        ...

    def confirm(self, question: str) -> bool:
        """A y/N question; anything but y/yes means no."""
        ...


class ConsolePrompter:
    """Asks on the terminal; answers "no" when there is no terminal to ask on."""

    def __init__(self, interactive: bool | None = None) -> None:
        """interactive=None means "only if stdin is a terminal"."""
        self._interactive = sys.stdin.isatty() if interactive is None else interactive

    @property
    def interactive(self) -> bool:
        """True when questions can actually be answered."""
        return self._interactive

    def ask(self, question: str) -> str:
        """A plain answer ("" when nobody can answer)."""
        if not self._interactive:
            logger.info("not interactive; skipped question %r", question)
            return ""
        return input(question).strip()

    def secret(self, question: str) -> str:
        """A password or key, never echoed, logged or stored."""
        if not self._interactive:
            logger.info("not interactive; skipped secret prompt")
            return ""
        return getpass.getpass(question)

    def confirm(self, question: str) -> bool:
        """A y/N question; anything but y/yes means no."""
        answer = self.ask(f"{question} [y/N] ").lower()
        logger.info("confirm %r -> %r", question, answer)
        return answer in ("y", "yes")
