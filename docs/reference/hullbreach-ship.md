# Hullbreach.Ship reference

Per-type reference for `Assets/Scripts/Hullbreach.Ship`, linked from the code by
`// frob:doc docs/reference/hullbreach-ship.md#<anchor>`. One heading per
public type; each heading carries the `frob:describes` lines for that
type and its public members. Architecture-level context lives in
[architecture.md](../architecture.md).

### BlockFacing

<!-- frob:describes Assets/Scripts/Hullbreach.Ship/BlockFacing.cs::BlockFacing -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/BlockFacing.cs::BlockFacing.Mask -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/BlockFacing.cs::BlockFacing.FromModifiers -->

Decodes the low 2 bits of `Block.Modifiers` into a ship-local facing for
thrusters and cannons. Kept in one place so every system that cares which
way a directional block points (thrust application, muzzle direction,
gizmo drawing) agrees on the same encoding: 0 = +y ("up"), 1 = +x, 2 = -y,
3 = -x, all in ship-local space before `Rotation` is applied.

- `FromModifiers`: thin alias for `Hullbreach.Core.Facing.Direction`, kept
  here so existing Ship-side callers do not need to change their using
  directives.

### ThrusterUpgrades

<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ThrusterUpgrades.cs::ThrusterUpgrades -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ThrusterUpgrades.cs::ThrusterUpgrades.Mask -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ThrusterUpgrades.cs::ThrusterUpgrades.RampRate -->

Decodes `Modifiers` bits 2-3 (mask `0b1100`) into a throttle ramp rate for
thrusters, retro thrusters and fins alike: all three ramp their control
value toward a target rather than snapping to it, and all three share the
same upgrade encoding so one block-modifier byte can carry both facing
(low 2 bits) and ramp level (next 2 bits).

- `RampRate`: seconds to go from zero to full throttle at each upgrade
  level 0..3 are `{1.0, 0.6, 0.35, 0.15}`; level 3 is a near-instant
  reaction thruster, level 0 is the sluggish stock part. The same rate is
  used ramping up and down.

### ShotRequest

<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShotRequest.cs::ShotRequest -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShotRequest.cs::ShotRequest.Key -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShotRequest.cs::ShotRequest.WorldOrigin -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShotRequest.cs::ShotRequest.WorldDirection -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShotRequest.cs::ShotRequest.Spec -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShotRequest.cs::ShotRequest.ShotRequest -->

One cannon shot handed off by `ShipBody.Step` for the caller to spawn.
`ShipBody` only records intent (and applies its own recoil); it never
creates the projectile object, so this stays a plain value the caller
drains from `ShipBody.PendingShots`.

### ProjectileKind

<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ProjectileSpec.cs::ProjectileKind -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ProjectileSpec.cs::ProjectileKind.None -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ProjectileSpec.cs::ProjectileKind.GravityWell -->

What special payload a projectile carries beyond plain damage. `None` is
the stock cannon round; `GravityWell` marks a shot whose impact should
drop a temporary gravity well (positive Mu) or anti-well (negative Mu) via
`IWorldSink.AddTemporaryGravity`, per its `WellSpec`.

### WellSpec

<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ProjectileSpec.cs::WellSpec -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ProjectileSpec.cs::WellSpec.Mu -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ProjectileSpec.cs::WellSpec.Radius -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ProjectileSpec.cs::WellSpec.Seconds -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ProjectileSpec.cs::WellSpec.WellSpec -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ProjectileSpec.cs::WellSpec.None -->

Parameters for the temporary gravity well a GravityWell-kind projectile
drops on impact. Meaningless (and ignored) for `Kind == None`.

- `Mu`: gravitational parameter G*M of the dropped well. Negative values
  repel instead of attract (the anti-gravity gun).
- `Radius`: physical radius of the dropped well, same meaning as
  `GravityBody.Radius`.
- `None`: the default, inert well spec (zero pull, zero lifetime) used by
  every projectile that is not a gravity well.

### ProjectileSpec

<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ProjectileSpec.cs::ProjectileSpec -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ProjectileSpec.cs::ProjectileSpec.Speed -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ProjectileSpec.cs::ProjectileSpec.Impulse -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ProjectileSpec.cs::ProjectileSpec.Damage -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ProjectileSpec.cs::ProjectileSpec.LifetimeSeconds -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ProjectileSpec.cs::ProjectileSpec.Radius -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ProjectileSpec.cs::ProjectileSpec.Kind -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ProjectileSpec.cs::ProjectileSpec.Well -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ProjectileSpec.cs::ProjectileSpec.ProjectileSpec -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ProjectileSpec.cs::ProjectileSpec.Default -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ProjectileSpec.cs::ProjectileSpec.WithGravityWell -->

Immutable description of the projectile a cannon fires. Plain data so the
demo-scene branch can spawn whatever visual/physics object it wants from a
`ShotRequest` without `ShipBody` knowing about prefabs.

- `Default`: baseline cannon round (`speed: 20, impulse: 2, damage: 25,
  lifetimeSeconds: 3, radius: 0.1`); a reasonable default until the
  balance pass picks real numbers.
- `WithGravityWell`: returns a copy of this spec with `Kind = GravityWell`
  and the given well parameters attached, everything else unchanged: used
  by the gravity-gun/anti-gravity-gun variants to reuse the ship's own
  ballistic numbers (speed, damage, lifetime, radius) while swapping only
  the impact payload.

### ShipContacts

<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipContacts.cs::ShipContacts -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipContacts.cs::ShipContacts.BlockRadius -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipContacts.cs::ShipContacts.Restitution -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipContacts.cs::ShipContacts.Resolve -->

Ship-to-ship contact, resolved in plain C# by the same authority that
integrates the ships.

WHY NOT BOX2D: `ShipBody` is authoritative for ship motion, and
`ShipController` overwrites the `Rigidbody2D`'s position and velocity
every `FixedUpdate`. Two DYNAMIC bodies driven that way still get Box2D's
depenetration solver run on them when their per-block `BoxCollider2D`s
overlap: Box2D computes a large separation velocity for an overlap our
`MovePosition` keeps re-creating, and the result is one ship flung across
the map. Making the bodies kinematic and resolving contact here removes
the fight entirely: there is exactly one integrator.

The geometry is a cheap proxy: each block is a disc of radius 0.5 at its
world center. Blocks are unit squares, so this slightly over-reports
contact at the corners, which for a collision response that already ends
in a restitution impulse is not worth an OBB test.

- `Resolve`: resolves at most one contact between `a` and `b` for this
  step: finds the deepest overlapping block pair, pushes the two ships
  apart along the contact normal in inverse-mass proportion, and applies
  an equal and opposite restitution impulse at the contact point. Above
  `ShipBody.ContactDamageSpeed` of closing speed both contacting blocks
  take damage, on the same curve as a planet impact. Returns whether a
  contact was found. One pair per step is enough: the deepest pair
  dominates the response, and resolving every overlapping pair in one
  pass double-counts the push for a flush face-to-face hit.

### ShipBody

<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.Grid -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.Position -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.Rotation -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.Velocity -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.AngularVelocity -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.ThrustPerBlock -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.RetroThrustPerBlock -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.FinForce -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.CannonCooldown -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.Projectile -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.Gravity -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.World -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.ContactDamageSpeed -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.ContactDamagePerSpeed -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.Friction -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.AngularDamping -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.ContactClearance -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.ContactsThisStep -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.ThrusterKeys -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.RetroKeys -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.FinKeys -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.WeaponKeys -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.FireRequested -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.PendingShots -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.AppliedForcesThisStep -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.LastLinearAcceleration -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.LastAngularAcceleration -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.RebuildDerivedViews -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.Throttle -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.SteerThrottle -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.ForwardThrottleMean -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.ReverseThrottleMean -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.SteerThrottleMean -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.Step -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.RampThrottleFor -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.TickCooldownFor -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.SetCooldownFor -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.RotateLocalToWorld -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.ApplyPowerup -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.VariantTimeLeft -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.LocalToWorld -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.WorldToLocal -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.WorldVectorToLocal -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.AddForceAtPoint -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.ApplyImpulseAtWorldPoint -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.ApplyDamageAtWorldPoint -->

The ship simulation. PLAIN C# ON PURPOSE: no UnityEngine anywhere in this
assembly. That constraint buys three things: edit-mode tests that run in
milliseconds without a scene, Burst-compilable hot paths, and a simulation
the headless server (S47) can run without Unity's object model. The
MonoBehaviour that drives this (`ShipController`) is a thin adapter
holding lifecycle and Inspector wiring, and nothing else.

- `ThrustPerBlock`: tunable force one FULLY THROTTLED forward thruster
  block contributes, in ship-local newtons-per-block.
- `RetroThrustPerBlock`: tunable force one fully throttled retro thruster
  block contributes. Defaults to half of `ThrustPerBlock`: retros are two
  small side nozzles, not a main engine.
- `FinForce`: force magnitude a fin applies at full (`|throttle| = 1`)
  steer authority.
- `CannonCooldown`: seconds between shots for a single cannon block.
- `Gravity`: the gravity field this ship falls in, or null for none (e.g.
  RocketScene, which never wires one up). Set by the caller
  (`ShipController.Awake`) rather than owned here, so the same `ShipBody`
  can be dropped into a field-less test without a stub.
- `World`: the game-side sink block behaviours use for spawning
  projectiles, dropping temporary gravity, and finding targets. Defaults
  to `NullWorldSink` so a `ShipBody` built by a test (or the headless
  server with no game layer wired up yet) never needs a null check to
  `Step`. Set by the caller (`ShipController.Awake`).
- `ContactDamageSpeed`: relative contact speed (m/s) above which a planet
  impact starts dealing damage; below this, a landing is "soft" and does
  no damage at all.
- `ContactDamagePerSpeed`: damage points per m/s of contact speed above
  `ContactDamageSpeed`.
- `Friction`: fraction of tangential contact velocity removed per second
  while a block is touching a planet's surface, approximating ground
  friction without a full friction-cone solve.
- `AngularDamping`: exponential decay rate (per second) applied to
  `AngularVelocity` every `Step`. Without it nothing ever stops a ship
  spinning: fins apply a torque, and when the player lets go the ship
  keeps the rotation rate it reached forever, so aiming means
  counter-steering exactly, which is what made the demo feel
  uncontrollable. Defaults to 0 so `ShipBody`'s own unit tests (and any
  caller that wants pure Newtonian rotation) are unaffected; the demo's
  `ShipController` sets it from the Inspector.
- `ContactClearance`: clearance (world units) added to a planet's `Radius`
  when testing block contact, so a block's own half-extent does not sink
  visibly into the surface before contact registers.
- `ContactsThisStep`: every planet contact resolved this Step: which
  block (grid key), the surface normal at that block, and the impact
  speed along that normal. Cleared and repopulated every Step, for the
  renderer/audio to react to.
- `ThrusterKeys`/`RetroKeys`/`FinKeys`/`WeaponKeys`: thruster/retro/fin/
  weapon block keys, rebuilt when topology is dirty. Dense typed lists,
  because systems iterate "all thrusters" rather than dispatching
  polymorphically over all blocks.
- `FireRequested`: set when `FirePressed` arrives in `Step`, for a later
  combat system to consume. This is deliberately the simplest possible
  hand-off: a flag, not an event, because nothing downstream exists yet to
  justify more machinery.
- `PendingShots`: shots fired this Step, appended to and left for the
  caller (`ShipController`) to drain and clear. `ShipBody` never spawns
  projectile objects itself: it only records intent and applies its own
  recoil.
- `AppliedForcesThisStep`: every continuous force applied via
  `AddForceAtPoint` this Step (thrusters, retros, fins), in SHIP-LOCAL
  coordinates. Cleared at the start of every Step. A later structural
  system (ShipStructure) feeds these straight into
  `StructuralSolver.Tick` without `ShipBody` needing to know
  `StructuralSolver` exists.
- `LastLinearAcceleration`: linear acceleration computed by the last
  Step, in world space. Exposed (not just consumed internally) because the
  FE inertia-relief work on another branch needs the same `a = F/M` this
  integrator already computed.
- `LastAngularAcceleration`: angular acceleration computed by the last
  Step.
- `RebuildDerivedViews`: rebuild `ThrusterKeys`/`RetroKeys`/`FinKeys`/
  `WeaponKeys` from the grid. O(block count); called lazily from `Step`
  only when `Grid.TopologyDirty`, so placing or removing blocks is what
  pays this cost, not every physics tick. Per-key ramp/cooldown state is
  pruned to the surviving keys but otherwise preserved, so placing an
  unrelated block does not reset an in-progress throttle ramp.
- `Throttle`: current throttle 0..1 of the thruster/retro at `key`, for
  the renderer to size its flame effect.
- `SteerThrottle`: current steer throttle -1..1 of the fin at `key`, for
  the renderer to animate the control surface.
- `ForwardThrottleMean`/`ReverseThrottleMean`/`SteerThrottleMean`: mean
  throttle across every forward thruster/retro thruster/fin after the
  last Step. One aggregate read by BOTH the HUD bars and the play-mode
  tests, so what a player sees and what a test asserts on can never drift
  apart. Zero when the ship has no thrusters.
- `Step`: advance one FIXED timestep. Never call this from Update: the
  physics step runs on a fixed timer, and applying force per rendered
  frame makes a 144 Hz machine fly differently from a 60 Hz one, and both
  differently from the headless server. Forces/torques are accumulated in
  SHIP-LOCAL space (thrusters and fins are fixed to the hull), then the
  net force is rotated into world space before integrating: torque is a
  scalar about the out-of-plane axis and is unaffected by that rotation.
  Three ramped control channels drive everything: forward (target 1 while
  ThrustAxis > 0), reverse (target 1 while ThrustAxis < 0) and steer
  (target Steer, -1..1). Each thruster, retro and fin block ramps its OWN
  throttle toward its channel's target at a rate from its own upgrade
  bits, so releasing a key fades the effect out rather than cutting it
  instantly.
- `RampThrottleFor`: ramps `key`'s stored throttle toward `target` at the
  rate implied by `modifiers`' ramp-upgrade bits, stores, and returns the
  updated value. Shared by every ramped-throttle behaviour (forward/retro
  thrust, fin steer, seeking thrust): one block belongs to exactly one
  behaviour, so keys never collide.
- `TickCooldownFor`: ticks `key`'s stored cooldown down by `dt` (floored
  at zero), stores, and returns the seconds remaining.
- `SetCooldownFor`: sets `key`'s stored cooldown to `seconds`, e.g. right
  after firing.
- `RotateLocalToWorld`: rotates a ship-local free vector into world space
  (no translation), for behaviours that compute a world-space muzzle
  direction from a ship-local facing. Thin public wrapper over the
  private rotation helper the rest of `Step` already uses.
- `ApplyPowerup`: finds the nearest block of `baseTypeId` (base variant or
  already transformed, ties broken by lowest key for determinism) to
  `worldPoint`, sets its variant bits to `variant`, and records that it
  should revert to variant 0 after `seconds` of further `Step` calls.
  No-op if no block of that type exists. This is the whole "temporary
  transform" mechanism a powerup pickup drives.
- `VariantTimeLeft`: seconds remaining before the temporary variant at
  `key` reverts to base, or 0 if that key has no active powerup.
- `LocalToWorld`: ship-local point to world space: rotate by `Rotation`,
  then translate by `Position`.
- `WorldToLocal`: world point to ship-local space: exact inverse of
  `LocalToWorld`.
- `WorldVectorToLocal`: rotates a world-space free VECTOR (force,
  direction, no translation) into ship-local space; the vector
  counterpart of `WorldToLocal`, used to fold a world-space gravity force
  into the ship-local force accumulator that `AddForceAtPoint` expects,
  and by the seeking thruster to turn a world-space "toward the enemy"
  direction into a ship-local force direction.
- `AddForceAtPoint`: accumulate a force applied at a ship-local point.
  Updates both the linear accumulator and the torque about the center of
  mass: `tau = r x F`, with r measured from the CoM, and in 2D
  `r x F = r.x * F.y - r.y * F.x`.
- `ApplyImpulseAtWorldPoint`: apply an INSTANTANEOUS impulse at a
  world-space point: `dv = J/M`, `dw = cross(r, J)/I` with r measured
  from the world-space center of mass. Used for cannon recoil, which
  should feel like a kick right now rather than a force integrated over
  the next dt.
- `ApplyDamageAtWorldPoint`: apply `damage` (saturating) to whatever
  block occupies the grid cell under a world-space point, e.g. a
  projectile hit. Returns whether a block was actually there; `key` is
  the grid key checked (valid even on a miss, -1 only if the point falls
  outside the packable grid range).

### ShipInput

<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipInput -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipInput.ThrustAxis -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipInput.Steer -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipInput.FirePressed -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipInput.ShipInput -->

One tick of player intent. A value type, so it is trivially serializable
for the netcode and trivially constructible in tests.

- `ThrustAxis`: main thrust axis, -1..1: positive fires forward thrusters,
  negative fires retro thrusters, zero fires neither.
- `Steer`: steering axis, -1..+1.
- `FirePressed`: fire was PRESSED this tick (edge, not level).

### BlockContext

<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/BlockContext.cs::BlockContext -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/BlockContext.cs::BlockContext.Ship -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/BlockContext.cs::BlockContext.Key -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/BlockContext.cs::BlockContext.Block -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/BlockContext.cs::BlockContext.LocalCenter -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/BlockContext.cs::BlockContext.WorldCenter -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/BlockContext.cs::BlockContext.Facing -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/BlockContext.cs::BlockContext.Dt -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/BlockContext.cs::BlockContext.Input -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/BlockContext.cs::BlockContext.World -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/BlockContext.cs::BlockContext.AddForceLocal -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/BlockContext.cs::BlockContext.Throttle -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/BlockContext.cs::BlockContext.TickCooldown -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/BlockContext.cs::BlockContext.ResetCooldown -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/BlockContext.cs::BlockContext.Fire -->

Everything an `IBlockBehaviour` needs to act for one block on one Step,
bundled so `ShipBody.Step` can fill in one instance per block without
allocating: a plain mutable struct passed by ref, never boxed, never
stored past the call that filled it in.

- `Facing`: ship-local facing direction decoded from `Block.Modifiers`,
  for directional blocks (Cannon/Fin). Thrusters ignore this: they have a
  fixed direction of their own.
- `World`: the game-side sink for anything a behaviour needs the world
  for (spawning projectiles, temporary gravity, targeting). Never null:
  `ShipBody` defaults it to `NullWorldSink.Instance`.
- `AddForceLocal`: accumulates `force` (ship-local) at this block's
  ship-local center via `Ship.AddForceAtPoint`, so it lands in both the
  force/torque accumulators and `AppliedForcesThisStep` like every other
  applied force.
- `Throttle`: ramps this block's own throttle toward `target` at a rate
  decided by its ramp-upgrade bits (`ThrusterUpgrades.RampRate`), stores
  the updated value keyed by `Key`, and returns it. Shared by every
  ramped channel (forward/retro thrust, fin steer) since one block
  belongs to exactly one behaviour and keys never collide.
- `TickCooldown`: ticks this block's cooldown down by `Dt` (floored at
  zero) and returns the seconds remaining afterward.
- `ResetCooldown`: sets this block's cooldown to `seconds`, e.g. right
  after firing.
- `Fire`: records a shot for the caller (`ShipController`) to spawn;
  `ShipBody` itself never instantiates a projectile object.

### IBlockBehaviour

<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/IBlockBehaviour.cs::IBlockBehaviour -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/IBlockBehaviour.cs::IBlockBehaviour.Step -->

One block's per-Step logic. This is the entire extension point: a new
weapon or thruster variant is one class implementing this interface plus
one `BehaviourRegistry.Register` call: `ShipBody.Step` never grows a new
switch case.

- `Step`: advance this one block by `ctx.Dt`: read/ramp its throttle or
  cooldown, apply forces/fire shots through `ctx`.

### IWorldSink

<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/IWorldSink.cs::IWorldSink -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/IWorldSink.cs::IWorldSink.SpawnProjectile -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/IWorldSink.cs::IWorldSink.AddTemporaryGravity -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/IWorldSink.cs::IWorldSink.TryNearestEnemy -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/IWorldSink.cs::IWorldSink.Ships -->

Everything a block behaviour needs from "the rest of the world": spawning
projectiles, dropping temporary gravity wells, and finding targets.
Implemented by the game layer (WorldSink); `ShipBody` and its behaviours
only ever see this interface, so the plain-C# simulation stays free of
UnityEngine and testable with `NullWorldSink`.

- `SpawnProjectile`: spawns whatever object represents `shot` (visual,
  physics, or both; the sink decides).
- `AddTemporaryGravity`: adds a temporary gravity well/anti-well to the
  world's gravity field for `seconds`.
- `TryNearestEnemy`: finds the nearest ship other than `self`, for a
  targeting behaviour (e.g. the seeking thruster). False (with default
  outs) when there is no other ship.
- `Ships`: every ship currently known to the sink.

### NullWorldSink

<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/NullWorldSink.cs::NullWorldSink -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/NullWorldSink.cs::NullWorldSink.Instance -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/NullWorldSink.cs::NullWorldSink.SpawnProjectile -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/NullWorldSink.cs::NullWorldSink.AddTemporaryGravity -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/NullWorldSink.cs::NullWorldSink.TryNearestEnemy -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/NullWorldSink.cs::NullWorldSink.Ships -->

The do-nothing `IWorldSink`: every `ShipBody` defaults to this so tests
(and any ship never wired to a real game scene) can `Step` without a null
check at every call site. `SpawnProjectile`/`AddTemporaryGravity` are
no-ops; `TryNearestEnemy` always fails; `Ships` is always empty.

- `Instance`: the single shared instance; this sink holds no state, so
  there is never a reason to allocate more than one.

### BehaviourRegistry

<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/BehaviourRegistry.cs::BehaviourRegistry -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/BehaviourRegistry.cs::BehaviourRegistry.Register -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/BehaviourRegistry.cs::BehaviourRegistry.Resolve -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/BehaviourRegistry.cs::BehaviourRegistry.RegisterDefaults -->

The `(TypeId, VariantId) -> IBlockBehaviour` table. This is the whole
extension mechanism: a new weapon or thruster registers itself here once
and `ShipBody.Step` picks it up through `Resolve`, with no switch
statement anywhere that needs editing. A static constructor calls
`RegisterDefaults` so tests (and anything else that touches this class
before a game bootstrap runs) always see the stock behaviours registered,
with no explicit setup call needed.

- `Register`: registers `behaviour` for the exact (typeId, variant) pair,
  overwriting whatever was registered there before: tests use that to
  swap in stubs.
- `Resolve`: looks up the behaviour for `block`'s (TypeId, variant bits),
  falling back to that type's variant 0 (its base behaviour) when the
  specific variant is not registered: an unrecognized variant id behaves
  like the plain block rather than doing nothing. Null when even variant
  0 has nothing registered (e.g. Core/Hull/Armor, which have no per-Step
  behaviour at all).
- `RegisterDefaults`: (re-)registers every stock behaviour shipped with
  the game. Public so a test that mutated the table with `Register` can
  restore the defaults afterward instead of leaking state across tests.

### ForwardThrusterBehaviour

<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/ForwardThrusterBehaviour.cs::ForwardThrusterBehaviour -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/ForwardThrusterBehaviour.cs::ForwardThrusterBehaviour.Step -->

The stock forward thruster: ramps toward full throttle while
`ThrustAxis > 0` and pushes straight ship-local +y. Thruster variant 0.

### RetroThrusterBehaviour

<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/RetroThrusterBehaviour.cs::RetroThrusterBehaviour -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/RetroThrusterBehaviour.cs::RetroThrusterBehaviour.Step -->

The stock retro thruster: ramps toward full throttle while
`ThrustAxis < 0` and pushes ship-local -y. RetroThruster variant 0.

### SeekingThrusterBehaviour

<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/SeekingThrusterBehaviour.cs::SeekingThrusterBehaviour -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/SeekingThrusterBehaviour.cs::SeekingThrusterBehaviour.Step -->

Thruster variant 1, the "inconvenient thruster": ramps like a normal
forward thruster while `ThrustAxis > 0`, but pushes toward the nearest
enemy ship's position instead of ship-forward, dragging the ship toward
the fight whether the pilot wants that or not. Falls back to ordinary
ship-forward thrust when no enemy is known (e.g. `NullWorldSink`, or a
lone ship in the world).

### FinBehaviour

<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/FinBehaviour.cs::FinBehaviour -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/FinBehaviour.cs::FinBehaviour.Step -->

The stock control fin: ramps its own throttle toward the steer channel
target, then pushes perpendicular to its facing with a sign chosen so the
resulting torque about the center of mass matches the sign of its current
ramped throttle: this is what lets torque fade out smoothly after the
steer key is released. Fin variant 0.

### CannonBehaviourBase

<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/CannonBehaviourBase.cs::CannonBehaviourBase -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/CannonBehaviourBase.cs::CannonBehaviourBase.BuildSpec -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/CannonBehaviourBase.cs::CannonBehaviourBase.Step -->

Shared fire-control for every cannon variant: ticks the per-block
cooldown, and on `FirePressed` while ready, builds the muzzle
origin/direction, records a `ShotRequest` with the spec from `BuildSpec`,
applies recoil, and resets the cooldown. Subclasses only decide WHAT gets
fired, never how firing/cooldown itself works: that is the whole point of
factoring this out, since the gravity gun and anti-gravity gun differ
from the stock cannon only in the `ProjectileSpec` they attach to the
shot.

- `BuildSpec`: the `ProjectileSpec` this variant's shot should carry.
- `Step`: ticks cooldown and fires (recording the shot and applying
  recoil) if the fire button was pressed this Step and the cooldown has
  elapsed.

### CannonBehaviour

<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/CannonBehaviour.cs::CannonBehaviour -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/CannonBehaviour.cs::CannonBehaviour.BuildSpec -->

The stock cannon: fires the ship's own Projectile spec unchanged. Cannon
variant 0.

### GravityGunBehaviour

<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/GravityGunBehaviour.cs::GravityGunBehaviour -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/GravityGunBehaviour.cs::GravityGunBehaviour.WellMu -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/GravityGunBehaviour.cs::GravityGunBehaviour.WellRadius -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/GravityGunBehaviour.cs::GravityGunBehaviour.WellSeconds -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/GravityGunBehaviour.cs::GravityGunBehaviour.BuildSpec -->

Cannon variant 1: fires a shot flagged GravityWell with a positive-Mu
`WellSpec`, so on impact the game drops a short-lived ATTRACTING well at
the hit point (via `IWorldSink.AddTemporaryGravity`).

### AntiGravityGunBehaviour

<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/AntiGravityGunBehaviour.cs::AntiGravityGunBehaviour -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/AntiGravityGunBehaviour.cs::AntiGravityGunBehaviour.WellMu -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/AntiGravityGunBehaviour.cs::AntiGravityGunBehaviour.WellRadius -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/AntiGravityGunBehaviour.cs::AntiGravityGunBehaviour.WellSeconds -->
<!-- frob:describes Assets/Scripts/Hullbreach.Ship/Behaviours/AntiGravityGunBehaviour.cs::AntiGravityGunBehaviour.BuildSpec -->

Cannon variant 2: fires a shot flagged GravityWell with a negative-Mu
`WellSpec`, so on impact the game drops a short-lived REPULSING well at
the hit point.
