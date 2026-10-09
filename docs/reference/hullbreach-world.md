# Hullbreach.World reference

Per-type reference for `Assets/Scripts/Hullbreach.World`, linked from the code by
`// frob:doc docs/reference/hullbreach-world.md#<anchor>`. One heading per
public type; each heading carries the `frob:describes` lines for that
type and its public members. Architecture-level context lives in
[architecture.md](../architecture.md).

### GravityBody

<!-- frob:describes Assets/Scripts/Hullbreach.World/GravityBody.cs::GravityBody -->
<!-- frob:describes Assets/Scripts/Hullbreach.World/GravityBody.cs::GravityBody.DefaultSoftRadiusFactor -->
<!-- frob:describes Assets/Scripts/Hullbreach.World/GravityBody.cs::GravityBody.MinSoftRadius -->
<!-- frob:describes Assets/Scripts/Hullbreach.World/GravityBody.cs::GravityBody.Position -->
<!-- frob:describes Assets/Scripts/Hullbreach.World/GravityBody.cs::GravityBody.Mu -->
<!-- frob:describes Assets/Scripts/Hullbreach.World/GravityBody.cs::GravityBody.Radius -->
<!-- frob:describes Assets/Scripts/Hullbreach.World/GravityBody.cs::GravityBody.SoftRadius -->
<!-- frob:describes Assets/Scripts/Hullbreach.World/GravityBody.cs::GravityBody.SurfaceRestitution -->
<!-- frob:describes Assets/Scripts/Hullbreach.World/GravityBody.cs::GravityBody.GravityBody -->

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

<!-- frob:describes Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField -->
<!-- frob:describes Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField.MaxAcceleration -->
<!-- frob:describes Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField.Count -->
<!-- frob:describes Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField.Add -->
<!-- frob:describes Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField.Remove -->
<!-- frob:describes Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField.TryGetPermanent -->
<!-- frob:describes Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField.Clear -->
<!-- frob:describes Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField.AddTemporary -->
<!-- frob:describes Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField.Tick -->
<!-- frob:describes Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField.AccelerationAt -->
<!-- frob:describes Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField.AccelerationMagnitude -->
<!-- frob:describes Assets/Scripts/Hullbreach.World/GravityField.cs::GravityField.TryContact -->

A field of gravitating bodies. Bodies are kept in one dense list (permanent
bodies added via `Add`, temporary ones via `AddTemporary`) so
`AccelerationAt` and `TryContact` are a single allocation-free pass with
deterministic (insertion) order, important both for determinism across
machines and so `ShipBody.ContactsThisStep` is reproducible.

- `DefaultMaxAcceleration` (40) is what `new GravityField()` uses; the
  constructor argument is how [GravityConfig](#gravityconfig) sets it.
- `MaxAcceleration`: read-only after construction (it used to be a public
  field), so client and server cannot be tuned apart at runtime. Ceiling on
  the SUMMED acceleration magnitude returned
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

### GravityConfig

The engine-free gravity constants, so tuning is a data edit and the Unity
client and the server cannot disagree (S40, T-0088). `GravityConfig.Default`
equals the values that used to be hard-coded: `MaxAcceleration` 40,
`SurfaceRestitution` 0.2, no planets. `BuildField()` is the one place a
`GravityField` is made from it; `GravityWorld` and `ServerSimulation` both
call it. `Planets` are `PlanetConfig` values (position, `Mu`, `Radius`,
optional soft-radius factor; `ToBody` applies the `GravityBody` soft-radius
floor once for everybody). Visuals (sprite, color, scale) are not physics
and stay on the Unity component.

The text format is one `key = value` per line, `#` comments:

```
max_acceleration = 40
surface_restitution = 0.2
planet = 0 -60 900 18
planet = 45 20 120 6 1.5
```

`TryParse` never throws: it returns false with a `line N: ...` message for
an unknown key, a malformed line, or an out-of-range value
(`max_acceleration` must be > 0, `surface_restitution` in 0..1, a planet
needs positive `mu` and `radius`). `TryLoad` reads a file path for the
headless server; the client reads the same file as a Unity `TextAsset`
(`Assets/Config/Gravity.txt`). Arena bounds are not in this file yet.

### ArenaBounds

A circular arena edge (`Center`, `Radius`) that returns a point to the
inside with a soft spring instead of a wall or damage (S41, T-0089).
Immutable, so one instance is shared by every `ShipBody` on a client and by
`ServerSimulation`, which is what keeps prediction and the authoritative
step in agreement. Inside `Radius` it does nothing.

- `PushBackAcceleration(point, velocity)`: zero inside; outside, an inward
  spring `Stiffness * (distance - Radius)` plus damping `Damping` times the
  OUTWARD speed only, clamped to `MaxAcceleration` (units/s^2). Damping is
  one-sided on purpose: going out the point is critically damped (the
  defaults make `Stiffness == Damping`, so it stops within a few units of
  the line); coming back the spring is undamped, so the point crosses the
  edge moving inward and ends up inside rather than creeping toward the
  line forever.
- `Contains`: whether a point is inside or on the edge.
- `ShipBody.Arena` adds this acceleration to `Velocity` directly, never to
  the force accumulator, `LastLinearAcceleration` or `AppliedForcesThisStep`:
  the structural solver therefore sees no load from the push-back and no
  block takes damage from it. The point tested is the ship's center of
  mass.

### OrbitHelper

<!-- frob:describes Assets/Scripts/Hullbreach.World/OrbitHelper.cs::OrbitHelper -->
<!-- frob:describes Assets/Scripts/Hullbreach.World/OrbitHelper.cs::OrbitHelper.CircularOrbitVelocity -->

Helpers for placing something into a circular orbit around a `GravityField`
body, used by `DemoMode`'s "reset to orbit" key so a player can start (or
return to) a stable pass without hand-tuning a velocity.

- `CircularOrbitVelocity`: the world-space velocity that puts a massless
  object at `position` into a circular orbit around the body at
  `bodyIndex` inside `field`: `v = sqrt(a(r) * r)` tangential,
  counter-clockwise (rotate the outward radial direction +90 degrees),
  where `a(r)` is `GravityField`'s softened acceleration law (so an orbit
  that dips inside `SoftRadius` still balances the actual pull applied
  there, not the un-softened `Mu / r^2`). Returns zero if the index is out
  of range or `position` coincides with the body (undefined orbit).
