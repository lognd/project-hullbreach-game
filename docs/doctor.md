# The doctor and the play scripts

Who this is for: anyone who has to get the game running, from a first-time
user to the person reviewing a change to `tools/hbtools`. The beginner
walkthrough is in the README ([Step 1: run the
doctor](../README.md#step-1-run-the-doctor)); this page is the reference.

## What is there

| File | What it does |
| --- | --- |
| `doctor.cmd` / `doctor.sh` | Checks this computer for every demo and explains each fix. |
| `play.cmd` / `play.sh` | Builds the game and starts it in each mode (`play --help` lists them). |
| `tools/hbtools/run.cmd` / `run.sh` | The shared bootstrap: finds `uv`, offers to install it, runs the tool. |
| `tools/hbtools/src/hbtools/` | The one Python implementation behind all four wrappers. |
| `Assets/Editor/Hullbreach.Editor/HullbreachBuild.cs` | The Unity build entry point the scripts call headless. |

Windows users double-click `doctor.cmd` (the window stays open at the end)
or type `doctor` / `play ...` in a terminal in the game folder. macOS and
Linux users type `./doctor.sh` and `./play.sh ...`.

## Why it is built this way

- **One implementation, not one per OS.** The checks, the fix texts and the
  launch logic live once, in Python (`tools/hbtools`). A batch file and a
  shell script of the same size would drift apart within a sprint.
- **uv as the only prerequisite.** `uv` installs the right Python and the
  pinned dependencies (pydantic, typani) on first run, so a beginner never
  installs Python or a virtual environment by hand. The wrappers are tiny:
  find `uv` (on `PATH`, `~/.local/bin`, `~/.cargo/bin`), and if it is
  missing explain what it is and ask `Install uv now? [y/N]` before running
  the official installer (`install.ps1` on Windows, `install.sh`
  elsewhere). Saying no prints the manual install link and stops.
- **OS differences in one place.** `host.py` detects the operating system
  once (`detect_host`) and everything else asks the `Host` object (where
  Unity Hub lives, the player executable name, how to set an environment
  variable, firewall commands, log paths).
- **Everything that touches the world is injected.** Subprocesses
  (`proc.Runner`), HTTP (`net.Http`), sockets (`net.Sockets`), prompts
  (`prompt.Prompter`) and the clock are passed in, so the tests run every
  check and every play command against fakes.
- **Fallible operations return typani `Result`.** Exceptions are only for
  programmer bugs; the doctor never crashes on a missing tool, it reports
  it.

## Secrets

The game server key (`HULLBREACH_PLATFORM_SERVER_KEY`), platform tokens
(`HULLBREACH_PLATFORM_TOKEN`) and passwords (`HULLBREACH_PLATFORM_PASSWORD`)
are only ever read from the environment or typed at a hidden prompt.
There is no command-line option for any of them (a test enforces it), they
are never written to a file, and the tool log records only whether a secret
is set. `doctor --collect` redacts known secret values and anything shaped
like a bearer token, an `X-Server-Key` header or a `*_KEY=` assignment
before it zips the logs.

## The checklist

`doctor` runs every check top to bottom; `doctor --demo guest|online|platform`
runs only what that demo needs. Each line is `[PASS]`, `[WARN]`, `[FAIL]`,
`[ -- ]` (waits for an earlier failure it depends on) or `[SKIP]`
(opt-in, not requested). A failure prints numbered fix steps and the README
section to read; checks that depend on it show as waiting, so the whole
list is always visible. When nothing fails the doctor prints the exact
commands for the demo. Exit code: 0 when nothing failed, 1 otherwise.

| Id | Checks | Demos | README |
| --- | --- | --- | --- |
| `os.shell` | Operating system, shell, WSL (Unity cannot use a project inside WSL) | all | [Step 1](../README.md#step-1-run-the-doctor) |
| `git.installed` | Git on `PATH` | all | [0.2](../README.md#02-install-git) |
| `repo.clone` | The folder is a git clone of the game | all | [0.3](../README.md#03-download-clone-the-game) |
| `repo.uptodate` | Not behind `origin/main` (explains `git pull`) | all | [0.3](../README.md#03-download-clone-the-game) |
| `repo.path` | Not a network or `\\wsl` path; warns about spaces, non-English letters, OneDrive/Dropbox, very long paths | all | [0.3](../README.md#03-download-clone-the-game) |
| `project.files` | `ProjectSettings`, the pinned editor version, scenes, services and sign-in facts | all | [Setup](../README.md#setup-and-unity) |
| `git.lfs` | Git LFS present if the repo uses it (it does not today) | all | [0.3](../README.md#03-download-clone-the-game) |
| `unity.hub` | Unity Hub installed | all | [0.4](../README.md#04-install-unity-hub-and-the-exact-unity-version) |
| `unity.editor` | Exactly Unity 6000.0.43f1 installed | all | [0.4](../README.md#04-install-unity-hub-and-the-exact-unity-version) |
| `unity.modules` | Build modules for this OS, IL2CPP, Linux Dedicated Server; prints the Unity Hub command to add them | all | [0.4](../README.md#04-install-unity-hub-and-the-exact-unity-version) |
| `unity.cpp` | C++ tools for IL2CPP (else builds fall back to Mono) | all | [Setup](../README.md#setup-and-unity) |
| `unity.running` | The editor does not have this project open (headless Unity cannot share it) | all | [Setup](../README.md#setup-and-unity) |
| `unity.license` | `--deep`: a headless Unity starts and is licensed | all | [Setup](../README.md#setup-and-unity) |
| `unity.compile` | `--deep`: the project compiles with zero errors | all | [Setup](../README.md#setup-and-unity) |
| `services.linked` | `cloudProjectId` set (Unity online services linked) | online | [2.1](../README.md#21-link-the-project-to-unitys-online-services-once-per-computer) |
| `services.reachable` | HTTPS to Unity Authentication, Lobby and Relay | online | [Relay lobby](../README.md#relay-lobby) |
| `signin.prefab` | `SignInCanvas.prefab` exists and `LobbyScene` uses it | platform | [4.1](../README.md#41-the-sign-in-screen-is-already-in-the-project) |
| `build.present` | A build made by `play build` exists | all | [Build](../README.md#build-the-game-with-one-command) |
| `build.fresh` | The build's commit matches `HEAD` | all | [Build](../README.md#build-the-game-with-one-command) |
| `build.scenes` | `LobbyScene` first, `MultiplayerGame` included | all | [Build](../README.md#build-the-game-with-one-command) |
| `build.server` | A dedicated server build, or the game build as a headless server | guest, platform | [Build](../README.md#build-the-game-with-one-command) |
| `net.port` | UDP game port (7979, or `--port`) is free | guest, platform | [Direct / LAN](../README.md#direct--lan) |
| `net.firewall` | Firewall allows the game port; offers the exact rule (Windows `netsh`, asks first); flags the Block rule `project hullbreach.exe` that a cancelled Windows prompt leaves | guest, platform | [Direct / LAN](../README.md#direct--lan) |
| `net.lan` | This computer's LAN addresses and which one to give a friend | guest, platform | [3.2](../README.md#32-find-the-hosts-ip-address) |
| `platform.url` | The platform address is a valid URL | platform | [4.2](../README.md#42-start-the-platform-and-make-an-account) |
| `platform.health` | `GET /api/v1/health` answers, with the platform version | platform | [4.2](../README.md#42-start-the-platform-and-make-an-account) |
| `platform.ready` | `GET /api/v1/ready`: the database answers | platform | [Platform](../README.md#platform-sign-in-guide-4) |
| `platform.migrations` | The account tables exist (a probe login gets 401, not 500) | platform | [Platform](../README.md#platform-sign-in-guide-4) |
| `platform.http` | A plain `http://` platform needs a development build | platform | [Platform](../README.md#platform-sign-in-guide-4) |
| `platform.loopback` | `127.0.0.1` only works when everything runs on one computer | platform | [4.6](../README.md#46-players-on-another-computer) |
| `platform.serverkey.set` | `HULLBREACH_PLATFORM_SERVER_KEY` is set (value never shown) | platform | [4.5](../README.md#45-demo-with-sign-in-required) |
| `platform.login` | `--login`: a real sign-in with a username and hidden password | platform | [4.2](../README.md#42-start-the-platform-and-make-an-account) |
| `platform.serverkey.accepted` | `/api/v1/auth/session` accepts the key; tells a key mismatch from a bad token | platform | [4.5](../README.md#45-demo-with-sign-in-required) |
| `dev.dotnet` | `dotnet` for the plain C# tests (developers only) | full run | [Checks](../README.md#checks) |
| `logs.locations` | Where every log is on this computer | all | [Logs](../README.md#where-the-logs-are) |

`platform.serverkey.accepted` works against both platform behaviours: a
platform that does not check keys yet answers 401 "not authenticated" for
a bogus token whatever the key (reported as "does not check keys"); a
platform that checks keys answers "server key missing", "invalid server
key" (a KEY MISMATCH, with the fix) or "server key not configured" before
it looks at the token. With `--login` the check uses the fresh real token,
so a 200 proves the key and the token end to end.

Options: `--deep` (license and compile through headless Unity, several
minutes), `--login`, `--fix-firewall` (add the rule without asking),
`--collect` (zip to `Logs/hullbreach-doctor-<time>.zip`),
`--platform-url`, `--port`, `--verbose`.

## Building

`play build` (client, the default) and `play build server` run Unity
headless with `-executeMethod Hullbreach.Editor.HullbreachBuild.BuildFromCommandLine`.
The scripts refuse to start while the editor has the project open, and
print the Unity log path while it works.

| Argument | Meaning |
| --- | --- |
| `-hullbreachBuildTarget windows\|linux\|macos` | Target OS (default: the computer's own). |
| `-hullbreachBuildKind client\|server` | Game or dedicated server (needs the Dedicated Server module). |
| `-hullbreachBuildOutput <folder>` | Default `Builds/<target>-<kind>/`. |
| `-hullbreachBuildCommit <sha>` | Recorded in the stamp so the doctor can tell a stale build. |
| `-hullbreachBuildRelease` | Non-development build (needs an `https://` platform). |
| `-hullbreachBuildBackend auto\|il2cpp\|mono` | `auto` uses IL2CPP when the module and C++ tools exist, else Mono. |

The scene list always comes from `EditorBuildSettings` (LobbyScene first).
Every build writes `hullbreach-build.json` next to the executable
(target, kind, commit, development, Unity version, time, backend, scenes);
the doctor's `build.*` checks read it. Unity prints
`HullbreachBuild: OK <target> <kind> -> <path>` or
`HullbreachBuild: FAILED <reason>`; a missing module is reported by name
with the Unity Hub command to add it. The same two builds are under the
editor menu **Hullbreach -> Build**.

`Builds/` is gitignored; never commit a build.

## Play commands

| Command | What it starts | The proof it waits for |
| --- | --- | --- |
| `play online` | The game at the Relay lobby | (interactive) |
| `play online --profile <name>` | The same, as another Unity player (second copy on one computer) | (interactive) |
| `play online --headless` | Unity Services smoke test (`HULLBREACH_ONLINE_SMOKE=1`): anonymous sign-in, private lobby with a Relay allocation, leave | `Hullbreach online: smoke test PASSED` |
| `play host` | Host: runs the match and plays (`-hullbreachDirect`) | `replicated ships: N` with N at least 1 |
| `play join <ip>` | Joins a host or server (`-hullbreachConnect <ip>`) | `replicated ships: N` with N at least 1; `server rejected this client` is a FAIL even after connecting |
| `play two-local` | A host and a client on this computer | both reach the match |
| `play server` | Headless server, no player of its own (`-hullbreachServer`) | runs until Ctrl+C or `--seconds` |
| `play host --platform [--require-auth]` | Host that signs in and checks players with the platform | |
| `play join <ip> --platform` | A player who signs in | as `play join`, after `signed in ... as '<name>'` |
| `play server --platform [--require-auth]` | Server that verifies (or requires) sign-in; checks its key first | |
| `play build [server]` | See [Building](#building) | `HullbreachBuild: OK` |
| `play logs [--follow]` | The newest game log | |

Common options: `--port <n>` (both sides must match), `--platform-url
<url>`, `--headless` (no window), `--seconds <n>` (stop after n seconds:
used by tests and demos). Each player the scripts start writes its own
log to `Logs/play/<name>.log` (overwritten each run; build logs are
`build-<kind>.log`) and every `Hullbreach` line in it is echoed with a
`[host]`, `[client]` or `[server]` prefix.

**Two copies on one computer.** Only the host listens on the port and a
client binds an ephemeral one, so `two-local` needs no second port; each
copy gets its own `-logFile` so the logs do not overwrite each other.
The Relay lobby is different: Unity remembers one anonymous player per
computer, so a second copy must use another Unity Services profile:
`play online --profile friend2` (the game reads `-hullbreachProfile`).

**Platform sign-in without clicking.** A headless player cannot click the
sign-in form, so `--platform` signs in for it: it uses
`HULLBREACH_PLATFORM_TOKEN` (+ `HULLBREACH_PLATFORM_USERNAME`) if set,
otherwise asks for the username and a hidden password (or reads
`HULLBREACH_PLATFORM_USERNAME` / `HULLBREACH_PLATFORM_PASSWORD`), logs in
through the platform API, and hands the token to the game in its
environment only. The game logs `Hullbreach platform: signed in from
HULLBREACH_PLATFORM_TOKEN as '<name>' (demo/testing aid).` This is a demo
and testing aid; players normally use the sign-in screen.

**Server key check.** `server --platform` and `host --platform
--require-auth` ask the platform about `HULLBREACH_PLATFORM_SERVER_KEY`
before starting and stop with `KEY MISMATCH` and the fix if the platform
refuses it. `--skip-key-check` starts anyway so you can see the server's
own message (`platform refused this game server's key (...)`).

## Logs

The tools log to `Logs/hbtools/hbtools.log` (rotating; no secret values).
Player logs from the scripts are in `Logs/play/`. Unity's own logs are
listed by the `logs.locations` check and in the README ([Where the logs
are](../README.md#where-the-logs-are)).

## Tests

```
cd tools/hbtools
uv run pytest -q
uv run ruff check . && uv run ruff format --check .
uv run python -m typani.lint src
```

Every check and command runs against fakes (`tests/unit/conftest.py`): a
scripted subprocess runner that also writes fake Unity/player logs, a
canned HTTP client, fake sockets, a scripted prompter and a monkeypatched
environment. `test_readme_anchors.py` proves every README link the doctor
prints exists.
