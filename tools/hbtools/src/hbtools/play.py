"""The play commands: build the game and start players without touching Unity menus."""

from __future__ import annotations

import logging
import re
import subprocess
import time
from collections.abc import Callable
from dataclasses import dataclass, field
from pathlib import Path

from typani import Err, Ok, Result

from hbtools import unity
from hbtools.builds import Build, BuildKind, build_folder, head_commit, read_build
from hbtools.config import (
    DEFAULT_PLATFORM_URL,
    ONLINE_SMOKE_ENV,
    PASSWORD_ENV,
    SERVER_KEY_ENV,
    TOKEN_ENV,
    USERNAME_ENV,
    ToolConfig,
)
from hbtools.errors import PlatformError, ToolError
from hbtools.host import BuildTarget, Host, set_env_line
from hbtools.net import Http
from hbtools.platform_api import KeyVerdict, PlatformApi
from hbtools.proc import Runner, Spawned
from hbtools.project import editor_has_project_open
from hbtools.prompt import Prompter
from hbtools.report import Reporter

logger = logging.getLogger(__name__)

# Player arguments; they mirror DirectLaunchPlan.cs and PlatformConfig.cs.
DIRECT_ARG = "-hullbreachDirect"
CONNECT_ARG = "-hullbreachConnect"
PORT_ARG = "-hullbreachPort"
SERVER_ARG = "-hullbreachServer"
PLATFORM_URL_ARG = "-hullbreachPlatformUrl"
REQUIRE_AUTH_ARG = "-hullbreachRequirePlatformAuth"
# Unity Services profile (OnlineSmoke.Profile in C# applies the same rule).
PROFILE_ARG = "-hullbreachProfile"
PROFILE_RE = re.compile(r"^[A-Za-z0-9_-]{1,30}$")
HEADLESS_ARGS = ["-batchmode", "-nographics"]
# Two windows side by side fit on a laptop screen.
WINDOWED_ARGS = [
    "-screen-fullscreen",
    "0",
    "-screen-width",
    "960",
    "-screen-height",
    "540",
]

# Player log lines worth echoing to the console while a demo runs.
WATCH_PREFIX = "Hullbreach "
# In the match = the server replicated at least one ship (MatchStatus in C#);
# "replicated ships: 0" only means connected, not yet admitted.
PROOF_RE = re.compile(r"replicated ships: [1-9]")
SMOKE_PASSED = "smoke test PASSED"
SMOKE_FAILED = "smoke test FAILED"
REJECTED_LINE = "server rejected this client"
BUILD_TIMEOUT = 3 * 3600.0
POLL_SECONDS = 0.5


@dataclass
class PlayCtx:
    """Everything a play command touches, injected so tests can fake it."""

    host: Host
    config: ToolConfig
    runner: Runner
    http: Http
    env: dict[str, str]
    prompter: Prompter
    reporter: Reporter
    sleep: Callable[[float], None] = time.sleep
    clock: Callable[[], float] = time.monotonic

    @property
    def play_logs(self) -> Path:
        """Logs/play: one log file per player the scripts start."""
        return self.config.repo_root / "Logs" / "play"


@dataclass
class Player:
    """One started player: a name for the console, its process and its log."""

    name: str
    proc: Spawned
    log: Path
    offset: int = 0
    seen: list[str] = field(default_factory=list)


# --- build --------------------------------------------------------------------


def build(
    ctx: PlayCtx,
    kind: BuildKind,
    backend: str = "auto",
    release: bool = False,
    target: BuildTarget | None = None,
) -> int:
    """`play build`: run HullbreachBuild headless and say where the game ended up."""
    say = ctx.reporter
    root = ctx.config.repo_root
    target = target or ctx.host.target
    match unity.require_editor(ctx.host):
        case Ok(editor):
            pass
        case Err(error):
            say.say(f"[FAIL] {error}.")
            say.say(f"       Run {ctx.host.doctor_cmd}; it shows how to install it.")
            return 1
    if editor_has_project_open(root):
        say.say("[FAIL] Unity already has this project open.")
        say.say("       Close the Unity editor window, then run this again.")
        return 1
    output = build_folder(ctx.config.builds_dir, target, kind)
    log = ctx.play_logs / f"build-{kind}.log"
    args = unity.build_args(root, target, kind, output, head_commit(ctx.runner, root))
    args += ["-hullbreachBuildBackend", backend]
    if release:
        args.append("-hullbreachBuildRelease")
    say.step(f"Building the {target} {kind} with Unity (no window opens).")
    say.say(
        "    The first build imports the whole project and can take 30-60 minutes;\n"
        "    later builds take a few minutes. Leave this window open."
    )
    say.say(f"    Unity's log while it works: {log}")
    logger.info("build %s %s backend=%s release=%s", target, kind, backend, release)
    match unity.run_headless(ctx.runner, editor, args, log, BUILD_TIMEOUT):
        case Ok(run):
            pass
        case Err(error):
            say.say(f"[FAIL] Unity could not run: {error}")
            return 1
    for line in run.result_lines:
        say.say(f"    {line}")
    if run.compile_errors:
        say.say(f"[FAIL] The code does not compile ({len(run.compile_errors)} errors):")
        for line in run.compile_errors[:10]:
            say.say(f"       {line}")
        say.say(
            "       Pull the latest code (git pull) or ask the team; see the log above."
        )
        return 1
    ok = any(f"{unity.BUILD_PREFIX} OK" in line for line in run.result_lines)
    if run.code != 0 or not ok:
        say.say(f"[FAIL] The build did not finish (Unity exit code {run.code}).")
        _explain_build_failure(ctx, kind, run.result_lines)
        return 1
    say.say(f"[PASS] Built the {target} {kind}: {output}")
    if target != ctx.host.target:
        say.say(
            "    Copy that whole folder to a computer running "
            f"{target}; see the README, Guide 5."
        )
        return 0
    say.say(
        f"    Next: {ctx.host.play_cmd} {'server' if kind == 'server' else 'two-local'}"
    )
    return 0


def _explain_build_failure(ctx: PlayCtx, kind: BuildKind, lines: list[str]) -> None:
    """Turn the build's FAILED line into the next thing to do."""
    say = ctx.reporter
    text = " ".join(lines).lower()
    if kind == "server" and ("module" in text or "server" in text):
        say.say(
            "       This computer's Unity has no Dedicated Server module.\n"
            "       Either add it in Unity Hub (Installs > 6000.0.43f1 > gear >\n"
            "       Add modules > 'Dedicated Server Build Support' for this OS),\n"
            f"       or skip the server build: {ctx.host.play_cmd} server runs\n"
            "       the normal game build as a server without a window."
        )
        return
    say.say(f"       Read the end of the log, or run {ctx.host.doctor_cmd} --deep.")


# --- finding a build and starting players -------------------------------------


def find_build(ctx: PlayCtx, kind: BuildKind) -> Result[Build, ToolError]:
    """The newest build of one kind for this computer, or NoBuild."""
    folder = build_folder(ctx.config.builds_dir, ctx.host.target, kind)
    found = read_build(folder).map_err(lambda _e: ToolError.NoBuild).note(str(folder))
    match found:
        case Ok(one) if one.executable.exists():
            return Ok(one)
        case Ok(one):
            return Err(ToolError.NoBuild).note(str(one.executable))
        case Err(_):
            return found
    raise AssertionError("unreachable")


def server_build(ctx: PlayCtx) -> Result[Build, ToolError]:
    """A dedicated server build if one exists, else the client build run as a server."""
    match find_build(ctx, "server"):
        case Ok(one):
            return Ok(one)
        case Err(_):
            return find_build(ctx, "client")
    raise AssertionError("unreachable")


def _no_build(ctx: PlayCtx, error: ToolError) -> int:
    ctx.reporter.say(f"[FAIL] {error}.")
    ctx.reporter.say(
        f"       Build the game first:  {ctx.host.play_cmd} build\n"
        f"       (or run {ctx.host.doctor_cmd} to see what is missing)."
    )
    return 1


def player_argv(
    build_: Build, role_args: list[str], log: Path, headless: bool
) -> list[str]:
    """The full command line for one player (never contains a secret)."""
    look = HEADLESS_ARGS if headless else WINDOWED_ARGS
    return [str(build_.executable), *look, *role_args, "-logFile", str(log)]


def start_player(
    ctx: PlayCtx,
    name: str,
    build_: Build,
    role_args: list[str],
    headless: bool,
    extra_env: dict[str, str] | None = None,
) -> Result[Player, ToolError]:
    """Start one player with its own log file; secrets travel only in extra_env."""
    ctx.play_logs.mkdir(parents=True, exist_ok=True)
    log = ctx.play_logs / f"{name}.log"
    try:
        log.unlink(missing_ok=True)
    except OSError as error:
        # Windows locks the log of a player that is still running.
        logger.warning("cannot replace %s: %s", log, error)
        return Err(ToolError.AlreadyRunning).note(
            f"{log} is in use: close the other {name} (or wait for it to stop), "
            "then try again"
        )
    argv = player_argv(build_, role_args, log, headless)
    logger.info("starting %s: %s (env names %s)", name, argv, sorted(extra_env or {}))
    started = ctx.runner.spawn(argv, cwd=build_.folder, env=extra_env)
    return (
        started.map(lambda proc: Player(name=name, proc=proc, log=log))
        .map_err(lambda _e: ToolError.Unsupported)
        .note(f"could not start {build_.executable}")
    )


def _new_lines(player: Player) -> list[str]:
    """Lines appended to a player's log since the last look."""
    if not player.log.is_file():
        return []
    with player.log.open("rb") as handle:
        handle.seek(player.offset)
        data = handle.read()
    # Keep a partial last line for next time.
    cut = data.rfind(b"\n") + 1
    player.offset += cut
    text = data[:cut].decode("utf-8", errors="replace")
    return [ln.rstrip("\r") for ln in text.splitlines()]


def watch(
    ctx: PlayCtx,
    players: list[Player],
    seconds: float | None,
    done: Callable[[list[Player]], bool] | None = None,
    prefix: str = WATCH_PREFIX,
) -> None:
    """Echo log lines containing prefix until done, all exit, time or Ctrl+C."""
    say = ctx.reporter
    deadline = None if seconds is None else ctx.clock() + seconds
    say.say(
        "    (Press Ctrl+C to stop.)"
        if seconds is None
        else f"    (Watching for up to {seconds:.0f} s.)"
    )
    try:
        while True:
            for player in players:
                for line in _new_lines(player):
                    if prefix in line:
                        player.seen.append(line)
                        say.say(f"  [{player.name}] {line.strip()}")
            if done is not None and done(players):
                logger.info("watch: done condition met")
                return
            if all(p.proc.poll() is not None for p in players):
                logger.info("watch: every player exited")
                return
            if deadline is not None and ctx.clock() >= deadline:
                logger.info("watch: time is up")
                return
            ctx.sleep(POLL_SECONDS)
    except KeyboardInterrupt:
        say.say("    Stopping (Ctrl+C).")


def stop(players: list[Player]) -> None:
    """Close every player the command started."""
    for player in players:
        if player.proc.poll() is None:
            logger.info("stopping %s (pid %s)", player.name, player.proc.pid)
            player.proc.terminate()
            try:
                player.proc.wait(timeout=10)
            except subprocess.TimeoutExpired:
                logger.warning("player %s did not stop in time", player.name)


def has(player: Player, text: str) -> bool:
    """True once the player logged a line containing text."""
    return any(text in line for line in player.seen)


def in_match(player: Player) -> str | None:
    """The first log line proving the player sees a ship in the match, if any."""
    return next((ln for ln in player.seen if PROOF_RE.search(ln)), None)


def summary(ctx: PlayCtx, players: list[Player]) -> int:
    """Say plainly whether each player reached the match; 0 only if all did."""
    say = ctx.reporter
    say.heading("Result")
    ok = True
    demo = "guest"
    for player in players:
        proof = in_match(player)
        rejected = next((ln for ln in player.seen if REJECTED_LINE in ln), None)
        # A rejection wins: the client connects (ships: 0) before the server
        # turns it away.
        if rejected is not None:
            ok = False
            # Only a sign-in check turns a client away: the platform demo.
            demo = "platform"
            say.say(f"[FAIL] {player.name}: {rejected.strip()}")
        elif proof is not None:
            say.say(f"[PASS] {player.name}: in the match ({proof.strip()})")
        else:
            ok = False
            say.say(f"[FAIL] {player.name}: did not reach the match. Log: {player.log}")
    if not ok:
        say.say(f"    Run {ctx.host.doctor_cmd} --demo {demo}, or read the logs above.")
    return 0 if ok else 1


# --- platform sign-in for scripted players ------------------------------------


def platform_identity(ctx: PlayCtx) -> Result[dict[str, str], PlatformError]:
    """A token for the player: from the environment, or a sign-in typed at a prompt."""
    token = ctx.env.get(TOKEN_ENV)
    if token:
        name = ctx.env.get(USERNAME_ENV, "")
        ctx.reporter.step(f"Using the platform token from {TOKEN_ENV} (value hidden).")
        return Ok({TOKEN_ENV: token, USERNAME_ENV: name})
    username = ctx.env.get(USERNAME_ENV) or ctx.prompter.ask("Platform username: ")
    password = ctx.env.get(PASSWORD_ENV) or ctx.prompter.secret(
        "Platform password (hidden, not stored): "
    )
    if not username or not password:
        return Err(PlatformError.BadCredentials).note(
            f"type them at the prompt, or set {USERNAME_ENV} and {PASSWORD_ENV}"
        )
    api = PlatformApi(ctx.http, ctx.config.api)
    ctx.reporter.step(f"Signing in to {ctx.config.platform_url} as {username}.")
    return api.login(username, password).map(
        lambda login: {TOKEN_ENV: login.token, USERNAME_ENV: login.username}
    )


def _platform_args(ctx: PlayCtx, require_auth: bool) -> list[str]:
    args = [PLATFORM_URL_ARG, ctx.config.platform_url]
    if require_auth:
        args.append(REQUIRE_AUTH_ARG)
    return args


def server_key_ok(ctx: PlayCtx) -> bool:
    """Ask the platform about this server's key before starting; explain a mismatch."""
    say = ctx.reporter
    key = ctx.env.get(SERVER_KEY_ENV)
    api = PlatformApi(ctx.http, ctx.config.api)
    match api.key_verdict(key):
        case Ok(verdict):
            pass
        case Err(error):
            say.say(
                f"[FAIL] The platform at {ctx.config.platform_url} "
                f"did not answer ({error})."
            )
            say.say(
                "       Start the platform first (README 4.2), or pass the right\n"
                "       address with --platform-url."
            )
            return False
    fix = (
        "       Ask the platform owner for one of the keys in\n"
        "       HULLBREACH_GAME_SERVER_API_KEYS and set it in this window\n"
        "       before starting the server:\n"
        f"       {set_env_line(ctx.host, SERVER_KEY_ENV, '<key>')}"
    )
    if verdict in (KeyVerdict.ACCEPTED, KeyVerdict.NOT_ENFORCED):
        say.step(
            "Platform accepts this server's key."
            if verdict is KeyVerdict.ACCEPTED
            else "Platform does not check server keys."
        )
        return True
    if verdict is KeyVerdict.MISSING:
        say.say(
            "[FAIL] The platform requires a game server key and "
            f"{SERVER_KEY_ENV} is not set."
        )
    elif verdict is KeyVerdict.INVALID:
        say.say(
            f"[FAIL] KEY MISMATCH: the platform refused the key in {SERVER_KEY_ENV}."
        )
    elif verdict is KeyVerdict.NOT_CONFIGURED:
        say.say("[FAIL] The platform has no game server keys configured.")
        say.say(
            "       On the platform computer set HULLBREACH_GAME_SERVER_API_KEYS\n"
            "       and restart it."
        )
        return False
    else:
        say.say(
            "[WARN] The platform gave an unexpected answer about server keys;"
            " starting anyway."
        )
        return True
    say.say(fix)
    return False


# --- the commands -------------------------------------------------------------


def _role_args(port: int, *role: str) -> list[str]:
    return [*role, PORT_ARG, str(port)]


def run_players(
    ctx: PlayCtx,
    specs: list[tuple[str, Build, list[str], dict[str, str] | None]],
    headless: bool,
    seconds: float | None,
    expect_match: list[str],
    gap: float = 6.0,
    stop_when_in: bool = True,
) -> int:
    """Start players in order, watch their logs, then report who reached the match."""
    players: list[Player] = []
    try:
        for index, (name, build_, args, extra) in enumerate(specs):
            if index:
                # The client needs the host listening before it connects.
                ctx.sleep(gap)
            match start_player(ctx, name, build_, args, headless, extra):
                case Ok(player):
                    players.append(player)
                    ctx.reporter.step(f"Started {name} (log: {player.log}).")
                case Err(error):
                    ctx.reporter.say(f"[FAIL] {error}")
                    return 1
        wanted = [p for p in players if p.name in expect_match]

        def all_in(_: list[Player]) -> bool:
            return (
                stop_when_in
                and headless
                and bool(wanted)
                and all(in_match(p) or has(p, REJECTED_LINE) for p in wanted)
            )

        watch(ctx, players, seconds, done=all_in)
    finally:
        stop(players)
    return summary(ctx, [p for p in players if p.name in expect_match])


def host(
    ctx: PlayCtx,
    headless: bool,
    seconds: float | None,
    platform: bool = False,
    require_auth: bool = False,
) -> int:
    """`play host`: this computer runs the match and plays in it."""
    match find_build(ctx, "client"):
        case Ok(game):
            pass
        case Err(error):
            return _no_build(ctx, error)
    args = _role_args(ctx.config.port, DIRECT_ARG)
    extra: dict[str, str] | None = None
    if platform:
        if require_auth and not server_key_ok(ctx):
            return 1
        match platform_identity(ctx):
            case Ok(extra):
                pass
            case Err(error):
                ctx.reporter.say(f"[FAIL] Platform sign-in failed: {error}")
                return 1
        args += _platform_args(ctx, require_auth)
    ctx.reporter.step(f"Hosting on UDP port {ctx.config.port}.")
    ctx.reporter.say(
        f"    Friends type:  {ctx.host.play_cmd} join <this computer's IP>"
        f"{_port_suffix(ctx)}\n"
        f"    ({ctx.host.doctor_cmd} --demo guest shows this computer's IP.)"
    )
    # A host is in its own match at once; keep it up so friends can still join.
    return run_players(
        ctx,
        [("host", game, args, extra)],
        headless,
        seconds,
        ["host"],
        stop_when_in=False,
    )


def join(
    ctx: PlayCtx,
    address: str,
    headless: bool,
    seconds: float | None,
    platform: bool = False,
) -> int:
    """`play join <ip>`: connect to someone else's host or server."""
    match find_build(ctx, "client"):
        case Ok(game):
            pass
        case Err(error):
            return _no_build(ctx, error)
    args = _role_args(ctx.config.port, CONNECT_ARG, address)
    extra: dict[str, str] | None = None
    if platform:
        match platform_identity(ctx):
            case Ok(extra):
                pass
            case Err(error):
                ctx.reporter.say(f"[FAIL] Platform sign-in failed: {error}")
                return 1
        args += _platform_args(ctx, False)
    ctx.reporter.step(f"Joining {address} on UDP port {ctx.config.port}.")
    ctx.reporter.say("    You should see the match in a few seconds.")
    return run_players(
        ctx, [("client", game, args, extra)], headless, seconds, ["client"]
    )


def server(
    ctx: PlayCtx,
    seconds: float | None,
    platform: bool = False,
    require_auth: bool = False,
    skip_key_check: bool = False,
) -> int:
    """`play server`: a match host without a window or a player of its own."""
    match server_build(ctx):
        case Ok(game):
            pass
        case Err(error):
            return _no_build(ctx, error)
    if game.stamp.kind != "server":
        ctx.reporter.step(
            "No dedicated server build here; running the game build as a server "
            "(no window)."
        )
    args = _role_args(ctx.config.port, SERVER_ARG)
    if platform:
        if not skip_key_check and not server_key_ok(ctx):
            ctx.reporter.say(
                "       (Add --skip-key-check to start anyway and see the server's "
                "own message.)"
            )
            return 1
        args += _platform_args(ctx, require_auth)
    ctx.reporter.step(
        f"Server on UDP port {ctx.config.port}"
        + (", sign-in REQUIRED" if require_auth else "")
        + "."
    )
    ctx.reporter.say(
        f"    Players type:  {ctx.host.play_cmd} join <this computer's IP>"
        + (" --platform" + _url_suffix(ctx) if platform else "")
        + _port_suffix(ctx)
    )
    players: list[Player] = []
    try:
        match start_player(ctx, "server", game, args, True):
            case Ok(player):
                players.append(player)
            case Err(error):
                ctx.reporter.say(f"[FAIL] {error}")
                return 1
        watch(ctx, players, seconds)
    finally:
        stop(players)
    return 0


def two_local(ctx: PlayCtx, headless: bool, seconds: float | None) -> int:
    """`play two-local`: a host and a client on this computer, for a solo demo."""
    match find_build(ctx, "client"):
        case Ok(game):
            pass
        case Err(error):
            return _no_build(ctx, error)
    port = ctx.config.port
    ctx.reporter.step(
        f"Starting a host on port {port} and a client that joins 127.0.0.1:{port}."
    )
    ctx.reporter.say(
        "    Only the host listens on the port, so both fit on one computer; each\n"
        "    writes its own log file. Two windows open (unless --headless)."
    )
    specs: list[tuple[str, Build, list[str], dict[str, str] | None]] = [
        ("host", game, _role_args(port, DIRECT_ARG), None),
        ("client", game, _role_args(port, CONNECT_ARG, "127.0.0.1"), None),
    ]
    return run_players(ctx, specs, headless, seconds, ["host", "client"])


def profile_args(profile: str | None) -> list[str]:
    """Unity Services profile arguments; a second copy on one computer needs one."""
    if profile is None:
        return []
    return [PROFILE_ARG, profile]


def online(
    ctx: PlayCtx,
    headless: bool,
    seconds: float | None,
    profile: str | None = None,
    require_auth: bool = False,
) -> int:
    """`play online`: the lobby; --headless runs the Unity Services smoke test."""
    if profile is not None and not PROFILE_RE.match(profile):
        ctx.reporter.say(
            "[FAIL] A profile name may only use letters, digits, - and _ "
            "(at most 30), for example: --profile friend2"
        )
        return 2
    # The sign-in screen talks to this platform (the default unless --platform-url).
    extra_args = profile_args(profile) + _platform_args(ctx, require_auth)
    match find_build(ctx, "client"):
        case Ok(game):
            pass
        case Err(error):
            return _no_build(ctx, error)
    if not headless:
        ctx.reporter.step("Starting the game at the lobby.")
        ctx.reporter.say(
            "    Wait for 'Online' at the top, click HOST WITH RELAY, and read\n"
            "    the INVITE CODE to your friend. They type it under JOIN BY CODE\n"
            "    and click JOIN CODE. When both names are under PLAYERS, click\n"
            "    START GAME."
        )
        if profile is None:
            ctx.reporter.say(
                "    A second copy on this computer needs its own Unity player:\n"
                "    start it with --profile friend2."
            )
        return run_players(
            ctx, [("online", game, extra_args, None)], False, seconds, []
        )
    ctx.reporter.step(
        "Headless check: sign in to Unity Services, create a private Relay lobby, "
        "leave it."
    )
    players: list[Player] = []
    try:
        match start_player(
            ctx, "online", game, extra_args, True, {ONLINE_SMOKE_ENV: "1"}
        ):
            case Ok(player):
                players.append(player)
            case Err(error):
                ctx.reporter.say(f"[FAIL] {error}")
                return 1

        def finished(found: list[Player]) -> bool:
            return has(found[0], SMOKE_PASSED) or has(found[0], SMOKE_FAILED)

        watch(ctx, players, seconds if seconds is not None else 120.0, done=finished)
    finally:
        stop(players)
    if players and has(players[0], SMOKE_PASSED):
        ctx.reporter.say(
            "[PASS] Unity Services sign-in, lobby and Relay work from here."
        )
        return 0
    ctx.reporter.say(
        "[FAIL] The online check did not pass. Read the 'Hullbreach online:' lines\n"
        f"       above and run {ctx.host.doctor_cmd} --demo online."
    )
    return 1


def logs(ctx: PlayCtx, follow: bool, lines: int = 40) -> int:
    """`play logs`: show the end of the newest player log (and keep following)."""
    candidates = (
        [p for p in ctx.play_logs.glob("*.log")] if ctx.play_logs.is_dir() else []
    )
    if ctx.host.player_log.is_file():
        candidates.append(ctx.host.player_log)
    if not candidates:
        ctx.reporter.say("No player logs yet. Start the game once, then try again.")
        ctx.reporter.say(f"    The game's own log will be at {ctx.host.player_log}")
        return 1
    newest = max(candidates, key=lambda p: p.stat().st_mtime)
    ctx.reporter.step(f"Newest log: {newest}")
    text = newest.read_text(encoding="utf-8", errors="replace").splitlines()
    for line in text[-lines:]:
        ctx.reporter.say(line)
    if follow:
        player = Player(name=newest.stem, proc=_Forever(), log=newest)
        player.offset = newest.stat().st_size
        watch(ctx, [player], None, prefix="")
    return 0


class _Forever:
    """A stand-in process for `logs --follow`: it never exits by itself."""

    pid = 0

    def poll(self) -> int | None:
        """Never finished."""
        return None

    def terminate(self) -> None:
        """Nothing to stop."""

    def wait(self, timeout: float | None = None) -> int:
        """Nothing to wait for."""
        return 0


def _port_suffix(ctx: PlayCtx) -> str:
    return "" if ctx.config.port == 7979 else f" --port {ctx.config.port}"


def _url_suffix(ctx: PlayCtx) -> str:
    url = ctx.config.platform_url
    return "" if url == DEFAULT_PLATFORM_URL else f" --platform-url {url}"
