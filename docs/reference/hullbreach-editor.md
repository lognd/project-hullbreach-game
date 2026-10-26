# Hullbreach.Editor reference

Per-type reference for `Assets/Editor/Hullbreach.Editor`, linked from the code by
`// frob:doc docs/reference/hullbreach-editor.md#<anchor>`. One heading per
public type; each heading carries the `describes:` lines for that
type and its public members. Architecture-level context lives in
[architecture.md](../architecture.md).

### HullbreachBuild

<!-- describes: Assets/Editor/Hullbreach.Editor/HullbreachBuild.cs::HullbreachBuild -->
<!-- describes: Assets/Editor/Hullbreach.Editor/HullbreachBuild.cs::HullbreachBuild.LobbyScene -->
<!-- describes: Assets/Editor/Hullbreach.Editor/HullbreachBuild.cs::HullbreachBuild.GameScene -->
<!-- describes: Assets/Editor/Hullbreach.Editor/HullbreachBuild.cs::HullbreachBuild.StampFile -->
<!-- describes: Assets/Editor/Hullbreach.Editor/HullbreachBuild.cs::HullbreachBuild.ResultPrefix -->
<!-- describes: Assets/Editor/Hullbreach.Editor/HullbreachBuild.cs::HullbreachBuild.BuildClientMenu -->
<!-- describes: Assets/Editor/Hullbreach.Editor/HullbreachBuild.cs::HullbreachBuild.BuildServerMenu -->
<!-- describes: Assets/Editor/Hullbreach.Editor/HullbreachBuild.cs::HullbreachBuild.BuildFromCommandLine -->
<!-- describes: Assets/Editor/Hullbreach.Editor/HullbreachBuild.cs::HullbreachBuild.ScenesFor -->

The one build entry point, so nobody edits Build Profiles by hand. It is
reached three ways: the `Hullbreach > Build` menu, `play build` (see
[doctor.md](../doctor.md#building)), and CI through
`-executeMethod Hullbreach.Editor.HullbreachBuild.BuildFromCommandLine`.

Command-line arguments (all optional):

| Argument | Values | Default |
|---|---|---|
| `-hullbreachBuildTarget` | `windows`, `linux`, `macos` | the editor's own OS |
| `-hullbreachBuildKind` | `client`, `server` | `client` |
| `-hullbreachBuildOutput` | a folder | `Builds/<target>-<kind>/` |
| `-hullbreachBuildCommit` | the git commit being built | empty |
| `-hullbreachBuildRelease` | (flag) non-development build | development build |
| `-hullbreachBuildBackend` | `auto`, `il2cpp`, `mono` | `auto` |

Scene lists (`ScenesFor`): a client ships `LobbyScene` first, then
`MultiplayerGame`, then any other scene enabled in Build Settings; a
server ships only `MultiplayerGame`. Direct and server launches load
`MultiplayerGame` themselves (S47-5), so the order is never a workaround.

Backend: the project is set to IL2CPP. With `auto`, a target whose IL2CPP
variation is not installed in the editor is built with Mono for that one
build (logged as `HullbreachBuild: IL2CPP is not installed ...`), and the
committed setting is restored afterwards. A server build needs the
`<OS> Dedicated Server Build Support` module; without it the build fails
with the exact Unity Hub module name.

Every build writes `hullbreach-build.json` next to the executable
(`target`, `kind`, `commit`, `development`, `unity`, `builtUtc`,
`executable`, `backend`, `scenes`); the doctor reads it to tell a stale
build from a fresh one. The last log line is always
`HullbreachBuild: OK <target> <kind> -> <path>` or
`HullbreachBuild: FAILED <reason>`, and the editor exits 0 or 1.
