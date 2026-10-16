"""Running other programs behind a small protocol, so tests use a fake instead."""

from __future__ import annotations

import logging
import os
import shutil
import subprocess
from pathlib import Path
from typing import Protocol

from pydantic import BaseModel
from typani import Err, Ok, Result

from hbtools.errors import ProcError

logger = logging.getLogger(__name__)


class Completed(BaseModel):
    """The outcome of a finished program: exit code and captured text."""

    argv: list[str]
    code: int
    stdout: str = ""
    stderr: str = ""

    @property
    def text(self) -> str:
        """Both output streams together, for grepping."""
        return self.stdout + self.stderr


class Spawned(Protocol):
    """A program started in the background (a game player)."""

    pid: int

    def poll(self) -> int | None:
        """Exit code, or None while still running."""
        ...

    def terminate(self) -> None:
        """Ask the program to stop."""
        ...

    def wait(self, timeout: float | None = None) -> int:
        """Block until it exits and return the code."""
        ...


class Runner(Protocol):
    """Everything the tools need from the operating system's process layer."""

    def which(self, name: str) -> str | None:
        """Full path of a program on PATH, or None."""
        ...

    def run(
        self,
        argv: list[str],
        *,
        cwd: Path | None = None,
        timeout: float = 60.0,
        env: dict[str, str] | None = None,
    ) -> Result[Completed, ProcError]:
        """Run to completion; Ok for any exit code, Err when it cannot run."""
        ...

    def spawn(
        self,
        argv: list[str],
        *,
        cwd: Path | None = None,
        env: dict[str, str] | None = None,
    ) -> Result[Spawned, ProcError]:
        """Start in the background and return a handle."""
        ...


def merged_env(extra: dict[str, str] | None) -> dict[str, str] | None:
    """The current environment plus extra variables (values never logged)."""
    if not extra:
        return None
    env = dict(os.environ)
    env.update(extra)
    return env


class RealRunner:
    """The Runner that really starts processes."""

    def which(self, name: str) -> str | None:
        """Full path of a program on PATH, or None."""
        return shutil.which(name)

    def run(
        self,
        argv: list[str],
        *,
        cwd: Path | None = None,
        timeout: float = 60.0,
        env: dict[str, str] | None = None,
    ) -> Result[Completed, ProcError]:
        """Run to completion; Ok for any exit code, Err when it cannot run."""
        logger.debug("run %s (cwd=%s, extra env=%s)", argv, cwd, sorted(env or {}))
        try:
            done = subprocess.run(
                argv,
                cwd=cwd,
                env=merged_env(env),
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                timeout=timeout,
                check=False,
            )
        except FileNotFoundError:
            logger.info("not found: %s", argv[0])
            return Err(ProcError.NotFound).note(argv[0])
        except subprocess.TimeoutExpired:
            logger.warning("timed out after %ss: %s", timeout, argv)
            return Err(ProcError.TimedOut).note(f"{argv[0]} after {timeout:.0f}s")
        except OSError as exc:
            logger.warning("could not start %s: %s", argv[0], exc)
            return Err(ProcError.StartFailed).note(f"{argv[0]}: {exc}")
        logger.debug("exit %s from %s", done.returncode, argv[0])
        return Ok(
            Completed(
                argv=argv,
                code=done.returncode,
                stdout=done.stdout or "",
                stderr=done.stderr or "",
            )
        )

    def spawn(
        self,
        argv: list[str],
        *,
        cwd: Path | None = None,
        env: dict[str, str] | None = None,
    ) -> Result[Spawned, ProcError]:
        """Start in the background and return a handle."""
        logger.info("spawn %s (extra env=%s)", argv, sorted(env or {}))
        try:
            proc = subprocess.Popen(
                argv,
                cwd=cwd,
                env=merged_env(env),
                stdin=subprocess.DEVNULL,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
            )
        except FileNotFoundError:
            return Err(ProcError.NotFound).note(argv[0])
        except OSError as exc:
            logger.warning("could not start %s: %s", argv[0], exc)
            return Err(ProcError.StartFailed).note(f"{argv[0]}: {exc}")
        return Ok(proc)
