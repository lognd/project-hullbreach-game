"""Finding the builds the play scripts launch, and telling whether they are stale."""

from __future__ import annotations

import json
import logging
from pathlib import Path
from typing import Literal

from pydantic import BaseModel, ValidationError
from typani import Err, Ok, Result

from hbtools.errors import ProjectError
from hbtools.proc import Runner

logger = logging.getLogger(__name__)

STAMP_FILE = "hullbreach-build.json"
# Folders whose changes mean a build no longer matches the code.
SOURCE_PATHS = ["Assets", "Packages", "ProjectSettings"]

BuildKind = Literal["client", "server"]


class BuildStamp(BaseModel):
    """What HullbreachBuild writes next to every build (hullbreach-build.json)."""

    target: str
    kind: str
    commit: str = ""
    development: bool = True
    unity: str = ""
    builtUtc: str = ""
    executable: str
    backend: str = ""
    scenes: list[str] = []


class Build(BaseModel):
    """One build on disk: its folder, stamp and runnable program."""

    folder: Path
    stamp: BuildStamp

    @property
    def executable(self) -> Path:
        """The program to launch (on macOS the .app's inner binary)."""
        exe = self.folder / self.stamp.executable
        if exe.suffix == ".app":
            inner = exe / "Contents" / "MacOS"
            found = sorted(inner.iterdir()) if inner.is_dir() else []
            return found[0] if found else exe
        return exe


def build_folder(builds_dir: Path, target: str, kind: BuildKind) -> Path:
    """Builds/<target>-<kind>, the default output of `play build`."""
    return builds_dir / f"{target}-{kind}"


def read_build(folder: Path) -> Result[Build, ProjectError]:
    """Load a build's stamp; Missing when there is no build in that folder."""
    path = folder / STAMP_FILE
    if not path.is_file():
        return Err(ProjectError.Missing).note(str(path))
    raw = Result.catch(
        lambda: json.loads(path.read_text(encoding="utf-8")),
        OSError,
        ValueError,
        on_error=lambda _e: ProjectError.Unreadable,
    ).note(str(path))
    return raw.and_then(
        lambda data: Result.catch(
            lambda: BuildStamp.model_validate(data),
            ValidationError,
            on_error=lambda _e: ProjectError.Malformed,
        ).note(str(path))
    ).map(lambda stamp: Build(folder=folder, stamp=stamp))


def head_commit(runner: Runner, root: Path) -> str:
    """The short HEAD commit, or "" outside a git clone."""
    return (
        runner.run(["git", "rev-parse", "--short", "HEAD"], cwd=root)
        .map(lambda done: done.stdout.strip() if done.code == 0 else "")
        .unwrap_or("")
    )


def changed_since(runner: Runner, root: Path, commit: str) -> list[str]:
    """Source files that differ from the build's commit (committed or not)."""
    if not commit:
        return ["(the build does not record its commit)"]
    diff = runner.run(
        ["git", "diff", "--name-only", commit, "--", *SOURCE_PATHS], cwd=root
    )
    match diff:
        case Ok(done) if done.code == 0:
            changed = [ln for ln in done.stdout.splitlines() if ln.strip()]
        case _:
            changed = [f"(commit {commit} is not in this clone)"]
    untracked = runner.run(
        ["git", "ls-files", "--others", "--exclude-standard", "--", *SOURCE_PATHS],
        cwd=root,
    ).map(lambda done: [ln for ln in done.stdout.splitlines() if ln.strip()])
    changed += untracked.unwrap_or([])
    logger.info("%d source changes since build commit %s", len(changed), commit)
    return changed
