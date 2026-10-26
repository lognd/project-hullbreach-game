"""Fakes for every boundary the tools cross: processes, HTTP, sockets, prompts."""

from __future__ import annotations

import io
import json
from collections.abc import Callable
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

import pytest
from typani import Err, Ok, Result

from hbtools.builds import STAMP_FILE
from hbtools.checks import Ctx, Options
from hbtools.config import load_config
from hbtools.errors import HttpError, ProcError
from hbtools.host import Host, detect_host
from hbtools.net import HttpReply
from hbtools.play import PlayCtx
from hbtools.proc import Completed
from hbtools.report import Reporter

ProcRule = tuple[tuple[str, ...], Completed | ProcError]


@dataclass
class FakeProc:
    """A spawned player that is 'running' until terminated."""

    pid: int = 4242
    code: int | None = None
    terminated: bool = False

    def poll(self) -> int | None:
        """Exit code, or None while running."""
        return self.code

    def terminate(self) -> None:
        """Stop it."""
        self.terminated = True
        self.code = 0

    def wait(self, timeout: float | None = None) -> int:
        """Already stopped."""
        return self.code or 0


@dataclass
class FakeRunner:
    """Scripted process results by argv prefix; spawned players write scripted logs."""

    rules: list[ProcRule] = field(default_factory=list)
    programs: dict[str, str] = field(default_factory=dict)
    log_lines: Callable[[list[str]], list[str]] | None = None
    calls: list[list[str]] = field(default_factory=list)
    spawned: list[tuple[list[str], dict[str, str] | None]] = field(default_factory=list)
    procs: list[FakeProc] = field(default_factory=list)

    def _write_log(self, argv: list[str]) -> None:
        if self.log_lines is not None and "-logFile" in argv:
            log = Path(argv[argv.index("-logFile") + 1])
            log.parent.mkdir(parents=True, exist_ok=True)
            log.write_text("".join(f"{ln}\n" for ln in self.log_lines(argv)))

    def which(self, name: str) -> str | None:
        """Scripted PATH lookup."""
        return self.programs.get(name)

    def run(
        self,
        argv: list[str],
        *,
        cwd: Path | None = None,
        timeout: float = 60.0,
        env: dict[str, str] | None = None,
    ) -> Result[Completed, ProcError]:
        """First rule whose prefix matches argv wins; unknown programs are missing."""
        self.calls.append(list(argv))
        self._write_log(argv)
        for prefix, outcome in self.rules:
            if tuple(argv[: len(prefix)]) == prefix:
                if isinstance(outcome, Completed):
                    return Ok(outcome.model_copy(update={"argv": argv}))
                return Err(outcome)
        return Err(ProcError.NotFound)

    def spawn(
        self,
        argv: list[str],
        *,
        cwd: Path | None = None,
        env: dict[str, str] | None = None,
    ) -> Result[Any, ProcError]:
        """Record the launch and write the scripted log lines to its -logFile."""
        self.spawned.append((list(argv), dict(env) if env else None))
        self._write_log(argv)
        proc = FakeProc()
        self.procs.append(proc)
        return Ok(proc)


def done(stdout: str = "", code: int = 0, stderr: str = "") -> Completed:
    """A finished program's result."""
    return Completed(argv=[], code=code, stdout=stdout, stderr=stderr)


@dataclass
class FakeHttp:
    """Answers by (method, url) or a handler; records every request's headers."""

    routes: dict[tuple[str, str], HttpReply | HttpError] = field(default_factory=dict)
    handler: Callable[[str, str, dict[str, str]], HttpReply | HttpError] | None = None
    calls: list[tuple[str, str, dict[str, str], Any]] = field(default_factory=list)

    def request(
        self,
        method: str,
        url: str,
        *,
        headers: dict[str, str] | None = None,
        payload: dict[str, Any] | None = None,
        timeout: float = 5.0,
    ) -> Result[HttpReply, HttpError]:
        """Scripted answer; Unreachable for anything unscripted."""
        hdrs = dict(headers or {})
        self.calls.append((method, url, hdrs, payload))
        answer: HttpReply | HttpError = self.routes.get(
            (method, url), HttpError.Unreachable
        )
        if self.handler is not None:
            answer = self.handler(method, url, hdrs)
        if isinstance(answer, HttpReply):
            return Ok(answer)
        return Err(answer)


def reply(status: int, body: Any = None) -> HttpReply:
    """A JSON reply."""
    return HttpReply(status=status, body=body, text="" if body is None else str(body))


@dataclass
class FakeSockets:
    """Scripted port and address facts."""

    free: bool = True
    addresses: list[str] = field(default_factory=lambda: ["192.168.1.20"])

    def udp_port_free(self, port: int) -> bool:
        """Scripted."""
        return self.free

    def local_addresses(self) -> list[str]:
        """Scripted."""
        return list(self.addresses)


@dataclass
class FakePrompter:
    """Canned answers; records what was asked."""

    answers: list[str] = field(default_factory=list)
    secrets: list[str] = field(default_factory=list)
    yes: bool = False
    asked: list[str] = field(default_factory=list)

    def ask(self, question: str) -> str:
        """Next canned answer."""
        self.asked.append(question)
        return self.answers.pop(0) if self.answers else ""

    def secret(self, question: str) -> str:
        """Next canned secret."""
        self.asked.append(question)
        return self.secrets.pop(0) if self.secrets else ""

    def confirm(self, question: str) -> bool:
        """Canned yes/no."""
        self.asked.append(question)
        return self.yes


def write_build(
    root: Path,
    target: str = "linux",
    kind: str = "client",
    development: bool = True,
    scenes: list[str] | None = None,
    exe: str = "ProjectHullbreach.x86_64",
) -> Path:
    """A build folder with a stamp and an executable, like HullbreachBuild makes."""
    folder = root / "Builds" / f"{target}-{kind}"
    folder.mkdir(parents=True)
    (folder / exe).write_text("binary")
    stamp = {
        "target": target,
        "kind": kind,
        "commit": "abc1234",
        "development": development,
        "unity": "6000.0.43f1",
        "builtUtc": "2026-10-15T00:00:00Z",
        "executable": exe,
        "backend": "mono",
        "scenes": scenes
        if scenes is not None
        else ["Assets/Scenes/LobbyScene.unity", "Assets/Scenes/MultiplayerGame.unity"],
    }
    (folder / STAMP_FILE).write_text(json.dumps(stamp))
    return folder


def make_host(tmp_path: Path, system: str = "Linux", release: str = "6.1") -> Host:
    """A host rooted in tmp_path so no real home folder is touched."""
    home = tmp_path / "home"
    env = (
        {"LOCALAPPDATA": str(home / "AppData" / "Local")} if system == "Windows" else {}
    )
    return detect_host(env=env, system=system, release=release, home=home)


@pytest.fixture
def clean_env(monkeypatch: pytest.MonkeyPatch) -> dict[str, str]:
    """No real Hullbreach settings or secrets leak in from the developer's shell."""
    for name in [
        "HULLBREACH_PLATFORM_URL",
        "HULLBREACH_PLATFORM_SERVER_KEY",
        "HULLBREACH_PLATFORM_TOKEN",
        "HULLBREACH_PLATFORM_USERNAME",
        "HULLBREACH_PLATFORM_PASSWORD",
        "HULLBREACH_UNITY_EDITOR",
    ]:
        monkeypatch.delenv(name, raising=False)
    return {}


@pytest.fixture
def repo(tmp_path: Path) -> Path:
    """A minimal Unity project folder with the facts the doctor reads."""
    root = tmp_path / "game"
    settings = root / "ProjectSettings"
    settings.mkdir(parents=True)
    (settings / "ProjectVersion.txt").write_text(
        "m_EditorVersion: 6000.0.43f1\nm_EditorVersionWithRevision: x\n"
    )
    (settings / "ProjectSettings.asset").write_text(
        "PlayerSettings:\n  companyName: Company of Theseus\n"
        "  productName: Project Hullbreach\n  cloudProjectId: abc-123\n"
        "  insecureHttpOption: 1\n"
    )
    (settings / "EditorBuildSettings.asset").write_text(
        "EditorBuildSettings:\n  m_Scenes:\n"
        "  - enabled: 1\n    path: Assets/Scenes/LobbyScene.unity\n"
        "  - enabled: 1\n    path: Assets/Scenes/MultiplayerGame.unity\n"
        "  - enabled: 0\n    path: Assets/Scenes/Old.unity\n"
    )
    ui = root / "Assets" / "Prefabs" / "UI"
    ui.mkdir(parents=True)
    (ui / "SignInCanvas.prefab").write_text("prefab")
    (ui / "SignInCanvas.prefab.meta").write_text("guid: 2ce1fde741939ea4\n")
    scenes = root / "Assets" / "Scenes"
    scenes.mkdir(parents=True)
    (scenes / "LobbyScene.unity").write_text("m_SourcePrefab: {guid: 2ce1fde741939ea4}")
    (root / ".gitattributes").write_text("*.cmd text eol=crlf\n")
    return root


@dataclass
class World:
    """One doctor/play world: fakes plus the contexts built on them."""

    root: Path
    host: Host
    runner: FakeRunner
    http: FakeHttp
    sockets: FakeSockets
    prompter: FakePrompter
    env: dict[str, str]
    out: io.StringIO
    reporter: Reporter

    def ctx(self, options: Options | None = None, url: str | None = None) -> Ctx:
        """A doctor context."""
        return Ctx(
            host=self.host,
            config=load_config(self.root, self.env, url),
            runner=self.runner,
            http=self.http,
            sockets=self.sockets,
            env=self.env,
            prompter=self.prompter,
            options=options or Options(),
        )

    def play(self, port: int | None = None) -> PlayCtx:
        """A play context whose clock jumps forward on every sleep."""
        now = [0.0]

        def sleep(seconds: float) -> None:
            now[0] += seconds

        return PlayCtx(
            host=self.host,
            config=load_config(self.root, self.env, None, port),
            runner=self.runner,
            http=self.http,
            env=self.env,
            prompter=self.prompter,
            reporter=self.reporter,
            sleep=sleep,
            clock=lambda: now[0],
        )

    @property
    def text(self) -> str:
        """Everything the reporter printed."""
        return self.out.getvalue()


@pytest.fixture
def world(tmp_path: Path, repo: Path, clean_env: dict[str, str]) -> World:
    """A Linux world with an empty environment."""
    out = io.StringIO()
    return World(
        root=repo,
        host=make_host(tmp_path),
        runner=FakeRunner(),
        http=FakeHttp(),
        sockets=FakeSockets(),
        prompter=FakePrompter(),
        env={},
        out=out,
        reporter=Reporter(out),
    )
