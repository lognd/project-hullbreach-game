# Testing

## Running the tests without Unity

```
tools/plaincs/run_tests.sh
```

This is what CI's `plaincs` job runs on every PR
(`.github/workflows/ci.yml`), no Unity license involved. It:

1. Calls `tools/plaincs/fetch_deps.sh`, which shallow-clones
   `Unity-Technologies/Unity.Mathematics` (tag `1.2.5`) into
   `tools/plaincs/.deps/um` if not already present. `Unity.Mathematics`
   is pure C# with no engine dependency, so it compiles standalone.
2. Resolves a `dotnet` executable, falling back to the Windows SDK path
   under WSL (`/mnt/c/Program Files/dotnet/dotnet.exe`) if `dotnet` is not
   on `PATH`.
3. Runs `dotnet test tools/plaincs/Hullbreach.Plain.Tests/Hullbreach.Plain.Tests.csproj`.

`Hullbreach.Plain.csproj` (`tools/plaincs/Hullbreach.Plain/`) compiles
every engine-free assembly from source, directly out of `Assets/Scripts`:
`Hullbreach.Core`, `.Ship`, `.World`, `.Structure`, `.Builder`, `.Net`,
plus the fetched `Unity.Mathematics` sources (with its own Editor/
Properties/attribute files excluded: those need the actual Unity
editor). `Hullbreach.Plain.Tests.csproj` compiles every `.cs` under
`Assets/Tests/EditMode` against that assembly, using NUnit +
`Microsoft.NET.Test.Sdk` + `NUnit3TestAdapter`: **the exact same test
sources Unity's own Test Runner would run**, because Unity Test Framework
is also NUnit underneath. There is no fork between "the tests we run in
CI" and "the tests the editor runs"; it is the same files, two different
hosts.

Run a subset or pass through extra `dotnet test` filters:

```
tools/plaincs/run_tests.sh --filter FullyQualifiedName~BlockBehaviourTests
```

## Running the play-mode tests (needs Unity)

`Assets/Tests/PlayMode/Hullbreach.Demo.Tests/` holds the demo scene's
play-mode suite: it loads `DemoScene`, drives it through a scripted
input source, and asserts the things "is the demo playable" actually
means. Unlike the edit-mode tests these need a real editor, because they
need a loaded scene, a running physics step and a renderer.

```
"/mnt/c/Program Files/Unity/Hub/Editor/6000.0.43f1/Editor/Unity.exe" \
  -batchmode -burst-disable-compilation \
  -projectPath "C:\path\to\project-hullbreach-game" \
  -runTests -testPlatform PlayMode \
  -testResults "C:\path\to\out\playmode.xml" \
  -logFile "C:\path\to\out\playmode.log"
```

Only ONE Unity instance may have a given project folder open, so close
the editor (or point `-projectPath` at a separate worktree) before
running this. The first run imports the project and takes several
minutes; later runs are one to three. Read `playmode.xml` for the
per-test results (`test-case` elements with `result="Failed"` carry a
`failure/message`) and `playmode.log` for the `Debug.Log` output each
test prints, which is where the measured stress ratios, load factors and
throttle readings live.

### The input seam

`UnityEngine.Input` cannot be driven from a test: there is no supported
way to synthesise a key press the legacy Input Manager will report. So
`DemoMode` and `ShipController` read player intent through
`IDemoInput` (`Assets/Scripts/Hullbreach.Game/DemoInput.cs`) instead of
calling `Input` directly. `LegacyDemoInput` is the shipping
implementation; a test assigns `demoMode.InputSource = new
ScriptedDemoInput()` (which also pushes the same source onto the
player's `ShipController`) and then sets `Thrust`/`SteerAxis` or calls
`PressFire()`/`PressToggle()`/`PressReset()`/`PressOverlay()`. The
`Press*` calls are one-frame pulses, matching `Input.GetKeyDown`'s
contract, because `ShipController` latches edge-triggered input.

`DemoSceneFixture` is the shared base class: it loads the scene, injects
the scripted input, and **fails the test if anything logs an error,
exception or assert** while it runs (`LogAssert.NoUnexpectedReceived`
plus a `logMessageReceived` handler). A silent `NullReferenceException`
every frame is exactly how "play mode instantly breaks" hides.

### Screenshots

`DemoScreenshots.CaptureDemoStates` writes PNGs of the demo in each
state worth looking at (build mode with a placement preview, full
thrust, reversing, the stress overlay, mid-shot). Point it at a
directory with `HULLBREACH_SHOTS`; if the variable is unset it falls
back to `Application.persistentDataPath/hullbreach-shots` and logs where
it went. From WSL the variable needs `WSLENV` to cross into the Windows
process:

```
WSLENV=HULLBREACH_SHOTS/w HULLBREACH_SHOTS='C:\path\to\out\shots' \
  "/mnt/c/Program Files/Unity/Hub/Editor/6000.0.43f1/Editor/Unity.exe" ...
```

`ScreenCapture.CaptureScreenshot` needs a real swapchain, so run
WITHOUT `-nographics`, and note that `-batchmode` on its own is still
not enough on Windows: a batchmode run reports the capture path but no
file appears. Run the editor normally (no `-batchmode`) and use the Test
Runner window, or accept that the numeric assertions are the part CI can
ever check.

## What CI checks

`.github/workflows/ci.yml` has three jobs, `tree`, `plaincs`, and `gate`:

- **`tree`**: `scripts/check_unity_tree.sh`: every asset/`.meta` pairs up
  correctly, no generated folder (`Library`, `Temp`, `Obj`, `Logs`,
  `UserSettings`, `Build`, `Builds`) is committed, `ProjectVersion.txt`
  still pins `6000.0.43f1`, and `Packages/manifest.json` plus every
  `.asmdef` parse as JSON. Needs bash and a `python`/`python3`/`py` on
  `PATH`; run it locally before pushing (`scripts/check_unity_tree.sh`,
  exit code is the number of problems found, so `0` means clean).
- **`plaincs`**: the `dotnet test` run described above.
- **`gate`**: the single required status check branch protection actually
  looks at ("All checks pass"): fails if either of the above did not
  succeed.

There is **no Unity editor in CI at all yet**. Editor/play-mode tests and
a headless dedicated-server build are both tracked as open items in
`TODO.md`, waiting on a Unity license secret (`UNITY_LICENSE`/
`UNITY_EMAIL`/`UNITY_PASSWORD`) before a `game-ci`-based job can be added.

## Which assemblies are NOT covered by `tools/plaincs`

**`Hullbreach.Game`**, for EDIT-mode purposes: the only assembly that
references `UnityEngine`
(`MonoBehaviour`s: `ShipController`, `BuilderController`, `ShipRenderer`,
`ShipStructure`, `WorldSink`, `GravityWorld`, `Powerup`/`PowerupSpawner`,
`Projectile`/`ProjectileSpawner`, `DemoMode`, `BuilderHud`,
`CameraFollow`). There is no `Hullbreach.Game.Tests` folder under
`Assets/Tests/EditMode` at all. Notice `Assets/Tests/EditMode` has one
`.Tests` folder per plain assembly (`Hullbreach.Builder.Tests`,
`Hullbreach.Core.Tests`, `Hullbreach.Net.Tests`, `Hullbreach.Ship.Tests`,
`Hullbreach.Structure.Tests`, `Hullbreach.World.Tests`,
`Hullbreach.Hud.Tests`) and no
`Hullbreach.Game.Tests`. Anything that only exists in `Hullbreach.Game`
(scene wiring, `DemoMode`'s state machine, `ShipRenderer`'s actual pixel
output, `Powerup`'s `OnTriggerEnter2D`) can only be exercised by opening
the editor and pressing Play, or by a future Unity Test Framework
play-mode test suite. The play-mode suite now exists
(`Assets/Tests/PlayMode/Hullbreach.Demo.Tests/`, see above) and covers
`DemoMode`'s state machine, the builder's placement API, projectile
spawning and hits, ship-to-ship contact and the structural calibration;
what it still does not cover is pixel output, which is what the
screenshot test exists to put in front of a human.

## How the Unity Test Runner runs the same sources

Inside the editor, Window -> General -> Test Runner -> EditMode tab lists
every `[Test]` under `Assets/Tests/EditMode`, grouped by each folder's
`.asmdef` (e.g. `Hullbreach.Ship.Tests.asmdef`). Unity compiles and runs
them with its own NUnit host, against the compiled `Hullbreach.Ship` etc.
assemblies from `Assets/Scripts`: the identical `.cs` files
`tools/plaincs` compiles, just through Unity's compiler and test host
instead of `dotnet test`. There is no separate "editor version" of a test
to keep in sync; if a test passes under `tools/plaincs/run_tests.sh` and
the editor disagrees, that is a real bug (a `LangVersion`/API-compatibility
mismatch between the plain `.csproj`'s `netstandard2.1`/C# 9 and Unity's
own compiler settings), not an expected difference.

## Writing a new test

1. **Pick the right assembly's test folder.** One test project per plain
   assembly, matching name: code in `Hullbreach.Ship` gets tests in
   `Assets/Tests/EditMode/Hullbreach.Ship.Tests/`. There is nowhere to put
   a test for `Hullbreach.Game` code yet (see above); if you are adding
   one, that is worth raising, not silently working around.
2. **Add the `.cs` file directly in that folder** (or a new file; NUnit
   discovers `[Test]`/`[TestFixture]` by attribute, not by file name).
   Every existing test file is plain public class + `[Test]` methods, no
   base class needed (see `Assets/Tests/EditMode/Hullbreach.Ship.Tests/BlockBehaviourTests.cs`
   for the house style, including the `[SetUp] RestoreDefaults()` pattern
   for anything that mutates `BehaviourRegistry`'s static table).
3. **`.asmdef` references**: each `*.Tests.asmdef` (e.g.
   `Hullbreach.Ship.Tests.asmdef`) already references the plain assembly
   it tests plus `Hullbreach.Core`/`Unity.Mathematics` as needed and
   Unity's built-in `nunit.framework.dll` (via `UNITY_INCLUDE_TESTS`
   define constraints Unity sets automatically for a Tests folder). You
   only need to add a reference if your new test needs a plain assembly
   the existing asmdef does not already reference (e.g. a
   `Hullbreach.Structure` test needing `Hullbreach.World`).
4. **`.meta` files**: every new `.cs` file needs a matching `.cs.meta`.
   Unity generates this automatically the moment the file exists inside
   an open project; if you are editing outside the editor, `git add` the
   `.meta` alongside the `.cs` exactly as `CONTRIBUTING.md`'s Unity rules
   require, or `scripts/check_unity_tree.sh` will fail with a missing-meta
   error.
5. **Run it two ways** before opening a PR: `tools/plaincs/run_tests.sh
   --filter FullyQualifiedName~YourTestClass` (fast, no editor) and, once
   you have the editor open, the Test Runner's EditMode tab (confirms the
   `.asmdef`/`.meta` wiring is actually correct, which the `dotnet test`
   path cannot fully verify since it compiles from raw file globs, not
   `.asmdef` references).
