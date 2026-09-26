# Hullbreach.Game reference

Per-type reference for `Assets/Scripts/Hullbreach.Game`, linked from the code by
`// frob:doc docs/reference/hullbreach-game.md#<anchor>`. One heading per
public type; each heading carries the `frob:describes` lines for that
type and its public members. Architecture-level context lives in
[architecture.md](../architecture.md).

### AuthoredBlock

<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipController.cs::AuthoredBlock -->

One block of the Inspector-authored ship, before it goes into the grid. A
plain serializable struct rather than a ScriptableObject or prefab-per-ship,
because Phase A only needs "a ship exists to fly", not a builder UI (that
is Hullbreach.Builder's job later).

### BuilderController

<!-- frob:describes Assets/Scripts/Hullbreach.Game/BuilderController.cs::BuilderController -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/BuilderController.cs::BuilderController.Session -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/BuilderController.cs::BuilderController.HoverValid -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/BuilderController.cs::BuilderController.HoverVerdictText -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/BuilderController.cs::BuilderController.HoverIndicator -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/BuilderController.cs::BuilderController.TryPlaceAt -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/BuilderController.cs::BuilderController.TryRemoveAt -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/BuilderController.cs::BuilderController.VerdictAt -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/BuilderController.cs::BuilderController.PreviewHoverAt -->

The MonoBehaviour adapter for `BuilderSession` (S30-S33). Lifecycle,
mouse-to-grid conversion and Gizmo drawing ONLY: every rule about what is a
legal click lives in `BuilderSession`/`PlacementRules`, which have no
UnityEngine dependency and are exercised in edit-mode tests. NOT compiled by
`tools/plaincs/run_tests.sh` (it depends on UnityEngine), so keep this file
thin and let the harness catch regressions in the logic it calls into.

### CameraFollow

<!-- frob:describes Assets/Scripts/Hullbreach.Game/CameraFollow.cs::CameraFollow -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/CameraFollow.cs::CameraFollow.SetTarget -->

Smoothly follows a target transform on the XY plane, keeping the camera's
own Z (its distance from the 2D scene). Orthographic size is left to the
Inspector/scene value rather than hardcoded here, so designers can still
tune framing without touching code.

### DemoState

<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoMode.cs::DemoState -->

Which of the two demo scene states is active: `Build` or `Fly`.

### DemoMode

<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoMode.cs::DemoMode -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoMode.cs::DemoMode.State -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoMode.cs::DemoMode.InputSource -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoMode.cs::DemoMode.PlayerShip -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoMode.cs::DemoMode.PlayerRenderer -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoMode.cs::DemoMode.PlayerStructure -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoMode.cs::DemoMode.Builder -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoMode.cs::DemoMode.OrbitStartPosition -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoMode.cs::DemoMode.StartsInOrbit -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoMode.cs::DemoMode.Warning -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoMode.cs::DemoMode.MaxStressRatio -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoMode.cs::DemoMode.CriticalBlockCount -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoMode.cs::DemoMode.CriticalBlockName -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoMode.cs::DemoMode.ActivePowerups -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoMode.cs::DemoMode.CriticalRatio -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoMode.cs::DemoMode.StrainRatio -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoMode.cs::DemoMode.CriticalLoadFactorFloor -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoMode.cs::DemoMode.SetState -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoMode.cs::DemoMode.ToggleState -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoMode.cs::DemoMode.ResetPlayer -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoMode.cs::DemoMode.OrbitStartVelocity -->

Top-level demo scene conductor: toggles between Build (ship frozen exactly
where it is, `BuilderController` editing the live grid) and Fly (simulation
on, WASD + arrows + Space + O + R). The status panel and hull warning
banner are uGUI views (`StatusPanelView`, `HullWarningBanner`) driven by the
read-only state exposed here (see [ui-port.md](../design/ui-port.md#2-decisions)
D8).

**Build-mode pause semantics ("Tab teleports me").** Build mode PAUSES the
ship rather than resetting it: switching modes must never move the ship.
Only R repositions anything. The pause is implemented on two independent
tracks that must both stop: `ShipController.SimulationEnabled` (the plain-C#
`ShipBody` integration) and `Rigidbody2D.simulated` (the Unity physics
body). Early versions only cleared input, which let `ShipBody` keep
integrating gravity every tick while `Rigidbody2D.simulated` was false; the
transform stayed put (`MovePosition` was a no-op) while `ShipBody` quietly
drifted underneath it, and the first Fly-mode `FixedUpdate` then snapped the
transform onto that drifted position - the ship visibly "teleported" the
instant Tab was pressed. `SimulationEnabled` closes that gap by also
stopping `ShipBody.Step` itself, so nothing drifts while paused and
resuming is exactly where the player left off.

### GravityWorld

<!-- frob:describes Assets/Scripts/Hullbreach.Game/GravityWorld.cs::GravityWorld -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/GravityWorld.cs::GravityWorld.Field -->

Scene-level gravity setup: builds a single `GravityField` from the
Inspector-authored `PlanetSpec` list on Awake and exposes it as a static
singleton so `ShipController` and `Projectile` can pick it up without a
scene-graph reference. Also spawns one visible disc per planet, since the
field itself is invisible plain data.

"Singleton-ish": `Field` is null until some `GravityWorld`'s Awake has run,
and `DemoMode`/`ShipController` read it lazily (null-safe) rather than
requiring load order: there is exactly one `GravityWorld` in any scene that
uses gravity, same convention as the rest of the demo.

### PlanetSpec

<!-- frob:describes Assets/Scripts/Hullbreach.Game/GravityWorld.cs::PlanetSpec -->

One planet/moon authored in the Inspector: its `GravityBody` plus the color
used for its runtime-generated disc, so the scene needs no baked sprites to
show planets.

### IDemoInput

<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoInput.cs::IDemoInput -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoInput.cs::IDemoInput.ThrustAxis -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoInput.cs::IDemoInput.Steer -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoInput.cs::IDemoInput.FirePressed -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoInput.cs::IDemoInput.TogglePressed -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoInput.cs::IDemoInput.ResetPressed -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoInput.cs::IDemoInput.OverlayPressed -->

Every player intent the demo scene reads, behind one interface so a
play-mode test can script it. `UnityEngine.Input` cannot be driven from a
test (there is no way to synthesise a key press the legacy Input Manager
will report), so the only way to prove the demo is playable is to make
"where input comes from" a seam instead of a hardcoded call. Level-triggered
axes are read every frame; the `*Pressed` members are EDGE-triggered and
must report true for exactly one Update, the same contract
`Input.GetKeyDown` has, because `ShipController` latches them.

### LegacyDemoInput

<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoInput.cs::LegacyDemoInput -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoInput.cs::LegacyDemoInput.Instance -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoInput.cs::LegacyDemoInput.ThrustAxis -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoInput.cs::LegacyDemoInput.Steer -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoInput.cs::LegacyDemoInput.FirePressed -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoInput.cs::LegacyDemoInput.TogglePressed -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoInput.cs::LegacyDemoInput.ResetPressed -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoInput.cs::LegacyDemoInput.OverlayPressed -->

The shipping implementation: the legacy Input Manager bindings the demo has
always used (W/S or Up/Down, A/D or Left/Right, Space, Tab, R, O).
Stateless, so one shared instance serves every component.

### ScriptedDemoInput

<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoInput.cs::ScriptedDemoInput -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoInput.cs::ScriptedDemoInput.Thrust -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoInput.cs::ScriptedDemoInput.SteerAxis -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoInput.cs::ScriptedDemoInput.PressFire -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoInput.cs::ScriptedDemoInput.PressToggle -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoInput.cs::ScriptedDemoInput.PressReset -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/DemoInput.cs::ScriptedDemoInput.PressOverlay -->

A scripted `IDemoInput` whose every member is a settable field, for
play-mode tests. The edge-triggered members auto-clear after one read so a
test can write `input.FirePressed = true` and get exactly the one-frame
pulse a real key press produces, rather than a key that is held down
forever.

### OrbitStarter

<!-- frob:describes Assets/Scripts/Hullbreach.Game/OrbitStarter.cs::OrbitStarter -->

Puts a non-player ship onto a circular orbit around one of `GravityWorld`'s
bodies at its authored position, on the first frame. Without this a scene
ship simply starts at rest inside a gravity field and falls into the planet
within seconds, which is why the demo scene's target ship was parked far
from the action: the only place it could survive was somewhere nothing else
was happening. The orbit velocity is derived from the transform, so moving
the ship in the Inspector can never leave a stale velocity behind.

### Powerup

<!-- frob:describes Assets/Scripts/Hullbreach.Game/Powerup.cs::Powerup -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/Powerup.cs::Powerup.Preset -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/Powerup.cs::Powerup.Configure -->

A floating pickup: a spinning tinted disc with a trigger collider. Always
created by `PowerupSpawner.Spawn`, which calls `Configure` immediately
after `AddComponent`: never place one in a scene directly, since it has
nothing to show/apply until configured.

**Contact resolution.** On contact with a ship (`OnTriggerEnter2D`) it looks
up the ship's `ShipCollider`/`ShipController` and calls
`ShipBody.ApplyPowerup(Preset.variant, Preset.baseTypeId, contactPoint,
Preset.seconds)`, which transforms the ship's nearest block of
`Preset.baseTypeId` into `Preset.variant` for `Preset.seconds`. If
`ApplyPowerup` reports the transform was applied, the powerup notifies its
parent `PowerupSpawner` (so a replacement spawns after a delay) and
destroys itself; if no eligible block was found, the pickup is left in
place for another attempt.

### PowerupSpawner

<!-- frob:describes Assets/Scripts/Hullbreach.Game/PowerupSpawner.cs::PowerupSpawner -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/PowerupSpawner.cs::PowerupSpawner.NotifyCollected -->

Spawns every authored `PowerupPreset` as a `Powerup` GameObject on Start,
and respawns one at the same place after `respawnSeconds` once it is
collected: this lets the demo scene keep a fixed lineup of pickups visible
near the player's orbit start without hand-placing prefabs.

### PowerupPreset

<!-- frob:describes Assets/Scripts/Hullbreach.Game/PowerupSpawner.cs::PowerupPreset -->

One powerup pickup authored in the Inspector: where it floats, which
variant it applies to which block type, how long the transform lasts, and
its color/label; the disc itself is generated at runtime
(`ShipRenderer.MakeSprite`), same convention as `GravityWorld`'s planets.

### Projectile

<!-- frob:describes Assets/Scripts/Hullbreach.Game/Projectile.cs::Projectile -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/Projectile.cs::Projectile.Configure -->

A single spawned cannon round: a small yellow circle with a `Rigidbody2D`
carrying it in a straight line, that applies the firing ship's own
recoil-free hit (impulse + damage) to whatever `ShipCollider` it touches,
then destroys itself. Spawned only by `ProjectileSpawner`: never construct
one directly, since `Configure` must run before the first `FixedUpdate`.

### ProjectileSpawner

<!-- frob:describes Assets/Scripts/Hullbreach.Game/ProjectileSpawner.cs::ProjectileSpawner -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ProjectileSpawner.cs::ProjectileSpawner.SpawnFromSink -->

Subscribes to every `ShipController`'s `ShotFired` in the scene and turns
each `ShotRequest` into a real `Projectile` GameObject. The Inspector fields
are written into every ship's `ShipBody.Projectile` at Start, so one
spawner tunes every ship's cannon uniformly for the demo. `FindOwner`
attributes a shot to whichever ship's position is nearest the shot's world
origin: simpler and robust enough for the demo's two ships, since ships are
never coincident.

### ShipCollider

<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipCollider.cs::ShipCollider -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipCollider.cs::ShipCollider.ColliderToKey -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipCollider.cs::ShipCollider.MarkDirty -->

Maintains one `BoxCollider2D` per block on the ship's grid, sized to one
cell and offset to that cell's center, so `Projectile` can hit-test against
real per-block geometry instead of one big hull box. Rebuilt whenever
`MarkDirty` is called (`BuilderController` after an edit, `ShipStructure`
after a detach).

### ShipContactsRunner

<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipContactsRunner.cs::ShipContactsRunner -->

Runs `ShipContacts.Resolve` over every pair of ships in the scene once per
FixedUpdate. Ordered AFTER `ShipController` (-100) and `ShipStructure` (-50)
so it acts on the positions this tick's integration produced, and its
corrections land before the next tick rather than a frame late. Ship
rigidbodies are kinematic (`ShipBody` is the only integrator), so nothing
else in the scene is resolving these overlaps: `ShipContacts.Resolve` is
the entire contact-resolution path for ship-vs-ship collision in this demo.

### ShipController

<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipController.cs::ShipController -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipController.cs::ShipController.Ship -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipController.cs::ShipController.InputEnabled -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipController.cs::ShipController.SimulationEnabled -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipController.cs::ShipController.InputSource -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipController.cs::ShipController.ShotFired -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipController.cs::ShipController.ApplyImpulse -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipController.cs::ShipController.ApplyDamage -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipController.cs::ShipController.ReplaceBlocks -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipController.cs::ShipController.RequestFire -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipController.cs::ShipController.ResetToOrigin -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipController.cs::ShipController.ResetTo -->

The MonoBehaviour adapter. Lifecycle and Inspector wiring ONLY: every line
of actual simulation belongs in `ShipBody`, which has no UnityEngine
dependency and is therefore testable in edit mode, Burst-compilable, and
runnable on the headless server.

This replaces PlayerSingle. Two things it fixes:

1. Forces move from Update to FixedUpdate. Update runs once per RENDERED
   frame with a variable delta; the 2D physics step runs on a fixed timer.
   Applying force from Update makes acceleration depend on framerate.
2. Input is LATCHED. `Input.GetButtonDown` is true for exactly one rendered
   frame, so polling it from FixedUpdate misses presses outright. Edge
   -triggered input is read in Update, stored, and consumed in
   FixedUpdate.

See [DemoMode](#demomode) for the "Tab teleports me" bug that
`SimulationEnabled` fixes.

### ShipRenderer

<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipRenderer.cs::ShipRenderer -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipRenderer.cs::ShipRenderer.Overlay -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipRenderer.cs::ShipRenderer.ExtraRatioSource -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipRenderer.cs::ShipRenderer.Solver -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipRenderer.cs::ShipRenderer.FlashRatioThreshold -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipRenderer.cs::ShipRenderer.MarkDirty -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipRenderer.cs::ShipRenderer.MakeSprite -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipRenderer.cs::ShipRenderer.ForwardFlameColor -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipRenderer.cs::ShipRenderer.RetroFlameColor -->

Builds and maintains one child GameObject per block on a ship's
`BlockGrid`, using a runtime-generated 1x1 white sprite tinted per type,
plus small nose/flame children for directional blocks. No prefabs, no asset
dependencies: every visual here is code-generated so the demo scene needs
nothing baked in the Editor.

Rebuild is driven by `MarkDirty`, called by whatever mutates the grid
(`BuilderController`, `ShipStructure` after a detach); this class does not
poll `BlockGrid.TopologyDirty` itself since that flag is owned by
`ShipBody`'s own Step/RebuildDerivedViews lifecycle and gets cleared before
a renderer polling on its own schedule could see it.

Forward thrusters read RED (the main drive is the loud, hot one, and it
must be unmistakable from the retro plumes); retro thrusters read GREEN,
the opposite channel, so which way the ship is pushing is readable at a
glance without looking at the HUD. A retro thruster pushes the ship
backward by exhausting FORWARD out two SIDE nozzles, so both plumes point
+y but sit outboard at the block's own +x/-x edges: straight above the
block is where the hull it is bolted to lives, so a plume centered there
would be drawn underneath solid hull and never seen.

### OverlayMode

<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipRenderer.cs::OverlayMode -->

Which per-block overlay `ShipRenderer` tints with, cycled by `DemoMode`'s O
key: `None`, `Stress`, `LoadBearing`, `Damage`, `Buckling`.

### ShipStructure

<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipStructure.cs::ShipStructure -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipStructure.cs::ShipStructure.DefaultLoadScale -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipStructure.cs::ShipStructure.DefaultMaterialStiffnessScale -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipStructure.cs::ShipStructure.Authoritative -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/ShipStructure.cs::ShipStructure.Solver -->

Runs the plain-C# `StructuralSolver` over a ship's grid every FixedUpdate,
using the forces `ShipBody` recorded this Step, and applies damage/detach
when a block's ratios cross `DamageModel`'s thresholds. Ordered AFTER
`ShipController` (-100) so `ship.AppliedForcesThisStep` for this tick is
already populated, and BEFORE `ShipRenderer` (default 0) so the Stress
overlay reads this tick's solve, not the previous one.

**Calibration.** `DefaultLoadScale` (0.06) is the gameplay-force to
material-unit conversion (`StructuralSolver.LoadScale`); the default is
measured, not guessed: at 1.0 the stock demo ship peaked at 1.08 of yield
under its own full thrust and shed both thrusters within 0.2s of the player
pressing W, which is the "play mode instantly breaks" report. 0.06 puts
that same peak near 0.34, inside the 0.3-to-0.5 band the demo is tuned to,
and gravity alone near 0.02.

`DefaultMaterialStiffnessScale` (40) is the E-over-yield ratio missing from
`BlockType`'s normalized material table (`StructuralSolver.MaterialStiffnessScale`).
Without it the demo ship read as buckling at a small fraction of its own
thrust and `ShipStructure` dutifully detached the "buckled" blocks. 200
would be a plausible E/yield for a stiff-but-not-steel structural material,
but 40 is the knob that SEPARATES the two ends of the calibration: it moves
buckling without touching stress at all, so the stock blob keeps a critical
load factor in the tens while a slender 1-wide arm still folds. Measured at
1.0 the stock ship read a load factor of 0.13 under its own thrust and
`ShipStructure` detached the blocks that "buckled": a rubber ship folding
up, not a metal one.

`Authoritative` gates whether this instance may act on
`Solver.BuckledBlocks` by detaching blocks: the FE solve is not
bit-identical across machines, so a client independently detaching from
`BuckledBlocks` can desync from the server (see
`StructuralSolver.BuckledBlocks`). Non-authoritative instances (clients)
still tint `BucklingRatio` via `ShipRenderer` but skip the break; they act
only on explicit block-died events broadcast by the server (see
`NetMessages.cs`).

`bucklingHoldSeconds` (Inspector-only, 0.4s default) is the delay between a
block entering `Solver.BuckledBlocks` and it actually detaching. Buckling
is published by an eigen-solve that only runs every `BucklingEveryNTicks`
and only once its subspace iteration has converged, so the first tick that
reports a sub-unity load factor is ALSO the first tick the player could
have been told anything. Detaching on that tick means the HUD warning and
the failure arrive in the same frame, which reads as "it just exploded". A
real column does not collapse instantaneously either: it deflects, then
goes. This hold is that deflection, and it is what makes the warning
actionable rather than a post-mortem.

### WorldSink

<!-- frob:describes Assets/Scripts/Hullbreach.Game/WorldSink.cs::WorldSink -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/WorldSink.cs::WorldSink.Instance -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/WorldSink.cs::WorldSink.Ships -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/WorldSink.cs::WorldSink.Refresh -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/WorldSink.cs::WorldSink.SpawnProjectile -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/WorldSink.cs::WorldSink.AddTemporaryGravity -->
<!-- frob:describes Assets/Scripts/Hullbreach.Game/WorldSink.cs::WorldSink.TryNearestEnemy -->

The game-side `IWorldSink`: routes block-behaviour requests (spawn a
projectile, drop a temporary gravity well, find the nearest enemy) onto the
actual scene: `ProjectileSpawner`, `GravityWorld.Field`, and every
`ShipController` found in the scene. One instance per scene, exposed as a
static `Instance` (same convention as `GravityWorld.Field`) so
`ShipController.Awake` can wire it up without a scene-graph reference.
