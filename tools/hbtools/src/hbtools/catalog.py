"""Every doctor check, top to bottom, with plain-English fixes and README links."""

from __future__ import annotations

import logging
import re
import urllib.parse

from typani import Err, Ok

from hbtools import unity
from hbtools.builds import Build, build_folder, changed_since, read_build
from hbtools.checks import (
    DEMOS,
    DEV,
    Check,
    Ctx,
    Outcome,
    failed,
    passed,
    skipped,
    warned,
)
from hbtools.config import (
    PASSWORD_ENV,
    SERVER_KEY_ENV,
    URL_ENV,
    USERNAME_ENV,
)
from hbtools.errors import PlatformError
from hbtools.host import (
    EDITOR_ENV,
    PRODUCT,
    UNITY_VERSION,
    python_summary,
    set_env_line,
)
from hbtools.log import log_dir
from hbtools.net import best_lan_address, is_loopback_url
from hbtools.platform_api import KeyVerdict, PlatformApi
from hbtools.proc import Completed
from hbtools.project import (
    GAME_SCENE,
    LOBBY_SCENE,
    SIGNIN_PREFAB,
    ProjectFacts,
    editor_has_project_open,
    read_project,
)

logger = logging.getLogger(__name__)

README = "README.md#"
# README anchors the checks point at (tests prove each one exists).
DOC_DOCTOR = README + "step-1-run-the-doctor"
DOC_GIT = README + "02-install-git"
DOC_CLONE = README + "03-download-clone-the-game"
DOC_UNITY = README + "04-install-unity-hub-and-the-exact-unity-version"
DOC_SETUP = README + "setup-and-unity"
DOC_SERVICES = (
    README + "21-link-the-project-to-unitys-online-services-once-per-computer"
)
DOC_RELAY = README + "relay-lobby"
DOC_BUILD = README + "build-the-game-with-one-command"
DOC_LAN = README + "direct--lan"
DOC_IP = README + "32-find-the-hosts-ip-address"
DOC_SIGNIN = README + "41-the-sign-in-screen-is-already-in-the-project"
DOC_PLATFORM = README + "42-start-the-platform-and-make-an-account"
DOC_KEY = README + "45-demo-with-sign-in-required"
DOC_REMOTE = README + "46-players-on-another-computer"
DOC_PLATFORM_TROUBLE = README + "platform-sign-in-guide-4"
DOC_LOGS = README + "where-the-logs-are"
DOC_DEV = README + "checks"

# Hosts the Relay lobby talks to; any HTTP answer at all proves they are reachable.
UNITY_SERVICE_URLS = (
    "https://services.api.unity.com/",
    "https://lobby.services.api.unity.com/",
    "https://relay-allocations.services.api.unity.com/",
)
# A name no account has: a 401 for it proves the accounts table exists.
MIGRATION_PROBE_USER = "hullbreach-doctor-probe"

ALL = frozenset(DEMOS) | {DEV}
LAN_DEMOS = frozenset({"guest", "platform"})


def _run(ctx: Ctx, argv: list[str], timeout: float = 30.0) -> Completed | None:
    """Run a helper program in the repo; None when it could not run at all."""
    match ctx.runner.run(argv, cwd=ctx.config.repo_root, timeout=timeout):
        case Ok(done):
            return done
        case Err(error):
            logger.info("%s could not run: %s", argv[0], error)
            return None
    raise AssertionError("unreachable")


def _facts(ctx: Ctx) -> ProjectFacts:
    """Project facts found by project.files (it runs before every user of this)."""
    facts = ctx.state["facts"]
    assert isinstance(facts, ProjectFacts)
    return facts


def _api(ctx: Ctx) -> PlatformApi:
    return PlatformApi(ctx.http, ctx.config.api)


def _cmd(ctx: Ctx, kind: str) -> str:
    """How to type our own commands on this computer (doctor.cmd vs ./doctor.sh)."""
    return ctx.host.doctor_cmd if kind == "doctor" else ctx.host.play_cmd


def _terminal_steps(ctx: Ctx) -> str:
    if ctx.host.kind == "windows":
        return "Close this window and open a new Command Prompt, so it sees the change."
    return "Close this terminal and open a new one, so it sees the change."


# --- computer and copy of the game -----------------------------------------


def check_os(ctx: Ctx) -> Outcome:
    """Which computer this is; WSL cannot run the Windows Unity tools."""
    detail = f"{ctx.host.os_name}, shell {ctx.host.shell}, {python_summary()}"
    if ctx.host.is_wsl:
        return failed(
            detail + "\nThis is WSL (Linux inside Windows). Unity and the game "
            "run on the Windows side, so the doctor must too.",
            [
                "Open the Start menu, type cmd and press Enter.",
                "Go to a copy of the game cloned on the Windows side, e.g.\n"
                "cd %USERPROFILE%\\project-hullbreach-game",
                "Type doctor.cmd and press Enter.",
            ],
        )
    return passed(detail)


def check_git(ctx: Ctx) -> Outcome:
    """Git downloads and updates the game."""
    done = _run(ctx, ["git", "--version"])
    if done is not None and done.code == 0:
        return passed(done.stdout.strip())
    if ctx.host.kind == "windows":
        steps = [
            "Open https://git-scm.com/download/win and download 'Git for Windows'.",
            "Run the installer and click Next on every page (the defaults are fine).",
            _terminal_steps(ctx),
            "Type git --version. You should see something like 'git version 2.4x'.",
        ]
    elif ctx.host.kind == "macos":
        steps = [
            "Type xcode-select --install and press Enter.",
            "Click Install in the window that pops up, then wait for it to finish.",
            "Type git --version. You should see 'git version ...'.",
        ]
    else:
        steps = [
            "Type sudo apt install git (Fedora: sudo dnf install git).",
            "Type git --version. You should see 'git version ...'.",
        ]
    return failed("Git is not installed (or not on PATH).", steps)


def check_clone(ctx: Ctx) -> Outcome:
    """A real git clone (not a ZIP download) so `git pull` can update it."""
    top = _run(ctx, ["git", "rev-parse", "--show-toplevel"])
    if top is None or top.code != 0:
        return failed(
            f"{ctx.config.repo_root} is not a git clone (was it downloaded as a ZIP?).",
            [
                "Follow README step 0.3 to clone the game with git.",
                f"Then run {_cmd(ctx, 'doctor')} again from inside the new folder.",
            ],
        )
    origin = _run(ctx, ["git", "remote", "get-url", "origin"])
    url = origin.stdout.strip() if origin is not None and origin.code == 0 else ""
    if "project-hullbreach-game" not in url:
        return warned(
            f"This clone's origin is '{url or 'missing'}', not the team repository.",
            [
                "If this is a fork, that is fine.",
                "Otherwise type: git remote set-url origin "
                "https://github.com/lognd/project-hullbreach-game.git",
            ],
        )
    return passed(f"git clone of {url}")


def check_uptodate(ctx: Ctx) -> Outcome:
    """Demos break when your copy is older than the team's main branch."""
    branch_run = _run(ctx, ["git", "rev-parse", "--abbrev-ref", "HEAD"])
    branch = branch_run.stdout.strip() if branch_run is not None else "?"
    fetch = _run(ctx, ["git", "fetch", "--quiet", "origin", "main"], timeout=90)
    if fetch is None or fetch.code != 0:
        return warned(
            "Could not ask GitHub for news (offline, or GitHub needs a sign-in).",
            ["Check your internet connection, then run the doctor again."],
        )
    behind_run = _run(ctx, ["git", "rev-list", "--count", "HEAD..origin/main"])
    behind = (
        int(behind_run.stdout.strip())
        if behind_run is not None and behind_run.stdout.strip().isdigit()
        else 0
    )
    dirty_run = _run(ctx, ["git", "status", "--porcelain"])
    dirty = len(dirty_run.stdout.splitlines()) if dirty_run is not None else 0
    note = f"branch {branch}, {dirty} locally changed file(s)"
    if behind == 0:
        return passed(f"up to date with origin/main ({note})")
    if branch == "main":
        steps = [
            "Type git pull and press Enter.",
            "You should see a list of updated files (or 'Already up to date').",
        ]
    else:
        steps = [
            f"You are on branch '{branch}'. To get the newest game type:\n"
            "git switch main\ngit pull",
        ]
    if dirty:
        steps.insert(0, "You have local changes; save them first with: git stash")
    return warned(f"{behind} new commit(s) on origin/main ({note}).", steps)


def check_path(ctx: Ctx) -> Outcome:
    """Unity dislikes network, synced and very long folder paths."""
    text = str(ctx.config.repo_root)
    if text.startswith("\\\\"):
        return failed(
            f"The game is on a network path ({text}). Unity cannot open it.",
            ["Clone the game into a normal folder, e.g. %USERPROFILE%\\projects."],
        )
    problems = []
    if " " in text:
        problems.append("the folder path has spaces (Unity build tools misbehave)")
    if not text.isascii():
        problems.append("the folder name has non-English letters")
    if "onedrive" in text.lower() or "dropbox" in text.lower():
        problems.append("the folder is synced by OneDrive/Dropbox (slow, locks files)")
    if ctx.host.kind == "windows" and len(text) > 100:
        problems.append("the folder path is very long (Windows limits paths)")
    if problems:
        return warned(
            f"{text}: " + "; ".join(problems) + ".",
            ["Clone the game into a short plain folder such as C:\\hb and use that."],
        )
    return passed(text)


def check_project(ctx: Ctx) -> Outcome:
    """The Unity project files are here and name the Unity version we expect."""
    match read_project(ctx.config.repo_root):
        case Ok(facts):
            ctx.state["facts"] = facts
        case Err(error):
            return failed(
                f"Could not read the Unity project ({error}).",
                [
                    "Run the doctor from the game folder (the one with README.md).",
                    "If files are missing, type git status to see what changed.",
                ],
            )
    if facts.unity_version != UNITY_VERSION:
        return failed(
            f"The project says Unity {facts.unity_version}; these tools expect "
            f"{UNITY_VERSION}.",
            ["Type git pull to update the tools, then run the doctor again."],
        )
    return passed(
        f"Unity project for {facts.unity_version}, scenes {len(facts.scenes)}"
    )


def check_lfs(ctx: Ctx) -> Outcome:
    """Large files come through Git LFS only if the repository uses it."""
    if not _facts(ctx).uses_lfs:
        return skipped("This repository does not use Git LFS.")
    done = _run(ctx, ["git", "lfs", "version"])
    if done is not None and done.code == 0:
        return passed(done.stdout.strip())
    return failed(
        "This repository stores big files with Git LFS, which is not installed.",
        [
            "Install it from https://git-lfs.com, then type: git lfs install",
            "Then type: git lfs pull",
        ],
    )


# --- Unity -------------------------------------------------------------------


def check_hub(ctx: Ctx) -> Outcome:
    """Unity Hub installs the editor and its build modules."""
    hub = unity.find_hub(ctx.host)
    if hub is not None:
        ctx.state["hub"] = hub
        return passed(str(hub))
    steps = [
        "Open https://unity.com/download and click 'Download Unity Hub'.",
        "Install it, open it, and sign in (a free Unity account is fine).",
        f"Run {_cmd(ctx, 'doctor')} again.",
    ]
    if unity.find_editor(ctx.host) is not None:
        return warned("Unity Hub was not found (the editor itself was).", steps)
    return failed("Unity Hub is not installed.", steps)


def check_editor(ctx: Ctx) -> Outcome:
    """The exact editor version the project was made with."""
    editor = unity.find_editor(ctx.host)
    if editor is not None:
        ctx.state["editor"] = editor
        return passed(f"Unity {UNITY_VERSION} at {editor}")
    others = unity.other_versions(ctx.host)
    detail = f"Unity {UNITY_VERSION} is not installed."
    if others:
        detail += (
            f" You have {', '.join(others)}; the project needs exactly {UNITY_VERSION}."
        )
    return failed(
        detail,
        [
            "Copy this link into your web browser's address bar: "
            f"{unity.HUB_DEEP_LINK}\n"
            "Unity Hub opens an 'Install Unity' window for the right version.",
            "On the modules page tick the build support for your computer "
            "(Windows Build Support / Mac Build Support / Linux Build Support).",
            "Click Install and wait (it is a big download).",
            f"If Unity lives somewhere unusual, set {EDITOR_ENV} to the "
            "full path of the Unity program.",
        ],
    )


def check_modules(ctx: Ctx) -> Outcome:
    """Build modules decide which builds this computer can make."""
    editor = ctx.state["editor"]
    statuses = unity.module_status(ctx.host, editor)
    hub = ctx.state.get("hub")
    missing = [s for s in statuses if not s.installed]
    have = ", ".join(s.spec.title for s in statuses if s.installed) or "none"
    if not missing:
        return passed(f"installed: {have}")
    steps = []
    for status in missing:
        steps.append(
            f"{status.spec.title}: {status.spec.needed_for}.\n"
            "Unity Hub -> Installs -> gear icon next to "
            f"{UNITY_VERSION} -> Add modules -> tick it -> Install.\n"
            "Or type: " + unity.hub_install_command(ctx.host, hub, status.spec.hub_id)
        )
    detail = f"installed: {have}\nmissing: " + ", ".join(s.spec.title for s in missing)
    required = statuses[0]
    if not required.installed:
        return failed(detail, steps)
    return warned(detail, steps)


def check_cpp(ctx: Ctx) -> Outcome:
    """IL2CPP builds need Visual Studio's C++ tools; Mono builds do not."""
    editor = ctx.state["editor"]
    if ctx.host.kind != "windows" or ctx.host.vswhere is None:
        return skipped("Builds use Mono here; no C++ compiler needed.")
    variations = unity.installed_variations(
        ctx.host, editor, "windowsstandalonesupport"
    )
    if not any("il2cpp" in v for v in variations):
        return skipped("IL2CPP is not installed, so builds use Mono (no C++ needed).")
    done = _run(
        ctx,
        [
            str(ctx.host.vswhere),
            "-products",
            "*",
            "-requires",
            "Microsoft.VisualStudio.Component.VC.Tools.x86.x64",
            "Microsoft.VisualStudio.Component.VC.Tools.ARM64",
            "-requiresAny",
            "-property",
            "installationPath",
        ],
    )
    if done is not None and done.code == 0 and done.stdout.strip():
        return passed(f"C++ tools in {done.stdout.strip().splitlines()[0]}")
    return failed(
        "IL2CPP is installed but Visual Studio's C++ tools are not.",
        [
            "Open 'Visual Studio Installer' (install Visual Studio 2022 Community "
            "from https://visualstudio.microsoft.com if you have none).",
            "Click Modify, tick 'Desktop development with C++', click Install.",
            f"Or skip IL2CPP: {_cmd(ctx, 'play')} build --backend mono",
        ],
    )


def check_running(ctx: Ctx) -> Outcome:
    """Headless builds cannot open a project that an editor already has open."""
    if editor_has_project_open(ctx.config.repo_root):
        return warned(
            "Unity seems to have this project open (Temp/UnityLockfile exists).",
            [
                "Close Unity (File -> Exit) before 'play build' or 'doctor --deep'.",
                "If Unity is not open, it crashed earlier; opening and closing the "
                "project in Unity clears this.",
            ],
        )
    return passed("no Unity editor has this project open")


def check_license(ctx: Ctx) -> Outcome:
    """Unity refuses to run without an activated license (slow, so opt-in)."""
    if not ctx.options.deep:
        return skipped(f"slow; run {_cmd(ctx, 'doctor')} --deep to test it")
    run = unity.license_probe(
        ctx.runner, ctx.state["editor"], log_dir(ctx.config.repo_root)
    )
    match run:
        case Ok(done) if done.code == 0:
            return passed("Unity started headless with a valid license")
        case Ok(done):
            log = done.log_path
        case Err(_):
            log = log_dir(ctx.config.repo_root) / "license-probe.log"
    return failed(
        f"Unity could not start headless (see {log}).",
        [
            "Open Unity Hub and sign in (top left).",
            "Go to Preferences (gear) -> Licenses -> Add -> 'Get a free personal "
            "license'.",
            f"Run {_cmd(ctx, 'doctor')} --deep again.",
        ],
    )


def check_compile(ctx: Ctx) -> Outcome:
    """Open the project headless once: every script must compile (opt-in, slow)."""
    if not ctx.options.deep:
        return skipped(f"slow; run {_cmd(ctx, 'doctor')} --deep to test it")
    root = ctx.config.repo_root
    if editor_has_project_open(root):
        return warned(
            "Unity has the project open; close it to run this check.",
            ["Close Unity, then run the doctor with --deep again."],
        )
    log = log_dir(root) / "compile.log"
    run = unity.run_headless(
        ctx.runner,
        ctx.state["editor"],
        unity.compile_args(root),
        log,
        unity.IMPORT_TIMEOUT,
    )
    match run:
        case Ok(done) if done.succeeded:
            return passed("project opened headless with zero compile errors")
        case Ok(done):
            errors = "\n".join(done.compile_errors[:5]) or f"Unity exited {done.code}"
        case Err(error):
            errors = str(error)
    return failed(
        f"The project does not compile (log: {log}):\n{errors}",
        [
            "Type git status. If you changed scripts, undo with: git stash",
            "Type git pull to get the newest working code.",
            f"Still broken? Run {_cmd(ctx, 'doctor')} --collect and send the zip "
            "to a teammate.",
        ],
    )


# --- online (Relay lobby) -----------------------------------------------------


def check_services_linked(ctx: Ctx) -> Outcome:
    """The Relay lobby needs the project linked to a Unity Cloud project."""
    cloud = _facts(ctx).cloud_project_id
    if cloud:
        return passed(f"linked to Unity Cloud project {cloud}")
    return failed(
        "The project is not linked to Unity's online services.",
        [
            "Open the project in Unity.",
            "Edit -> Project Settings -> Services -> pick the organization and "
            "the Project Hullbreach cloud project -> Link.",
            "Save, close Unity, run the doctor again.",
        ],
    )


def check_services_reachable(ctx: Ctx) -> Outcome:
    """The lobby and Relay are on the internet; school networks may block them."""
    bad = []
    for url in UNITY_SERVICE_URLS:
        reply = ctx.http.request("GET", url, timeout=8.0)
        if reply.is_err:
            bad.append(url)
    if not bad:
        return passed("Unity Lobby and Relay answer from this network")
    return failed(
        "Cannot reach: " + ", ".join(bad),
        [
            "Check the internet connection (open https://unity.com in a browser).",
            "School and office networks sometimes block game services; try a "
            "phone hotspot.",
            "No internet? Use the guest (LAN) demo instead: doctor --demo guest.",
        ],
    )


# --- platform sign-in screen -------------------------------------------------


def check_signin_prefab(ctx: Ctx) -> Outcome:
    """Platform mode needs the sign-in screen generated into the lobby scene."""
    facts = _facts(ctx)
    if facts.signin_prefab_present and facts.signin_in_lobby:
        return passed(f"{SIGNIN_PREFAB} is in {LOBBY_SCENE}")
    what = "is missing" if not facts.signin_prefab_present else "is not in the lobby"
    return failed(
        f"The sign-in screen {what}.",
        [
            "Type git pull: the sign-in screen ships with the game.",
            "Still missing? Open the project in Unity and click the menu "
            "Hullbreach -> Online -> Add sign-in screen to lobby scene, then save.",
        ],
    )


# --- builds ------------------------------------------------------------------


def _client_build(ctx: Ctx) -> Build:
    build = ctx.state["build"]
    assert isinstance(build, Build)
    return build


def check_build_present(ctx: Ctx) -> Outcome:
    """The play commands launch a build, not the editor."""
    folder = build_folder(ctx.config.builds_dir, ctx.host.target, "client")
    match read_build(folder):
        case Ok(build) if build.executable.exists():
            ctx.state["build"] = build
            kind = "development" if build.stamp.development else "release"
            return passed(f"{kind} build at {folder} (commit {build.stamp.commit})")
        case _:
            pass
    return failed(
        f"There is no game build in {folder} yet.",
        [
            "Close Unity if it is open.",
            f"Type: {_cmd(ctx, 'play')} build",
            "Wait. The first build can take 30-60 minutes; you should see "
            "'[PASS] Built the ... client' at the end.",
        ],
    )


def check_build_fresh(ctx: Ctx) -> Outcome:
    """A build older than the code shows old behaviour and confuses everyone."""
    build = _client_build(ctx)
    changed = changed_since(ctx.runner, ctx.config.repo_root, build.stamp.commit)
    if not changed:
        return passed(
            f"built from {build.stamp.commit}; the code has not changed since"
        )
    shown = "\n".join(changed[:5]) + ("\n..." if len(changed) > 5 else "")
    return warned(
        f"{len(changed)} game file(s) changed since this build:\n{shown}",
        [f"Rebuild: {_cmd(ctx, 'play')} build"],
    )


def check_build_scenes(ctx: Ctx) -> Outcome:
    """The lobby must open first and the match scene must be in the build."""
    scenes = _client_build(ctx).stamp.scenes
    if scenes and scenes[0] == LOBBY_SCENE and GAME_SCENE in scenes:
        return passed(
            f"{len(scenes)} scenes, LobbyScene first, MultiplayerGame included"
        )
    return failed(
        f"The build's scenes are wrong: {scenes or 'not recorded'}.",
        [
            "Type git status: ProjectSettings/EditorBuildSettings.asset should be "
            "unchanged (git checkout -- ProjectSettings/EditorBuildSettings.asset).",
            f"Rebuild: {_cmd(ctx, 'play')} build",
        ],
    )


def check_build_server(ctx: Ctx) -> Outcome:
    """A dedicated-server build is optional; the game build can act as one."""
    folder = build_folder(ctx.config.builds_dir, ctx.host.target, "server")
    if read_build(folder).is_ok:
        return passed(f"dedicated server build at {folder}")
    return passed(
        "no separate server build; 'play server' runs the game build as a "
        "headless server instead (same networking code)"
    )


# --- network -----------------------------------------------------------------


def check_port(ctx: Ctx) -> Outcome:
    """The host listens on one UDP port; something else may already have it."""
    port = ctx.config.port
    if ctx.sockets.udp_port_free(port):
        return passed(f"UDP port {port} is free")
    return warned(
        f"UDP port {port} is already in use (maybe the game is already running).",
        [
            "Close any other copy of the game (and 'play server' windows).",
            f"Or pick another port on BOTH computers: {_cmd(ctx, 'play')} host "
            f"--port {port + 1}  and  {_cmd(ctx, 'play')} join <ip> --port {port + 1}",
        ],
    )


def _firewall_ok(ctx: Ctx) -> tuple[bool, bool, str]:
    """(allowed, blocked, raw status) from the read-only firewall commands."""
    plan = ctx.host.firewall(ctx.config.port)
    texts = []
    for argv in plan.status_argvs:
        done = _run(ctx, argv)
        if done is not None:
            texts.append(done.text)
    text = "\n".join(texts)
    blocked = bool(plan.block_pattern) and bool(re.search(plan.block_pattern, text))
    allowed = any(marker in text for marker in plan.ok_markers)
    return allowed, blocked, text


def check_firewall(ctx: Ctx) -> Outcome:
    """Friends cannot join through a firewall that blocks the game's port."""
    plan = ctx.host.firewall(ctx.config.port)
    allowed, blocked, text = _firewall_ok(ctx)
    if allowed and not blocked:
        return passed(f"the firewall lets UDP {ctx.config.port} in")
    if not text and ctx.host.kind == "linux":
        return passed("no ufw firewall found; with firewalld: " + plan.fix_text)
    if plan.fix_argv and not blocked:
        agreed = ctx.options.fix_firewall or ctx.prompter.confirm(
            f"Add a firewall rule that lets the game use UDP {ctx.config.port}? "
            "Windows will ask for administrator permission."
        )
        if agreed:
            logger.info("adding firewall rule for UDP %s", ctx.config.port)
            _run(ctx, plan.fix_argv, timeout=180)
            allowed, blocked, _ = _firewall_ok(ctx)
            if allowed and not blocked:
                return passed(f"added a firewall rule for UDP {ctx.config.port}")
    detail = (
        "A firewall rule BLOCKS the game (someone clicked Cancel on the prompt)."
        if blocked
        else f"No firewall rule lets UDP {ctx.config.port} in yet."
    )
    steps = [plan.prompt_help]
    if ctx.host.kind == "windows":
        steps += [
            "Or: Start menu -> type PowerShell -> right-click -> Run as "
            f"administrator -> type:\n{plan.fix_text}",
            f"Or let the doctor do it: {_cmd(ctx, 'doctor')} --fix-firewall",
        ]
        if blocked:
            steps.insert(
                0,
                "Start menu -> 'Windows Defender Firewall with Advanced Security' "
                f"-> Inbound Rules -> delete the '{PRODUCT}' rules marked with a "
                "red circle.",
            )
    else:
        steps.append(plan.fix_text)
    steps.append("Only the computer that hosts needs this; joining works without it.")
    return warned(detail, steps)


def check_lan(ctx: Ctx) -> Outcome:
    """Which address a friend on the same network types to join you."""
    addresses = ctx.sockets.local_addresses()
    best = best_lan_address(addresses)
    if best is None:
        return warned(
            f"No home-network address found (have: {addresses or 'none'}).",
            [
                "Connect to Wi-Fi or a network cable, then run the doctor again.",
                "Without a network you can still play on this computer: "
                f"{_cmd(ctx, 'play')} two-local",
            ],
        )
    ctx.state["lan"] = best
    others = [a for a in addresses if a != best]
    extra = f" (other addresses: {', '.join(others)})" if others else ""
    return passed(
        f"give a friend on the same network this address: {best}{extra}\n"
        f"they type: {_cmd(ctx, 'play')} join {best}"
    )


# --- platform ----------------------------------------------------------------


def check_platform_url(ctx: Ctx) -> Outcome:
    """Where the platform (accounts server) is expected to be."""
    url = ctx.config.platform_url
    parsed = urllib.parse.urlparse(url)
    if parsed.scheme not in ("http", "https") or not parsed.hostname:
        return failed(
            f"'{url}' is not a web address.",
            [
                "Use a full address like http://127.0.0.1:8000 "
                f"(--platform-url or {URL_ENV})."
            ],
        )
    return passed(f"platform address {url}")


def check_platform_health(ctx: Ctx) -> Outcome:
    """The platform must be running before anyone can sign in."""
    match _api(ctx).health():
        case Ok(health):
            ctx.state["platform_version"] = health.version
            return passed(f"platform is up, version {health.version}")
        case Err(error):
            detail = f"Nothing answered at {ctx.config.api}/health ({error})."
    return failed(
        detail,
        [
            "The platform is a separate program (the 'platform' repository). In its "
            "folder run its own doctor, then start it (see its README).",
            "You should see 'Uvicorn running on http://127.0.0.1:8000'.",
            "If it runs on another computer, point the game at it: "
            f"{_cmd(ctx, 'doctor')} --platform-url http://<that-ip>:8000",
        ],
    )


def check_platform_ready(ctx: Ctx) -> Outcome:
    """Up is not enough: the platform's database must answer too."""
    match _api(ctx).ready():
        case Ok(reply) if reply.status == 200:
            return passed("the platform's database answers")
        case Ok(reply) if reply.status == 404:
            return warned("this platform is too old to report readiness (/ready).", [])
        case Ok(reply):
            detail = f"/ready answered {reply.status}: {reply.detail}"
        case Err(error):
            detail = str(error)
    return failed(
        f"The platform runs but its database does not answer ({detail}).",
        [
            "In the platform folder start the database: docker compose up -d db",
            "Run the platform's doctor; it explains database problems in detail.",
        ],
    )


def check_platform_migrations(ctx: Ctx) -> Outcome:
    """A sign-in for a made-up name must say "wrong password", not crash."""
    match _api(ctx).login(MIGRATION_PROBE_USER, "not-a-real-password"):
        case Err(PlatformError.BadCredentials) | Err(PlatformError.RateLimited):
            return passed("the accounts table exists (migrations applied)")
        case Ok(_):
            return passed("the accounts table exists")
        case Err(error):
            detail = str(error)
    return failed(
        f"Sign-in does not work on the platform ({detail}); usually the database "
        "tables are missing.",
        [
            "In the platform folder type: uv run hullbreach_server db upgrade",
            "Restart the platform and run this doctor again.",
        ],
    )


def check_http_dev(ctx: Ctx) -> Outcome:
    """Unity only allows plain http:// addresses in development builds."""
    url = ctx.config.platform_url
    if not url.startswith("http://"):
        return passed("https platform address; any build may use it")
    build = _client_build(ctx)
    if build.stamp.development or _facts(ctx).insecure_http == 2:
        return passed("plain http is fine: this is a development build")
    return failed(
        "This is a release build, and Unity blocks plain http:// in release builds, "
        "so sign-in would fail.",
        [
            f"Make a development build: {_cmd(ctx, 'play')} build "
            "(development is the default).",
            "Or give the platform an https:// address.",
        ],
    )


def check_loopback(ctx: Ctx) -> Outcome:
    """127.0.0.1 means "this computer": wrong once players are on other machines."""
    url = ctx.config.platform_url
    lan = ctx.state.get("lan", "<your-lan-ip>")
    if is_loopback_url(url):
        return passed(
            f"{url} only works when the platform, server and players all run on "
            "this computer.\nFor players on other computers: start the platform "
            f"with --host 0.0.0.0 and use --platform-url http://{lan}:8000 everywhere."
        )
    return passed(
        f"{url} is a network address; every computer (players and server) must "
        "use this same address, and the platform must listen on --host 0.0.0.0."
    )


def check_key_set(ctx: Ctx) -> Outcome:
    """The game server proves itself to the platform with a key from the environment."""
    if ctx.env.get(SERVER_KEY_ENV):
        return passed(f"{SERVER_KEY_ENV} is set (value hidden)")
    return warned(
        f"{SERVER_KEY_ENV} is not set. Only the computer that runs the game "
        "server needs it, and only if the platform checks keys (see below).",
        [
            "Ask whoever runs the platform for a game server key (the platform "
            "keeps its list in HULLBREACH_GAME_SERVER_API_KEYS).",
            "In the window where you will start the server type:\n"
            + set_env_line(ctx.host, SERVER_KEY_ENV, "<the-key-you-were-given>"),
            "Never put the key on the command line of play or in a file you commit.",
        ],
    )


def check_login(ctx: Ctx) -> Outcome:
    """Optionally sign in for real, typed at a prompt and never stored."""
    if not ctx.options.login:
        return skipped(
            f"add --login to try a real sign-in ({_cmd(ctx, 'doctor')} --login)"
        )
    username = ctx.env.get(USERNAME_ENV) or ctx.prompter.ask("Platform username: ")
    password = ctx.env.get(PASSWORD_ENV) or ctx.prompter.secret(
        "Platform password (hidden): "
    )
    if not username or not password:
        return skipped(f"no username/password given (prompt or {PASSWORD_ENV})")
    match _api(ctx).login(username, password):
        case Ok(login):
            ctx.state["login"] = login
            return passed(f"signed in as {login.username} (the token stays in memory)")
        case Err(PlatformError.RateLimited):
            return warned("too many sign-in attempts; wait a minute and retry.", [])
        case Err(error):
            detail = str(error)
    return failed(
        f"Sign-in failed: {detail}",
        [
            "Check the username and password (they are case-sensitive).",
            "No account yet? Make one: see README 4.2.",
        ],
    )


def check_key_accepted(ctx: Ctx) -> Outcome:
    """Ask /auth/session exactly like the game server does, and explain the answer."""
    key = ctx.env.get(SERVER_KEY_ENV)
    login = ctx.state.get("login")
    token = login.token if login is not None else None
    match _api(ctx).key_verdict(key, token):
        case Ok(verdict):
            pass
        case Err(error):
            return warned(f"could not ask the platform about keys ({error})", [])
    platform_fix = [
        "On the platform computer the allowed keys are in "
        "HULLBREACH_GAME_SERVER_API_KEYS (comma separated).",
        f"Set {SERVER_KEY_ENV} on the server computer to exactly one of them "
        "(no quotes, no spaces):\n" + set_env_line(ctx.host, SERVER_KEY_ENV, "<key>"),
        "Restart the platform if you changed its keys, then run the doctor again.",
    ]
    if verdict is KeyVerdict.NOT_ENFORCED:
        return passed("this platform does not check game server keys")
    if verdict is KeyVerdict.MISSING:
        return warned(
            "The platform requires a game server key and none is set here: a server "
            "started from this window would reject every signed-in player.",
            platform_fix,
        )
    if verdict is KeyVerdict.INVALID:
        return failed(
            f"KEY MISMATCH: the platform refused the key in {SERVER_KEY_ENV} "
            "('invalid server key').",
            platform_fix,
        )
    if verdict is KeyVerdict.NOT_CONFIGURED:
        return failed(
            "The platform has no game server keys configured, so no game server "
            "can verify players.",
            [
                "On the platform computer set HULLBREACH_GAME_SERVER_API_KEYS to a "
                "long random value and restart the platform.",
                f"Give the same value to the server computer as {SERVER_KEY_ENV}.",
            ],
        )
    if verdict is KeyVerdict.UNKNOWN:
        return warned("The platform gave an unexpected answer about keys.", [])
    if token is None:
        return passed("the platform accepts this game server key")
    match _api(ctx).session(token, key):
        case Ok(reply) if reply.status == 200:
            return passed("the platform accepts this key and verified your sign-in")
        case Ok(reply):
            return warned(f"key accepted but the session check said {reply.status}", [])
        case Err(error):
            return warned(f"session check failed ({error})", [])
    raise AssertionError("unreachable")


# --- developer extras and logs ------------------------------------------------


def check_dotnet(ctx: Ctx) -> Outcome:
    """Developers run the engine-free C# tests with dotnet; players never need it."""
    done = _run(ctx, ["dotnet", "--list-sdks"])
    if done is not None and done.code == 0 and done.stdout.strip():
        return passed(
            "dotnet SDKs: "
            + ", ".join(ln.split()[0] for ln in done.stdout.splitlines() if ln.strip())
        )
    return warned(
        "dotnet is not installed (only needed to run tools/plaincs/run_tests.sh).",
        ["Install the .NET 8 SDK from https://dotnet.microsoft.com/download"],
    )


def check_logs(ctx: Ctx) -> Outcome:
    """Where to look when something goes wrong, and how to send it to a teammate."""
    root = ctx.config.repo_root
    lines = [
        f"game (Player.log): {ctx.host.player_log}",
        f"play command logs: {root / 'Logs' / 'play'}",
        f"Unity editor: {ctx.host.editor_log}",
        f"doctor log: {log_dir(root)}",
        f"to send everything to a teammate: {_cmd(ctx, 'doctor')} --collect",
    ]
    return passed("\n".join(lines))


SHELL = ("os.shell",)
EDITOR = ("unity.editor",)
BUILD = ("build.present",)
PLATFORM_UP = ("platform.health",)

CATALOG: list[Check] = [
    Check("os.shell", "Computer and terminal", check_os, DOC_DOCTOR, demos=ALL),
    Check("git.installed", "Git is installed", check_git, DOC_GIT, demos=ALL),
    Check(
        "repo.clone",
        "This folder is a git clone",
        check_clone,
        DOC_CLONE,
        ("git.installed",),
        ALL,
    ),
    Check(
        "repo.uptodate",
        "Up to date with origin/main",
        check_uptodate,
        DOC_CLONE,
        ("repo.clone",),
        ALL,
    ),
    Check(
        "repo.path",
        "Folder location is Unity-friendly",
        check_path,
        DOC_CLONE,
        demos=ALL,
    ),
    Check("project.files", "Unity project files", check_project, DOC_SETUP, demos=ALL),
    Check(
        "git.lfs",
        "Git LFS (large files)",
        check_lfs,
        DOC_CLONE,
        ("git.installed", "project.files"),
        ALL,
    ),
    Check("unity.hub", "Unity Hub", check_hub, DOC_UNITY, SHELL, ALL),
    Check(
        "unity.editor",
        f"Unity {UNITY_VERSION}",
        check_editor,
        DOC_UNITY,
        ("os.shell", "project.files"),
        ALL,
    ),
    Check(
        "unity.modules", "Unity build modules", check_modules, DOC_UNITY, EDITOR, ALL
    ),
    Check(
        "unity.cpp",
        "C++ tools for IL2CPP builds",
        check_cpp,
        DOC_SETUP,
        ("unity.modules",),
        ALL,
    ),
    Check(
        "unity.running",
        "Unity editor is closed",
        check_running,
        DOC_SETUP,
        ("project.files",),
        ALL,
    ),
    Check(
        "unity.license",
        "Unity license works (--deep)",
        check_license,
        DOC_SETUP,
        EDITOR,
        ALL,
    ),
    Check(
        "unity.compile",
        "Project compiles (--deep)",
        check_compile,
        DOC_SETUP,
        ("unity.license",),
        ALL,
    ),
    Check(
        "services.linked",
        "Linked to Unity online services",
        check_services_linked,
        DOC_SERVICES,
        ("project.files",),
        frozenset({"online", DEV}),
    ),
    Check(
        "services.reachable",
        "Unity Lobby and Relay reachable",
        check_services_reachable,
        DOC_RELAY,
        (),
        frozenset({"online", DEV}),
    ),
    Check(
        "signin.prefab",
        "Sign-in screen in the lobby",
        check_signin_prefab,
        DOC_SIGNIN,
        ("project.files",),
        frozenset({"platform", DEV}),
    ),
    Check(
        "build.present",
        "Game build exists",
        check_build_present,
        DOC_BUILD,
        ("os.shell", "project.files"),
        ALL,
    ),
    Check(
        "build.fresh",
        "Build matches the code",
        check_build_fresh,
        DOC_BUILD,
        ("build.present", "repo.clone"),
        ALL,
    ),
    Check(
        "build.scenes",
        "Build has the right scenes",
        check_build_scenes,
        DOC_BUILD,
        BUILD,
        ALL,
    ),
    Check(
        "build.server",
        "Server build",
        check_build_server,
        DOC_BUILD,
        BUILD,
        LAN_DEMOS | {DEV},
    ),
    Check("net.port", "Game port is free", check_port, DOC_LAN, (), LAN_DEMOS | {DEV}),
    Check(
        "net.firewall",
        "Firewall lets the game in",
        check_firewall,
        DOC_LAN,
        SHELL,
        LAN_DEMOS | {DEV},
    ),
    Check(
        "net.lan", "Address to give a friend", check_lan, DOC_IP, (), LAN_DEMOS | {DEV}
    ),
    Check(
        "platform.url",
        "Platform address",
        check_platform_url,
        DOC_PLATFORM,
        (),
        frozenset({"platform", DEV}),
    ),
    Check(
        "platform.health",
        "Platform is running",
        check_platform_health,
        DOC_PLATFORM,
        ("platform.url",),
        frozenset({"platform", DEV}),
    ),
    Check(
        "platform.ready",
        "Platform database answers",
        check_platform_ready,
        DOC_PLATFORM_TROUBLE,
        PLATFORM_UP,
        frozenset({"platform", DEV}),
    ),
    Check(
        "platform.migrations",
        "Platform accounts work",
        check_platform_migrations,
        DOC_PLATFORM_TROUBLE,
        ("platform.ready",),
        frozenset({"platform", DEV}),
    ),
    Check(
        "platform.http",
        "Plain http allowed by this build",
        check_http_dev,
        DOC_PLATFORM_TROUBLE,
        ("platform.url", "build.present"),
        frozenset({"platform", DEV}),
    ),
    Check(
        "platform.loopback",
        "Platform address works across computers",
        check_loopback,
        DOC_REMOTE,
        ("platform.url",),
        frozenset({"platform", DEV}),
    ),
    Check(
        "platform.serverkey.set",
        "Game server key is set",
        check_key_set,
        DOC_KEY,
        (),
        frozenset({"platform", DEV}),
    ),
    Check(
        "platform.login",
        "Real sign-in (--login)",
        check_login,
        DOC_PLATFORM,
        ("platform.migrations",),
        frozenset({"platform", DEV}),
    ),
    Check(
        "platform.serverkey.accepted",
        "Platform accepts the server key",
        check_key_accepted,
        DOC_KEY,
        PLATFORM_UP,
        frozenset({"platform", DEV}),
    ),
    Check(
        "dev.dotnet",
        "dotnet for the plain C# tests (developers)",
        check_dotnet,
        DOC_DEV,
        (),
        frozenset({DEV}),
    ),
    Check("logs.locations", "Where the logs are", check_logs, DOC_LOGS, (), ALL),
]
