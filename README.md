# Project Hullbreach: Game

This is the game half of Hullbreach: the Unity client and the dedicated
server, built from one project. Players build a spaceship out of blocks
and try to breach each other's hull. Accounts, ELO, match history, and the
store are in the platform repo; this one talks to that API over HTTPS.

Company of Theseus, UF CEN3031, fall 2026. All rights reserved, see
[LICENSE](LICENSE).

We started from Unity's Competitive Action Multiplayer template, so right
now it is a first-person shooter with rockets. That is the scaffolding,
not the game. Netcode for Entities, the server-authoritative setup, the
menus, and the dedicated-server build are what we are keeping; the guns
go.

## What is in the box

- **Unity 6000.0.43f1** (Unity 6). Everyone uses exactly this version.
  `ProjectSettings/ProjectVersion.txt` pins it and CI checks it.
- **Netcode for Entities** for the multiplayer sim. The server world is
  authoritative; clients send input and get ghosts back.
- **Universal Render Pipeline**, the new **Input System**, **UI Toolkit**
  for menus, and **Multiplayer Play Mode** so one editor can run a host
  and a couple of clients at once.
- **Unity Gaming Services** hooks (sessions, relay) from the template.
  Whether we keep them or run our own relay is an open question in
  `TODO.md`.

## Where things live

```
Assets/
  Scenes/                MainMenu, GameScene, GameResources, ServerScene
  Scripts/
    Managers/            GameBootstrap, GameManager, connection + scene loading
    Gameplay/            Character, Player, Weapons, Camera, Client, Server
    DedicatedServer/     server bootstrap and build options
    Input/               Input System actions and wrappers
    UI/                  menus, HUD, mobile touch controls
  Prefabs/               ghosts (networked prefabs), VFX, environment
  UIToolkit/             UXML/USS for every screen
  Art/                   textures, materials, models
Packages/manifest.json   package versions (edit through the Package Manager)
ProjectSettings/         editor and player settings, all committed
scripts/                 check_unity_tree.sh, what CI runs
```

## Getting started with Unity

1. **Install Unity Hub** from https://unity.com/download. Make a Unity
   account if you do not have one; the Personal license is free and is
   what we use.
2. **Install the editor.** In the Hub: Installs -> Install Editor ->
   Archive -> pick **6000.0.43f1** exactly. Not the newest 6000.x, this
   one. During the install tick these modules:
   - Microsoft Visual Studio Community (Windows) or leave it off and use
     VS Code / Rider, your call
   - **Linux Dedicated Server Build Support** (the server we deploy)
   - Windows Build Support (IL2CPP) if you are on Windows, Mac Build
     Support if you are on a Mac
   It is a big download. Start it before you clone.
3. **Clone the repo** somewhere without spaces in the path:
   ```
   git clone https://github.com/lognd/project-hullbreach-game.git
   ```
   Send me your GitHub username first if you are not a collaborator yet.
   Git will want a personal access token instead of your password; the
   platform README walks through making one.
4. **Open the project.** Hub -> Projects -> Add -> Add project from disk
   -> pick the cloned folder. The first open imports everything and
   builds the `Library/` folder. That takes a while, five to fifteen
   minutes depending on your machine. It is normal. Do not commit
   `Library/`; it is ignored.
5. **Play it.** Open `Assets/Scenes/MainMenu.unity` (double-click in the
   Project window), press Play, and you get the menu. Host a game and you
   are running server and client in the editor. For a second player, open
   Window -> Multiplayer -> Multiplayer Play Mode, enable a virtual player,
   and it joins as a client.
6. **Pick an editor for code.** Edit -> Preferences -> External Tools ->
   External Script Editor. VS Code with the "Unity" extension works, and
   `.vscode/` already has the attach-debugger config. Rider is great if
   you have the student license. Whatever you pick, let Unity generate
   the `.csproj` files (they are ignored by git).

### Building the dedicated server

File -> Build Profiles -> Linux Server (or Platforms -> Dedicated Server
-> Linux). Build to a folder outside the repo. The result is a headless
binary that hosts a match; `ServerScene` is its entry. This is what we
will deploy; a CI build for it is on the list in `TODO.md`.

### Things that bite people

- **"This project was saved with a different editor version"**: you
  installed the wrong 6000.x. Install 6000.0.43f1 and open with that.
  Never click "Continue" and upgrade the project on your own.
- **Missing `.meta` files / broken references after a pull**: someone
  moved an asset outside Unity. Move it back inside Unity so the meta
  travels with it. CI catches missing or orphaned metas.
- **Scene or prefab merge conflict**: do not hand-merge YAML. Take one
  side (`git checkout --theirs path/to/Scene.unity`) and redo the smaller
  change in the editor. Say in chat which scene you are working in so
  this does not happen.
- **Everything is pink**: URP shaders did not compile yet or the project
  opened with the wrong render pipeline asset. Wait for import to finish,
  then Edit -> Render Pipeline -> URP -> Upgrade Project Materials if it
  persists.
- **Slow on WSL**: do not put the project inside WSL. Unity is a Windows
  or Mac app here; clone with Git for Windows (or from your Mac terminal).
  Keep `core.autocrlf=true` on Windows; `.gitattributes` normalizes line
  endings on commit.

## Checks

```
scripts/check_unity_tree.sh
```

That is what CI runs on every PR: meta files pair with assets, nothing
generated is committed, the editor version is still pinned, manifests
parse, nothing looks like a credential. It needs bash and python3, so on
Windows run it from Git Bash. Editor tests and a headless server build
in CI are not there yet; see `TODO.md`. frob does not gate this repo yet
either (it can parse C# but has no check stage for it), so there are no
tickets here, just `TODO.md`.

## Making a change

Same as platform. `main` is protected; every change is a branch, a PR,
green CI, one approval, merge.

```
git switch main && git pull
git switch -c feat/ship-builder          # feat/ fix/ docs/ chore/
# ...work in Unity, save the scene, scripts/check_unity_tree.sh...
git add -A
git commit -m "feat: place hull blocks on a grid"
git push -u origin feat/ship-builder
```

Commit messages are [Conventional Commits](https://www.conventionalcommits.org/).
Always `git add` the `.meta` next to any new asset. Read
[CONTRIBUTING.md](CONTRIBUTING.md) once; the Unity-specific rules there
are the ones that save us from a bad week.

[CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) has the expectations and
[SECURITY.md](SECURITY.md) how to report a vulnerability.
