# The demo scene

`Assets/Scenes/DemoScene.unity`, registered as the first
`EditorBuildSettings` entry alongside `RocketScene`. Hand-authored YAML
(commit `f680856`, "author DemoScene with a flyable/buildable player
ship"). See the checklist at the bottom before you trust anything about
it beyond "it parses and CI's tree check passes".

## Object / fileID layout

Five root GameObjects, fileIDs grouped by the thousands digit so it is
obvious which object a component belongs to just from its fileID:

| fileID prefix | Object | Components (in order) |
| --- | --- | --- |
| `2000xxx` | (camera scaffolding: Transform, Camera, etc.; `fileID 1` block for the built-in camera setup) | -- |
| n/a | **Main Camera** (`2000001`) | Transform, Camera, GUILayer/AudioListener, **`CameraFollow`** (target = PlayerShip's Transform, smoothing = 5) |
| `2001xxx` | **Demo** | Transform, **`DemoMode`**, **`ProjectileSpawner`**, **`BuilderHud`**, **`WorldSink`**, **`ShipContactsRunner`** |
| `2002xxx` | **PlayerShip** | Transform, `Rigidbody2D`, **`ShipController`**, **`ShipRenderer`**, **`ShipCollider`**, **`ShipStructure`**, **`BuilderController`** |
| `2003xxx` | **TargetShip** | Transform, `Rigidbody2D`, **`ShipController`**, **`ShipRenderer`**, **`ShipCollider`**, **`ShipStructure`**, **`OrbitStarter`** (no `BuilderController`; it is never edited) |
| `2004xxx` | **Gravity** | Transform, **`GravityWorld`** |
| `2005xxx` | **Powerups** | Transform, **`PowerupSpawner`** |

`DemoMode` (on **Demo**, fileID `2001003`) wires everything by direct
fileID reference in its Inspector fields: `playerShip: {fileID: 2002004}`
(PlayerShip's `ShipController`), `playerBody: {fileID: 2002003}`
(PlayerShip's `Rigidbody2D`), `builder: {fileID: 2002008}`
(PlayerShip's `BuilderController`), `builderHud: {fileID: 2001005}`
(Demo's own `BuilderHud`), `playerRenderer: {fileID: 2002005}`,
`playerStructure: {fileID: 2002007}`. It also carries `startInOrbit: 1`,
`orbitStartPosition: {x: 0, y: -30}`, `orbitBodyIndex: 0`. The player
now STARTS on that circular orbit around the big planet (index 0 in
`Gravity`'s `GravityWorld.planets` list): `DemoMode.Start` calls
`ResetPlayer` before the first physics step, so the demo no longer opens
with the ship dropped at the origin falling straight at the planet.
**R** in Fly mode puts it back there. Nothing else moves the ship.

`WorldSink` (Demo, fileID `2001006`) points `spawner:` at `2001004`, the
`ProjectileSpawner` on the same object: this is the concrete
`IWorldSink` implementation every `ShipBody` on a ship in this scene uses
(see `docs/architecture.md`'s note on `IWorldSink`/`NullWorldSink`).

`ProjectileSpawner` (Demo, fileID `2001004`) carries the default
`ProjectileSpec` numbers directly in the Inspector: `speed: 20, impulse:
2, damage: 25, lifetime: 3, radius: 0.1`, the same defaults as
`ProjectileSpec.Default` in code, duplicated here because the scene
authors its own spawner rather than reading the ship's spec (worth
noting if the two ever need to diverge on purpose).

## Positions and bodies

- **PlayerShip** starts on the circular orbit at `(0, -30)`, PAUSED (both
  `ShipController.SimulationEnabled` and `Rigidbody2D.simulated` are
  false) while in Build mode. Pausing rather than resetting is what makes
  Tab safe: see "Switching modes" below.
- **TargetShip** sits on the same orbit about ten units ahead at
  `(-10.26, -31.8)`, put there (with the matching orbital velocity) by an
  `OrbitStarter`. It used to be parked at `(0, 24)` with no velocity,
  which was the only place it could survive: at rest inside a gravity
  field it simply fell into the planet.
- Both ships' `Rigidbody2D`s are **Kinematic** with
  `m_UseFullKinematicContacts: 1`. `ShipBody` is the authoritative
  integrator and `ShipController` overwrites position and velocity every
  `FixedUpdate`; leaving the bodies Dynamic meant Box2D's depenetration
  solver fought that overwrite on every ship-to-ship overlap and flung
  one of them across the map. Ship-to-ship contact is resolved in plain
  C# by `ShipContacts.Resolve`, driven by the `ShipContactsRunner` on
  **Demo**.
- **Gravity** holds a `GravityWorld` with two planets: a big one at
  `(0, -60)`, radius 18, and a small moon at `(45, 20)`, radius 6.
- **Powerups** holds three `PowerupPreset` entries, all near `(0, -30)`
  (the player's orbit start): `(-6, -30)` gravity gun (Cannon variant 1),
  `(0, -26)` seeking thruster (Thruster variant 1), `(6, -30)`
  anti-gravity gun (Cannon variant 2), each radius `0.6`.

## Controls (see also `DemoMode.OnGUI`'s always-on panel)

**Build mode** (starting mode; player ship frozen):

- `1`-`7`: select a palette entry (Core, Hull, Armor, Thruster, Cannon,
  Fin, RetroThruster, `BlockTypes` order).
- Hover: green preview if the current selection can be placed there, red
  with a `PlacementVerdict` reason otherwise.
- Left click: place (directional blocks take a second click to choose
  facing). Right click: remove (can strand and remove other blocks too).
- `Ctrl+Z` / `Ctrl+Shift+Z`: undo / redo. `Esc`: cancel an in-progress
  facing choice.

**Fly mode** (`Tab` to switch from Build):

- `W`/`S` or `Up`/`Down`: forward thrusters / retro thrusters.
- `A`/`D` or `Left`/`Right`: fins.
- `Space`: fire every cannon off cooldown.
- `O`: cycle overlay, `None -> Stress -> LoadBearing -> Damage ->
  Buckling -> None` (`DemoMode.NextOverlay`).
- `R`: reset. With `startInOrbit` set (the shipped scene has it), drops
  onto the preset circular orbit; otherwise resets to rest at the origin.
  This is the ONLY control that repositions the ship.

## Switching modes

`Tab` pauses and resumes; it never moves anything. `DemoMode.ApplyState`
sets `ShipController.SimulationEnabled` and `Rigidbody2D.simulated`
together, so in Build mode the plain-C# body stops integrating at the
same instant the rigidbody stops simulating. Previously only the
rigidbody was frozen: `ShipBody` kept integrating gravity every tick
while `MovePosition` was a no-op, so the transform stayed put while the
simulation drifted away from it, and the first Fly-mode `FixedUpdate`
snapped the transform onto the drifted position. That snap is what read
as "Tab teleports me".

Leaving Build also disables `BuilderController` BEFORE flight resumes,
and its `OnDisable` hides the hover cursor and cancels any half-finished
two-click placement, so the red/green build outline can never be left on
screen over a flying ship.

## Overlays and the HUD

`ShipRenderer.Overlay` (`OverlayMode`): `None` (plain per-type color),
`Stress` (green-to-red by `max(DuctileRatio, BrittleRatio)`),
`LoadBearing` (magenta on articulation points), `Damage` (white-to-black
by `DamageFraction`), `Buckling` (blue-to-red by `BlockStress
.BucklingRatio`). The always-on `OnGUI` panel (bottom-left) shows mode,
per-mode controls, current overlay, mass, block count, and in Fly mode:
speed, angular speed, three control-channel bars, and one line per
active powerup (`DemoMode.DrawActivePowerups`, e.g. "Cannon (0,2):
Gravity gun 6.2 s").

The **control bars** read straight off `ShipBody.ForwardThrottleMean`,
`ReverseThrottleMean` and `SteerThrottleMean`, the same aggregates the
play-mode tests assert on, so what the player sees and what the tests
check can never drift apart: Thrust 0-100% (red fill), Reverse 0-100%
(green fill), Steer -100 to +100% (white, filling left or right from a
centered tick). All three RAMP rather than snapping, over the ~1 s
`ThrusterUpgrades` ramp time for stock parts.

A separate **structural readout** sits top-center in Fly mode
(`DemoMode.DrawHullWarning`): green `Hull: OK` below 0.5 of yield,
yellow `Hull: STRAIN` from 0.5, flashing red `Hull: CRITICAL` from 0.8
or whenever the critical load factor drops below 1.5. When it is not OK
it also names how many blocks are in the red, the worst one's type, and
a one-line hint ("ease off thrust" / "brace the arm"). Independently of
any overlay, `ShipRenderer` pulses any block at or above
`FlashRatioThreshold` (0.8) toward alarm red, so the player can see
WHERE the problem is without switching to the Stress overlay.

## Resetting

`R` in Fly mode calls `ShipController.ResetTo`/`ResetToOrigin`. This only
resets the *player* ship's position/velocity/rotation: it does not
re-run `BuilderSession` or restore the ship to its as-authored block
layout, so a ship that lost blocks to combat or a buckling failure stays
lost after a reset. There is no scene-reset ("restart the whole demo")
control; reopening the scene in the editor (or `Ctrl+P`/`Ctrl+P` to stop
and restart Play mode) is the only full reset.

## Structural calibration

`BlockType` normalizes materials for conditioning (hull yield = 1.0,
E = 1.0) while thrust and gravity are authored in whatever units fly
nicely (`ThrustPerBlock` 10, planet `mu` 900). Those two scales have no
reason to agree, and they did not: at play-mode start the demo ship's
own thrusters put its most loaded block at 1.08 of yield, and it shed
both thrusters within 0.2 s of the player pressing W. That is the "the
ship switching to play mode instantly breaks" report.

Two conversion factors fix it, both on `StructuralSolver` and both set
from `ShipStructure`'s Inspector fields. Neither disables a failure
check; the mechanic still fires, just where it should.

| Knob | Default | What it moves | What it does NOT move |
| --- | --- | --- | --- |
| `LoadScale` | `0.06` | The whole load vector (applied forces AND the inertia relief that balances them), so every stress ratio scales with it | Buckling separation: it scales load factors too |
| `MaterialStiffnessScale` | `40` | The published buckling load factors, equivalent to solving with `E` multiplied by it | Stress, which for a given self-equilibrated load is independent of `E` |

`MaterialStiffnessScale` is the one that separates the two ends of the
calibration. It stands for the E-over-yield ratio the normalized table
omits: E = yield = 1 describes a material that yields at unit strain, a
rubber, and a rubber blob under 20 N of thrust genuinely does buckle.
At 1.0 the stock ship read a critical load factor of 0.13 under its own
thrust and `ShipStructure` dutifully detached the blocks that "buckled".
Because it moves buckling without touching stress, a compact hull keeps
a load factor in the tens while a slender 1-wide arm still folds.

Measured in `DemoStructureTests` (numbers printed into the play-mode
log, ten seconds per combination):

| Control combination | Peak ratio | Lowest load factor |
| --- | --- | --- |
| full thrust | 0.339 | 14.17 |
| full reverse | 0.333 | 14.17 |
| full steer, either way | 0.308 | 53.35 |
| thrust + hard turn, either way | 0.397 | 13.02 |
| reverse + hard turn | 0.386 | 13.02 |
| coasting (gravity only) | 0.011 | inf |

No blocks are lost under any of them, and the HUD stays green. The
other end is `LongArm_WarnsThenBreaks`: a 43-block ship with a 1-wide
12-cell hull arm and a thruster on its tip drives its critical load
factor under 1 within a fraction of a second of full thrust, the HUD
goes CRITICAL, and the arm comes off.

`ShipStructure.bucklingHoldSeconds` (0.4 s) is why the warning arrives
first. The buckling eigen-solve only publishes on the ticks its subspace
iteration converges, so the first tick reporting a sub-unity load factor
is also the first tick the player could have been told anything;
detaching on that tick put the warning and the failure in the same
frame. A block must now stay in `BuckledBlocks` for that long before it
detaches, and a block that stops buckling loses its accumulated time
entirely.

## What the play-mode tests now verify

`Assets/Tests/PlayMode/Hullbreach.Demo.Tests/` loads this scene and
drives it through a scripted `IDemoInput` (see `docs/testing.md` for how
to run it, and why the input seam exists). Every one of these was a
"first open in Unity" checklist item; they are now checked on every run
instead of by hand, and every one of them fails the test if ANY error or
exception is logged while it runs:

- **`DemoScene` loads and runs clean.** No missing scripts, no null
  Inspector reference, no `NullReferenceException`: the fixture's log
  guard fails on the first `LogType.Error`/`Exception`/`Assert`.
- **`NoInput_ShipSurvivesTenSeconds`.** Left alone for ten seconds the
  player keeps all nine blocks, stays finite and stays in the arena.
  This covers "the ship instantly breaks" and the old checklist item
  about contact damage and the block layout seeding correctly.
- **`ToggleMode_DoesNotTeleport`.** Four Build/Fly switches with a
  second of flight between: each moves the ship less than 0.5 units
  (measured: 0.000 into Build, one tick of orbital motion out of it) and
  never spikes velocity. Also asserts the build hover cursor is hidden
  and `BuilderSession.State` is back to `Idle` in Fly mode.
- **`Thrust_MovesForwardWithoutBreaking`** and
  **`Steer_TurnsAndFadesOnRelease`.** Both channels ramp rather than
  snapping (still climbing at 0.3 s, full by 1 s), thrust accelerates
  the ship along its own nose, a symmetric ship does not spin up under
  pure thrust, and releasing steer lets the spin decay.
- **`Fire_SpawnsProjectileAndHitsTarget`.** A projectile exists one
  frame after the press and damages the target within four seconds:
  the old checklist's "confirm the FE/overlay pipeline in a live scene"
  and the `ProjectileSpawner`/`ShipCollider` wiring in one.
- **`Build_PlaceAndRemove`.** Places a hull beside the core, is refused
  (`InsideReservedCell`) when placing into a thruster's exhaust cell,
  removes, and undoes, all through `BuilderController`'s public API.
- **`Overlay_CyclesWithoutErrors`** walks all five overlays and back.
- **`Reset_ReturnsToStart`** lands within 1 unit of the orbit start on
  exactly the computed orbital velocity.
- **`RammingTheTarget_DoesNotFlingEitherShip`.** Full thrust into the
  target for three seconds: they touch (1.17 units apart at closest),
  neither is flung, and neither exceeds 40 units/s.
- **`StockShip_SurvivesEveryControlCombination`** and
  **`LongArm_WarnsThenBreaks`**: the calibration table above.

Still NOT covered, and still worth a human eye on first open:

- [ ] URP materials/shaders compile cleanly on first import
      ("everything is pink" in `README.md`'s "Things that bite people").
      The play-mode tests run without `-nographics` but assert nothing
      about pixels.
- [ ] The generated 1x1 white sprite (`ShipRenderer.MakeSprite`) and the
      runtime exhaust `ParticleSystem`s render correctly under URP 2D:
      block colors, the powerup pulse, the red forward flames and the
      green retro plumes. `DemoScreenshots` captures these for a human
      to look at; it cannot judge them.
- [ ] `PowerupSpawner`'s three presets spawn, are collectible, and
      respawn ten seconds later.
- [ ] `EditorBuildSettings.asset` still lists the missing `MainMenu`/
      `GameSceneOld`/`GameResources` scenes; confirm a build actually
      launches `DemoScene` first rather than erroring on a missing scene
      index.
