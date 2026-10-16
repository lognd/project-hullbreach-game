"""The one place that knows which operating system we are on and where things live."""

from __future__ import annotations

import json
import logging
import os
import platform
import sys
from pathlib import Path
from typing import Literal

from pydantic import BaseModel

logger = logging.getLogger(__name__)

UNITY_VERSION = "6000.0.43f1"
UNITY_CHANGESET = "97272b72f107"
COMPANY = "Company of Theseus"
PRODUCT = "Project Hullbreach"
EDITOR_ENV = "HULLBREACH_UNITY_EDITOR"

HostKind = Literal["windows", "macos", "linux", "wsl"]
BuildTarget = Literal["windows", "macos", "linux"]


class ModuleSpec(BaseModel):
    """One Unity Hub module: how Hub names it and how to spot it on disk."""

    hub_id: str
    title: str
    engine_dir: str
    variation_prefix: str
    needed_for: str


class Host(BaseModel):
    """Everything OS-specific the doctor and play scripts need, resolved once."""

    kind: HostKind
    os_name: str
    shell: str
    home: Path
    target: BuildTarget
    hub_candidates: list[Path]
    editor_roots: list[Path]
    editor_override: Path | None = None
    player_log: Path
    editor_log: Path
    doctor_cmd: str
    play_cmd: str
    client_exe: str
    server_exe: str
    # Visual Studio's locator; only Windows IL2CPP builds need C++ tools.
    vswhere: Path | None = None

    @property
    def is_wsl(self) -> bool:
        """WSL runs Linux Python, but Unity lives on the Windows side."""
        return self.kind == "wsl"

    def editor_path(self, root: Path) -> Path:
        """The Unity executable inside one Hub editor folder."""
        version_dir = root / UNITY_VERSION
        if self.target == "macos":
            return version_dir / "Unity.app" / "Contents" / "MacOS" / "Unity"
        exe = "Unity.exe" if self.target == "windows" else "Unity"
        return version_dir / "Editor" / exe

    def playback_engines(self, editor: Path) -> Path:
        """Where Unity keeps installed build modules, given the editor executable."""
        if self.target == "macos":
            return editor.parents[3] / "PlaybackEngines"
        return editor.parent / "Data" / "PlaybackEngines"

    def modules(self) -> list[ModuleSpec]:
        """The Hub modules that matter on this computer, most important first."""
        return MODULES[self.target]

    def player_log_for(self, name: str) -> Path:
        """A per-process log path under the default Player.log folder."""
        return self.player_log.parent / name

    def firewall(self, port: int) -> FirewallPlan:
        """How to check and open the game's UDP port on this OS's firewall."""
        return firewall_plan(self.kind, port)


class FirewallPlan(BaseModel):
    """One OS's firewall: a read-only status command and the fix to offer."""

    # Read-only commands; any output containing an ok marker means "fine".
    status_argvs: list[list[str]]
    # Text in the status output meaning "a rule exists / firewall is off".
    ok_markers: list[str]
    # A rule that blocks the game (someone clicked "Cancel" on the prompt).
    block_pattern: str = ""
    fix_argv: list[str]
    fix_text: str
    prompt_help: str


def rule_name(port: int) -> str:
    """The Windows firewall rule name the doctor creates and looks for."""
    return f"Project Hullbreach UDP {port}"


def prompt_rule_name() -> str:
    """The rule name Windows gives the game when its firewall prompt is answered."""
    return f"{PRODUCT.lower()}.exe"


def firewall_plan(kind: HostKind, port: int) -> FirewallPlan:
    """The firewall commands for one kind of computer."""
    if kind == "windows":
        name = rule_name(port)
        netsh = (
            f'advfirewall firewall add rule name="{name}" dir=in action=allow '
            f"protocol=UDP localport={port}"
        )
        return FirewallPlan(
            # Our rule, or the ones Windows names after the game's .exe when
            # someone answers its "allow access" prompt (Allow or Cancel).
            status_argvs=[
                ["netsh", "advfirewall", "firewall", "show", "rule", f"name={name}"],
                [
                    "netsh",
                    "advfirewall",
                    "firewall",
                    "show",
                    "rule",
                    f"name={prompt_rule_name()}",
                ],
            ],
            ok_markers=["Rule Name:"],
            block_pattern=r"Action:\s+Block",
            fix_argv=[
                "powershell",
                "-NoProfile",
                "-Command",
                f"Start-Process netsh -Verb RunAs -Wait -ArgumentList '{netsh}'",
            ],
            fix_text=f"netsh {netsh}",
            prompt_help=(
                "The first time the game opens a port, Windows Defender Firewall "
                "shows 'Windows Defender Firewall has blocked some features of "
                "this app'. Tick 'Private networks' and click 'Allow access'."
            ),
        )
    if kind == "macos":
        return FirewallPlan(
            status_argvs=[
                ["/usr/libexec/ApplicationFirewall/socketfilterfw", "--getglobalstate"]
            ],
            ok_markers=["disabled", "State = 0"],
            fix_argv=[],
            fix_text="System Settings -> Network -> Firewall -> Options -> "
            "add Project Hullbreach -> Allow incoming connections",
            prompt_help=(
                "macOS asks 'Do you want the application Project Hullbreach to "
                "accept incoming network connections?'. Click 'Allow'."
            ),
        )
    return FirewallPlan(
        status_argvs=[["ufw", "status"]],
        ok_markers=["inactive", f"{port}/udp"],
        fix_argv=[],
        fix_text=f"sudo ufw allow {port}/udp   (or, with firewalld: "
        f"sudo firewall-cmd --permanent --add-port={port}/udp && "
        "sudo firewall-cmd --reload)",
        prompt_help="Linux does not ask; open the port with the command above.",
    )


MODULES: dict[BuildTarget, list[ModuleSpec]] = {
    "windows": [
        ModuleSpec(
            hub_id="windows-mono",
            title="Windows Build Support (Mono)",
            engine_dir="windowsstandalonesupport",
            variation_prefix="win64_player_",
            needed_for="building the game for Windows (required)",
        ),
        ModuleSpec(
            hub_id="windows-il2cpp",
            title="Windows Build Support (IL2CPP)",
            engine_dir="windowsstandalonesupport",
            variation_prefix="win64_player_development_il2cpp",
            needed_for="release-quality builds (optional; Mono works for demos)",
        ),
        ModuleSpec(
            hub_id="windows-server",
            title="Windows Dedicated Server Build Support",
            engine_dir="windowsstandalonesupport",
            variation_prefix="win64_server_",
            needed_for="a real dedicated-server build (optional; the game "
            "build can run as a server)",
        ),
        ModuleSpec(
            hub_id="linux-server",
            title="Linux Dedicated Server Build Support",
            engine_dir="linuxstandalonesupport",
            variation_prefix="linux64_server_",
            needed_for="Guide 5, a server on a Linux machine (optional)",
        ),
    ],
    "macos": [
        ModuleSpec(
            hub_id="mac-mono",
            title="Mac Build Support (Mono)",
            engine_dir="macstandalonesupport",
            variation_prefix="macos",
            needed_for="building the game for macOS (required)",
        ),
        ModuleSpec(
            hub_id="mac-server",
            title="Mac Dedicated Server Build Support",
            engine_dir="macstandalonesupport",
            variation_prefix="macos_x64arm64_server_",
            needed_for="a real dedicated-server build (optional)",
        ),
        ModuleSpec(
            hub_id="linux-server",
            title="Linux Dedicated Server Build Support",
            engine_dir="linuxstandalonesupport",
            variation_prefix="linux64_server_",
            needed_for="Guide 5, a server on a Linux machine (optional)",
        ),
    ],
    "linux": [
        ModuleSpec(
            hub_id="linux-mono",
            title="Linux Build Support (Mono)",
            engine_dir="linuxstandalonesupport",
            variation_prefix="linux64_player_",
            needed_for="building the game for Linux (required)",
        ),
        ModuleSpec(
            hub_id="linux-server",
            title="Linux Dedicated Server Build Support",
            engine_dir="linuxstandalonesupport",
            variation_prefix="linux64_server_",
            needed_for="Guide 5, a dedicated server build (optional)",
        ),
    ],
}


def _is_wsl(release: str, env: dict[str, str]) -> bool:
    return "microsoft" in release.lower() or "WSL_DISTRO_NAME" in env


def _secondary_root(path: Path) -> list[Path]:
    """Unity Hub's custom install folder, if the user picked one."""
    try:
        raw = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return []
    if isinstance(raw, str) and raw.strip():
        logger.info("Unity Hub secondary install path: %s", raw)
        return [Path(raw)]
    return []


def detect_host(
    env: dict[str, str] | None = None,
    system: str | None = None,
    release: str | None = None,
    home: Path | None = None,
) -> Host:
    """Work out the Host once; every other module asks it instead of sys.platform."""
    env = dict(os.environ) if env is None else env
    system = system or platform.system()
    release = release or platform.release()
    home = home or Path.home()
    override = Path(env[EDITOR_ENV]) if env.get(EDITOR_ENV) else None
    logs_tail = Path(COMPANY) / PRODUCT / "Player.log"

    if system == "Windows":
        appdata = Path(env.get("APPDATA", str(home / "AppData" / "Roaming")))
        local = Path(env.get("LOCALAPPDATA", str(home / "AppData" / "Local")))
        pf = Path(env.get("ProgramFiles", r"C:\Program Files"))
        pf86 = Path(env.get("ProgramFiles(x86)", r"C:\Program Files (x86)"))
        host = Host(
            kind="windows",
            os_name=f"Windows {release} ({platform.machine()})",
            shell="cmd.exe" if "PROMPT" in env else env.get("ComSpec", "cmd.exe"),
            home=home,
            target="windows",
            hub_candidates=[
                pf / "Unity Hub" / "Unity Hub.exe",
                pf86 / "Unity Hub" / "Unity Hub.exe",
            ],
            editor_roots=[pf / "Unity" / "Hub" / "Editor"]
            + _secondary_root(appdata / "UnityHub" / "secondaryInstallPath.json"),
            editor_override=override,
            player_log=home / "AppData" / "LocalLow" / logs_tail,
            editor_log=local / "Unity" / "Editor" / "Editor.log",
            doctor_cmd=".\\doctor.cmd",
            play_cmd=".\\play.cmd",
            client_exe="Project Hullbreach.exe",
            server_exe="HullbreachServer.exe",
            vswhere=pf86 / "Microsoft Visual Studio" / "Installer" / "vswhere.exe",
        )
    elif system == "Darwin":
        support = home / "Library" / "Application Support" / "UnityHub"
        host = Host(
            kind="macos",
            os_name=f"macOS {platform.mac_ver()[0] or release}",
            shell=env.get("SHELL", "sh"),
            home=home,
            target="macos",
            hub_candidates=[Path("/Applications/Unity Hub.app")],
            editor_roots=[Path("/Applications/Unity/Hub/Editor")]
            + _secondary_root(support / "secondaryInstallPath.json"),
            editor_override=override,
            player_log=home / "Library" / "Logs" / logs_tail,
            editor_log=home / "Library" / "Logs" / "Unity" / "Editor.log",
            doctor_cmd="./doctor.sh",
            play_cmd="./play.sh",
            client_exe="Project Hullbreach.app",
            server_exe="Project Hullbreach.app",
        )
    else:
        wsl = _is_wsl(release, env)
        config = home / ".config"
        host = Host(
            kind="wsl" if wsl else "linux",
            os_name=("WSL " if wsl else "Linux ") + release,
            shell=env.get("SHELL", "sh"),
            home=home,
            target="linux",
            hub_candidates=[
                Path("/opt/unityhub/unityhub"),
                Path("/usr/bin/unityhub"),
                home / "Applications" / "UnityHub.AppImage",
            ],
            editor_roots=[home / "Unity" / "Hub" / "Editor"]
            + _secondary_root(config / "UnityHub" / "secondaryInstallPath.json"),
            editor_override=override,
            player_log=config / "unity3d" / logs_tail,
            editor_log=config / "unity3d" / "Editor.log",
            doctor_cmd="./doctor.sh",
            play_cmd="./play.sh",
            client_exe="ProjectHullbreach.x86_64",
            server_exe="HullbreachServer.x86_64",
        )
    logger.info("host: %s (%s), shell %s", host.kind, host.os_name, host.shell)
    return host


def python_summary() -> str:
    """Which Python runs the tools, for the doctor's first line and bug reports."""
    return f"Python {sys.version.split()[0]} ({platform.machine()})"


def set_env_line(host: Host, name: str, value: str) -> str:
    """The exact line to set an environment variable in this computer's shell."""
    if host.kind == "windows":
        return f"set {name}={value}"
    return f"export {name}={value}"
