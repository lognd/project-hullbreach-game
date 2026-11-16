# Hullbreach.World reference

Per-type reference for `Assets/Scripts/Hullbreach.World`, linked from the code by
`// frob:doc docs/reference/hullbreach-world.md#<anchor>`. One heading per
public type; each heading carries the `describes:` lines for that
type and its public members. Architecture-level context lives in
[architecture.md](../architecture.md).

### ArenaBounds

<!-- describes: Assets/Scripts/Hullbreach.World/ArenaBounds.cs::ArenaBounds -->
<!-- describes: Assets/Scripts/Hullbreach.World/ArenaBounds.cs::ArenaBounds.Center -->
<!-- describes: Assets/Scripts/Hullbreach.World/ArenaBounds.cs::ArenaBounds.Radius -->
<!-- describes: Assets/Scripts/Hullbreach.World/ArenaBounds.cs::ArenaBounds.Margin -->
<!-- describes: Assets/Scripts/Hullbreach.World/ArenaBounds.cs::ArenaBounds.Strength -->
<!-- describes: Assets/Scripts/Hullbreach.World/ArenaBounds.cs::ArenaBounds.ArenaBounds -->
<!-- describes: Assets/Scripts/Hullbreach.World/ArenaBounds.cs::ArenaBounds.Depth -->
<!-- describes: Assets/Scripts/Hullbreach.World/ArenaBounds.cs::ArenaBounds.InPushZone -->
<!-- describes: Assets/Scripts/Hullbreach.World/ArenaBounds.cs::ArenaBounds.PushAcceleration -->

Circular arena edge (S41-1). Inside `Radius - Margin` nothing happens; across
the `Margin` ramp the inward acceleration grows linearly to `Strength`, and
beyond `Radius` it stays at `Strength`. `Depth` is that 0..1 ramp value,
`InPushZone` is `Depth > 0`, and `PushAcceleration` points at `Center`.
A `Margin` of 0 is a hard switch at `Radius`. The server applies it as a
velocity change only, so the structural solver never sees it and no block
is damaged.

### GravityBody

<!-- describes: Assets/Scripts/Hullbreach.World/GravityBody.cs::GravityBody -->
<!-- describes: Assets/Scripts/Hullbreach.World/GravityBody.cs::GravityBody.DefaultSoftRadiusFactor -->
<!-- describes: Assets/Scripts/Hullbreach.World/GravityBody.cs::GravityBody.MinSoftRadius -->
<!-- describes: Assets/Scripts/Hullbreach.World/GravityBody.cs::GravityBody.Position -->
<!-- describes: Assets/Scripts/Hullbreach.World/GravityBody.cs::GravityBody.Mu -->
<!-- describes: Assets/Scripts/Hullbreach.World/GravityBody.cs::GravityBody.Radius -->
<!-- describes: Assets/Scripts/Hullbreach.World/GravityBody.cs::GravityBody.SoftRadius -->
<!-- describes: Assets/Scripts/Hullbreach.World/GravityBody.cs::GravityBody.SurfaceRestitution -->
<!-- describes: Assets/Scripts/Hullbreach.World/GravityBody.cs::GravityBody.GravityBody -->

One gravitating point mass (a planet, moon, or temporary gravity-gun well):
position, gravitational parameter (G*M, so the field never has to know G or
M separately), a physical radius used both to soften the acceleration near
the center and to test surface contact, and the restitution a ship should
bounce with when it hits the surface.

- `DefaultSoftRadiusFactor`: default multiplier of `Radius` used to derive
  `SoftRadius` when a caller does not specify one explicitly.
- `MinSoftRadius`: floor applied to `SoftRadius` so even a tiny body (e.g. a
  gravity-gun well with sub-unit `Radius`) still softens over a perceptible
  distance instead of behaving like a near-singularity.
- `Mu`: gravitational parameter G*M. Acceleration at distance r (outside
  `SoftRadius`) is `Mu / r^2` toward `Position`.
- `Radius`: physical radius, used by the contact test as the surface
  distance.
- `SoftRadius`: distance below which acceleration is softened: at and
  beyond `SoftRadius` the law is the ordinary `Mu / r^2`; inside it,
  acceleration scales linearly from that value down to zero at the center
  (gameplay choice over physical realism, so close encounters with a planet
  or a gravity-gun well never blow up). Defaults to
  `max(Radius * DefaultSoftRadiusFactor, MinSoftRadius)`.
- `SurfaceRestitution`: coefficient of restitution (0..1) used by
  `ShipBody`'s contact response when a ship lands on this body's surface.
- The four-argument constructor builds a gravity body with an explicit
  `SoftRadius`; a convenience over positional struct initializer syntax at
  call sites. The five-argument overload derives `SoftRadius` from `Radius`
  via the default factor/floor, for the common case where a caller has no
  reason to override it.

### GravityField

<!-- describes: Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField -->
<!-- describes: Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField.MaxAcceleration -->
<!-- describes: Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField.Count -->
<!-- describes: Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField.PermanentCount -->
<!-- describes: Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField.Add -->
<!-- describes: Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField.Remove -->
<!-- describes: Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField.TryGetPermanent -->
<!-- describes: Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField.Clear -->
<!-- describes: Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField.AddTemporary -->
<!-- describes: Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField.Tick -->
<!-- describes: Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField.AccelerationAt -->
<!-- describes: Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField.AccelerationMagnitude -->
<!-- describes: Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField.TryContact -->

A field of gravitating bodies. Bodies are kept in one dense list (permanent
bodies added via `Add`, temporary ones via `AddTemporary`) so
`AccelerationAt` and `TryContact` are a single allocation-free pass with
deterministic (insertion) order, important both for determinism across
machines and so `ShipBody.ContactsThisStep` is reproducible.

- `MaxAcceleration`: ceiling on the SUMMED acceleration magnitude returned
  by `AccelerationAt`, applied after every body's contribution is added so
  overlapping strong wells cannot stack past a playable pull (gameplay
  choice: a ship should never get yanked harder than this regardless of how
  many wells overlap it). Units/s^2.
- `Add`: adds a permanent body (a planet/moon placed in the scene). Returns
  its index among permanent bodies at the time of insertion; note this
  index is NOT stable across `Remove` calls.
- `TryGetPermanent`: reads the permanent body at `index` (as returned by
  `TryContact`'s `bodyIndex`), for callers such as `OrbitHelper` that need
  that body's `Position`/`Mu`.
- `AddTemporary`: adds a body that expires after `seconds` of `Tick` calls.
  Used by the gravity-gun powerup to drop a short-lived well without the
  caller having to track and remove it itself.
- `Tick`: advances every temporary body's remaining lifetime by `dt` and
  drops any that have expired. Iterates backward so removal during the pass
  is safe and allocation-free.
- `AccelerationAt`: net gravitational acceleration at `worldPoint`: sum over
  every body of its softened pull toward that body (see
  `GravityBody.SoftRadius` and `AccelerationMagnitude`), then clamped to
  `MaxAcceleration` in magnitude so stacked wells cannot exceed the same cap
  a single strong one would. Allocation-free: iterates the two lists
  directly.
- `AccelerationMagnitude`: softened inverse-square law shared by
  `GravityField` and `OrbitHelper`: `Mu / r^2` at and beyond `SoftRadius`;
  inside it, scales LINEARLY from that same value at `SoftRadius` down to
  zero at the center (`dist == 0`), so the pull is continuous at
  `SoftRadius` and never diverges. `dist` must be `>= 0`.
- `TryContact`: tests whether `worldPoint` is within `clearance` of any
  body's surface (i.e. distance to center `<= Radius + clearance`). Returns
  the nearest such body (by penetration depth), its outward surface normal
  at that point, and how far inside the clearance shell the point is
  (`Radius + clearance - distance`; positive means penetrating). False
  (with default outs) when no body is in range.

### OrbitHelper

<!-- describes: Assets/Scripts/Hullbreach.World/OrbitHelper.cs::OrbitHelper -->
<!-- describes: Assets/Scripts/Hullbreach.World/OrbitHelper.cs::OrbitHelper.TryCircularOrbitVelocity -->

Helpers for placing something into a circular orbit around a `GravityField`
body, used by `DemoMode`'s "reset to orbit" key so a player can start (or
return to) a stable pass without hand-tuning a velocity.

- `TryCircularOrbitVelocity`: the world-space velocity that puts a massless
  object at `position` into a circular orbit around the body at
  `bodyIndex` inside `field`: `v = sqrt(a(r) * r)` tangential,
  counter-clockwise (rotate the outward radial direction +90 degrees),
  where `a(r)` is `GravityField`'s softened acceleration law (so an orbit
  that dips inside `SoftRadius` still balances the actual pull applied
  there, not the un-softened `Mu / r^2`). Returns false (velocity zero)
  if the index is out of range, `position` coincides with the body, or the
  body's `Mu <= 0` (a repeller has no circular orbit); callers log and skip.

### PlanetCrashConfig

<!-- describes: Assets/Scripts/Hullbreach.World/PlanetCrashConfig.cs::PlanetCrashConfig -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetCrashConfig.cs::PlanetCrashConfig.SafeLandingSpeed -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetCrashConfig.cs::PlanetCrashConfig.CatastrophicSpeed -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetCrashConfig.cs::PlanetCrashConfig.DamagePerSpeed -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetCrashConfig.cs::PlanetCrashConfig.CoreDamageScale -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetCrashConfig.cs::PlanetCrashConfig.CoreMaxDamagePerImpact -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetCrashConfig.cs::PlanetCrashConfig.RestSpeed -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetCrashConfig.cs::PlanetCrashConfig.MaxRestitution -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetCrashConfig.cs::PlanetCrashConfig.SweepStep -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetCrashConfig.cs::PlanetCrashConfig.MaxSubsteps -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetCrashConfig.cs::PlanetCrashConfig.AtmosphereDepthFactor -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetCrashConfig.cs::PlanetCrashConfig.AtmosphereMinDepth -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetCrashConfig.cs::PlanetCrashConfig.AtmosphereSafeSpeed -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetCrashConfig.cs::PlanetCrashConfig.AtmosphereDrag -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetCrashConfig.cs::PlanetCrashConfig.HeatSpeed -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetCrashConfig.cs::PlanetCrashConfig.HeatDamagePerSecond -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetCrashConfig.cs::PlanetCrashConfig.HeatMaxDamagePerSecond -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetCrashConfig.cs::PlanetCrashConfig.WarningSeconds -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetCrashConfig.cs::PlanetCrashConfig.Default -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetCrashConfig.cs::PlanetCrashConfig.Validate -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetCrashConfig.cs::PlanetCrashConfig.AtmosphereDepth -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetCrashConfig.cs::PlanetCrashConfig.AtmosphereDensity -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetCrashConfig.cs::PlanetCrashConfig.EffectiveRestitution -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetCrashConfig.cs::PlanetCrashConfig.ImpactDamage -->

Every tunable of planet crashes and the thin atmosphere, in one struct with
`Default()` and `Validate()` (null when valid). The server reads an optional
`planets` block from the arena JSON (see `ArenaConfig`). Rules:

- Touching a planet at or below `SafeLandingSpeed` (relative normal speed)
  costs nothing. Above it, each touching block takes
  `(speed - SafeLandingSpeed) * DamagePerSpeed / sqrt(touchingBlocks)`, so a
  wide landing spreads the hit.
- The core takes `CoreDamageScale` of that, capped at
  `CoreMaxDamagePerImpact` (below 255), so a graze or an ordinary hard landing
  can never breach it. Only a hit at or above `CatastrophicSpeed` applies the
  full, uncapped amount and can kill a core that is on the contact side.
- Bounces are clamped to `MaxRestitution` and are zero below `RestSpeed`, so
  a resting ship does not jitter and a bounce never adds energy.
- The bounce also fades with impact speed (`EffectiveRestitution`): full up to
  `SafeLandingSpeed`, linearly down to none at `CatastrophicSpeed`, so a hard
  crash dead-stops with damage instead of rebounding out of the planet well.
- `SweepStep` and `MaxSubsteps` bound the per-tick substepping that stops a
  fast ship tunnelling through the surface.
- The atmosphere is a band above the surface (`AtmosphereDepth`, at least
  `AtmosphereMinDepth`) with density 1 at the surface falling quadratically to
  0 at the top. Below `AtmosphereSafeSpeed` it does nothing, so orbits are free.
  Faster ships feel drag (`AtmosphereDrag`); above `HeatSpeed` the leading
  blocks heat for `HeatDamagePerSecond` (up to `HeatMaxDamagePerSecond`). The
  core is never heated.

### PlanetProximity

<!-- describes: Assets/Scripts/Hullbreach.World/PlanetProximity.cs::ImpactSeverity -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetProximity.cs::PlanetProximity -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetProximity.cs::PlanetProximity.HasBody -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetProximity.cs::PlanetProximity.BodyIndex -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetProximity.cs::PlanetProximity.SurfaceNormal -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetProximity.cs::PlanetProximity.Altitude -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetProximity.cs::PlanetProximity.ClosingSpeed -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetProximity.cs::PlanetProximity.TangentialSpeed -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetProximity.cs::PlanetProximity.TimeToImpact -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetProximity.cs::PlanetProximity.PredictedImpactSpeed -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetProximity.cs::PlanetProximity.AtmosphereDensity -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetProximity.cs::PlanetProximity.StopDistance -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetProximity.cs::PlanetProximity.MustBrake -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetProximity.cs::PlanetProximity.Heating -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetProximity.cs::PlanetProximity.Severity -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetProximity.cs::PlanetProximity.IsSafe -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetProximity.cs::PlanetProximity.Classify -->
<!-- describes: Assets/Scripts/Hullbreach.World/PlanetProximity.cs::PlanetProximity.TimeToCover -->

Read-only picture of the nearest planet for the HUD, filled by
`ShipBody.QueryPlanetProximity()`: altitude of the lowest block, closing and
tangential speed, time to impact (gravity included), the predicted impact
speed, atmosphere density, whether the ship is heating, the stop distance of
full thrust and `MustBrake`, plus an `ImpactSeverity` (None, Safe, Caution,
Danger, Catastrophic) and `IsSafe`. It never changes the ship.

### StormZone

<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormPhase -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormPhase.HoldSeconds -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormPhase.ShrinkSeconds -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormPhase.TargetRadius -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormPhase.DamagePerSecond -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormPhase.StormPhase -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormConfig -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormConfig.MinWarningSeconds -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormConfig.TickRate -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormConfig.StartRadius -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormConfig.GraceSeconds -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormConfig.WarningFraction -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormConfig.HardLimitMargin -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormConfig.SuddenDeathDamagePerSecond -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormConfig.Phases -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormConfig.StormConfig -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormConfig.Default -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormConfig.WithTickRate -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormConfig.TotalSeconds -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormConfig.Validate -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormStage -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormState -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormState.Stage -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormState.PhaseIndex -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormState.Center -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormState.Radius -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormState.NextCenter -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormState.NextRadius -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormState.TicksUntilNextStage -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormState.TicksUntilShrink -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormState.DamagePerTick -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormState.WarningFraction -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormState.HardLimitCenter -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormState.HardLimitRadius -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormState.StormState -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormState.IsSuddenDeath -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormState.StageName -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormState.Contains -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormState.SignedDistance -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormState.InWarningBand -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormState.PullDirection -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormState.WithinHardLimit -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormState.DamageFor -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormZone -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormZone.Config -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormZone.FinalCenter -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormZone.SuddenDeathTick -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormZone.StormZone -->
<!-- describes: Assets/Scripts/Hullbreach.World/StormZone.cs::StormZone.At -->

Shrinking safe ring for the 1v1 duel (S41). It exists to stop kiting and force
engagement, not to kill: a fight should end by core breach, and the storm only
decides stalemates. `StormZone.At(tick)` is a pure function of
(`StormConfig`, arena centre, seed, match tick), so the server and clients
agree with no extra traffic. Schedule: Grace (no storm) -> [Hold -> Shrink]*
-> SuddenDeath. Each Hold warns at least 20 s before its shrink. The ring
drifts toward a seed-chosen candidate centre (a planet) for a gravity-well
finale, and each ring is clamped inside the previous one.

`StormState` is the read-only HUD/minimap view: `Center`/`Radius`,
`NextCenter`/`NextRadius`, `PhaseIndex`, `Stage`/`StageName`,
`TicksUntilNextStage`, `TicksUntilShrink`, `Contains`, `SignedDistance`
(positive inside), `InWarningBand` (inside, no damage), `PullDirection`
(unit vector to the safe centre) and `WithinHardLimit` (fixed disc of
`StartRadius + HardLimitMargin`; projectiles/debris outside it despawn).
`DamageFor(point, isCore)` is HP this tick for a block at that point: zero
inside, `DamagePerSecond / TickRate` outside, escalating per phase; the core
is spared until sudden death, when the ring is closed and the core takes
`SuddenDeathDamagePerSecond`, so a match always ends. All durations, radii and
damage rates live in `StormConfig` (`Default()` ends at 550 s, under the
600 s server time limit). `StormConfig.WithTickRate` re-rates a schedule. The
server applies the damage and despawns projectiles; see
[ServerSimulation](hullbreach-net.md#arenaconfig) and
[StormStarted](hullbreach-net.md#stormstarted).


### PickupField

Deterministic pickups for the block economy: crates (a pure function of seed and wave index: a
planet crate, a storm-edge rare pair, or an open-field mirrored pair; first at 20 s, then every
12 s, 75 s lifetime) and salvage dropped from wrecks (40 s lifetime, cap 96, oldest culled).
Pickups despawn on expiry, planet contact or the storm hard limit. `Reach` decides whether a ship
can grab one (arm delay 1.5 s, relative speed at most 4, within the 2.0 magnet of a block centre,
no planet in between). Planet crates are asymmetric unless the arena is.
