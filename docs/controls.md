# Ship controls

Read this if you want the flight key map, how raw input becomes ship input,
or why the ship feels the same offline and online.

## Key map

| Action | Keys |
|---|---|
| Thrust / reverse | W/S or Up/Down |
| Steer | A/D or Left/Right |
| Fire | Space (offline: also Fire1; online: also left click) |
| Brake | S (with a retro thruster: also kills velocity) |
| Flight assist off | hold Left Shift |
| Build / fly toggle | Tab |
| Enlarge / shrink the radar map | M |
| Cycle overlay (offline only) | O |
| Reset to start (offline only) | R |

The wording lives once in `ShipControls` (`Hullbreach.Core`) and feeds the
offline status panel and the online HUD, so they cannot drift apart.

## From keys to the sim

1. Raw axes come from the legacy Input Manager (`Vertical`, `Horizontal`).
2. `ShipInputShaper` applies a 0.15 dead zone (rescaled, so no jump at the
   edge) and a 1.5 power curve: fine control near the dead zone, full
   deflection unchanged. Offline (`LegacyDemoInput`), online
   (`HullbreachNetCodeClientSystem`) and `NetDemo` all use it.
3. Fire is edge-triggered everywhere: one press, one shot request.
4. Fire is buffered for 0.15 s inside `ShipBody.Step`: a press during the
   cannon cooldown still fires when it is ready, and a shot clears the buffer.
5. Losing window focus or pausing clears latched input (`ShipController`)
   and sends neutral input online, so no key is ever stuck.

## How it flies

All of this lives in `FlightTuning` and `ShipBody.Step`, so the server, the
client prediction and the offline `ShipController` fly identically.

- Flight assist (default on): steer commands a spin rate, a gyro holds it,
  and releasing steer holds the heading instead of drifting into a spin. The
  rate cap scales with ship inertia: the 9 block starter turns at up to
  2.5 rad/s, a 13 wide hull at 0.8 rad/s. Hold Left Shift for raw fin physics
  (the old behaviour); it travels as input flag bit 0x04.
- Throttle ramps: on fast (about 0.4 s to full), off about 1.4 times slower.
  Fins ramp in 0.15 s and out in 0.10 s.
- Brake: S with a retro thruster adds a velocity-killing deceleration of 3x
  the retro acceleration, relative to the dominant body; it stops, never
  reverses.
- Measured on the starter ship at 50 Hz (`FlightFeelTests` logs these):

| | Before | After |
|---|---|---|
| Steer to first response | 20 ticks (0.40 s) | 2 ticks (0.04 s) |
| Time to 1 rad/s at full steer | 0.43 rad/s after 1 s | 0.52 s |
| Null a 1 rad/s spin | never (no damping online) | 0.66 s, no overshoot |
| 0.2 s steer tap | leaves a permanent spin | 0 residual |
| Thrust to 90% | 46 ticks (0.92 s) | 18 ticks (0.36 s) |
| Thrust off to 10% | 46 ticks | 25 ticks (1.4x the on time) |
| Brake sideways from 3 u/s | no effect | stops in 2.5 s, no reversal |
| Fire press 0.24 s after a shot | lost | fires at cooldown end |

## Online feel

- The client predicts its own ship; the throttle ramps survive a reconcile
  (`ShipBody.CopyThrottlesFrom`), so held thrust does not stutter.
- Reconcile corrections are hidden by `CorrectionSmoother`, a visual-only
  offset that decays in about 0.1 s; large corrections snap.
- The server keeps applying the last input for
  `ServerSimulation.InputHoldSeconds` (0.25 s) if packets are late, then
  goes neutral.

See `reference/hullbreach-ship.md`, `reference/hullbreach-net.md` and
`netcode.md` for the details.
