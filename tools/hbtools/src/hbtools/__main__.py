"""Entry points: hb-doctor and hb-play (started by doctor.cmd/.sh and play.cmd/.sh)."""

from __future__ import annotations

import argparse
import logging
import os
import sys
from pathlib import Path
from typing import Any

from typani import Err, Ok

from hbtools import log as hblog
from hbtools import play
from hbtools.checks import DEMOS, Ctx, Options
from hbtools.collect import write_bundle
from hbtools.config import load_config
from hbtools.doctor import exit_code, run_doctor
from hbtools.host import detect_host, python_summary
from hbtools.net import RealSockets, UrllibHttp
from hbtools.proc import RealRunner
from hbtools.project import find_repo_root
from hbtools.prompt import ConsolePrompter
from hbtools.report import Reporter

logger = logging.getLogger(__name__)


def _repo_root() -> Path | None:
    """The game folder: from where the command was typed, else next to this tool."""
    return find_repo_root(Path.cwd()) or find_repo_root(Path(__file__).resolve())


def _common(parser: argparse.ArgumentParser, sub: bool = False) -> None:
    # On a subcommand the defaults are suppressed so `play host --port 7980` and
    # `play --port 7980 host` both work without the subcommand erasing the value.
    extra: dict[str, Any] = {"default": argparse.SUPPRESS} if sub else {}
    parser.add_argument(
        "--platform-url",
        **extra,
        help="platform address, e.g. http://192.168.1.20:8000 "
        "(default: HULLBREACH_PLATFORM_URL or http://127.0.0.1:8000)",
    )
    parser.add_argument(
        "--port",
        type=int,
        help="game UDP port (default 7979)",
        **extra,
    )
    parser.add_argument(
        "--verbose",
        action="store_true",
        help="also print the tool log to the screen",
        **extra,
    )


def _build_doctor_parser() -> argparse.ArgumentParser:
    """The doctor's command line."""
    parser = argparse.ArgumentParser(
        prog="doctor",
        description="Checks this computer for every Project Hullbreach demo and "
        "says exactly how to fix what is missing.",
    )
    parser.add_argument("--demo", choices=DEMOS, help="check only what one demo needs")
    parser.add_argument(
        "--deep",
        action="store_true",
        help="also start Unity headless: license and compile checks (slow)",
    )
    parser.add_argument(
        "--login",
        action="store_true",
        help="try a real platform sign-in (typed at a prompt, never stored)",
    )
    parser.add_argument(
        "--fix-firewall",
        action="store_true",
        help="add the firewall rule for the game port without asking",
    )
    parser.add_argument(
        "--collect",
        action="store_true",
        help="zip the logs and this report (secrets removed) to send to a teammate",
    )
    _common(parser)
    return parser


def _build_play_parser() -> argparse.ArgumentParser:
    """The play command line: one subcommand per way to start the game."""
    parser = argparse.ArgumentParser(
        prog="play", description="Build and start Project Hullbreach."
    )
    _common(parser)
    sub = parser.add_subparsers(dest="command", required=True)

    def watched(p: argparse.ArgumentParser, headless: bool = True) -> None:
        _common(p, sub=True)
        if headless:
            p.add_argument(
                "--headless", action="store_true", help="no window (tests, servers)"
            )
        p.add_argument(
            "--seconds", type=float, help="stop the game(s) after this many seconds"
        )

    b = sub.add_parser("build", help="build the game (client) or the dedicated server")
    _common(b, sub=True)
    b.add_argument("kind", nargs="?", choices=["client", "server"], default="client")
    b.add_argument("--backend", choices=["auto", "il2cpp", "mono"], default="auto")
    b.add_argument("--release", action="store_true", help="non-development build")
    b.add_argument(
        "--target",
        choices=["windows", "linux", "macos"],
        help="build for another operating system (default: this one)",
    )

    o = sub.add_parser("online", help="the Relay lobby (Unity online services)")
    watched(o)
    o.add_argument(
        "--profile",
        help="Unity Services profile, so a second copy on this computer is "
        "another player (letters, digits, - and _)",
    )
    o.add_argument(
        "--require-auth",
        action="store_true",
        help="when this copy hosts, refuse players who are not signed in",
    )

    h = sub.add_parser("host", help="host a match on this computer (LAN/direct)")
    watched(h)
    h.add_argument("--platform", action="store_true", help="sign in to the platform")
    h.add_argument(
        "--require-auth",
        action="store_true",
        help="refuse players who are not signed in",
    )

    j = sub.add_parser("join", help="join a host or server by its IP address")
    j.add_argument("address", help="the host's IP address, e.g. 192.168.1.20")
    watched(j)
    j.add_argument("--platform", action="store_true", help="sign in to the platform")

    s = sub.add_parser("server", help="a dedicated server with no window")
    watched(s, headless=False)
    s.add_argument("--platform", action="store_true", help="verify players' sign-ins")
    s.add_argument(
        "--require-auth",
        action="store_true",
        help="refuse players who are not signed in",
    )
    s.add_argument(
        "--skip-key-check",
        action="store_true",
        help="start even if the platform refuses this server's key",
    )

    t = sub.add_parser("two-local", help="host and client on this computer (solo demo)")
    watched(t)

    lg = sub.add_parser("logs", help="show the newest game log")
    _common(lg, sub=True)
    lg.add_argument("--follow", action="store_true", help="keep showing new lines")
    lg.add_argument("--lines", type=int, default=40)
    return parser


def doctor_main(argv: list[str] | None = None) -> int:
    """hb-doctor: run the checklist (and optionally bundle logs)."""
    args = _build_doctor_parser().parse_args(argv)
    reporter = Reporter()
    root = _repo_root()
    if root is None:
        reporter.say(
            "[FAIL] This is not the Project Hullbreach folder.\n"
            "       Open a terminal in the folder you cloned (it has doctor.cmd and\n"
            "       doctor.sh in it) and run the doctor from there."
        )
        return 1
    hblog.configure(root, args.verbose)
    env = dict(os.environ)
    host = detect_host(env)
    logger.info("doctor %s on %s", args, python_summary())
    ctx = Ctx(
        host=host,
        config=load_config(root, env, args.platform_url, args.port),
        runner=RealRunner(),
        http=UrllibHttp(),
        sockets=RealSockets(),
        env=env,
        prompter=ConsolePrompter(),
        options=Options(
            deep=args.deep, login=args.login, fix_firewall=args.fix_firewall
        ),
    )
    results = run_doctor(ctx, reporter, args.demo)
    if args.collect:
        match write_bundle(root, host, reporter.lines, env):
            case Ok(path):
                reporter.say(f"Made {path}. Send that file to a teammate.")
            case Err(error):
                reporter.say(f"[FAIL] Could not write the zip: {error}")
                return 1
    return exit_code(results)


def play_main(argv: list[str] | None = None) -> int:
    """hb-play: build, host, join, serve, or show logs."""
    args = _build_play_parser().parse_args(argv)
    reporter = Reporter()
    root = _repo_root()
    if root is None:
        reporter.say("[FAIL] Run play from the Project Hullbreach folder.")
        return 1
    hblog.configure(root, args.verbose)
    env = dict(os.environ)
    logger.info("play %s", args.command)
    ctx = play.PlayCtx(
        host=detect_host(env),
        config=load_config(root, env, args.platform_url, args.port),
        runner=RealRunner(),
        http=UrllibHttp(),
        env=env,
        prompter=ConsolePrompter(),
        reporter=reporter,
    )
    match args.command:
        case "build":
            return play.build(ctx, args.kind, args.backend, args.release, args.target)
        case "online":
            return play.online(
                ctx, args.headless, args.seconds, args.profile, args.require_auth
            )
        case "host":
            if args.require_auth and not args.platform:
                reporter.say("[FAIL] --require-auth needs --platform too.")
                return 2
            return play.host(
                ctx, args.headless, args.seconds, args.platform, args.require_auth
            )
        case "join":
            return play.join(
                ctx, args.address, args.headless, args.seconds, args.platform
            )
        case "server":
            if args.require_auth and not args.platform:
                reporter.say("[FAIL] --require-auth needs --platform too.")
                return 2
            return play.server(
                ctx, args.seconds, args.platform, args.require_auth, args.skip_key_check
            )
        case "two-local":
            return play.two_local(ctx, args.headless, args.seconds)
        case "logs":
            return play.logs(ctx, args.follow, args.lines)
    raise AssertionError(f"unknown command {args.command}")


if __name__ == "__main__":
    sys.exit(doctor_main())
