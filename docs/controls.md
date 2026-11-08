# Ship controls

Read this if you want the flight key map, how raw input becomes ship input,
or why the ship feels the same offline and online.

## Key map

| Action | Keys |
|---|---|
| Thrust / reverse | W/S or Up/Down |
| Steer | A/D or Left/Right |
| Fire | Space (offline: also Fire1; online: also left click) |
| Build / fly toggle | Tab |
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
4. Losing window focus or pausing clears latched input (`ShipController`)
   and sends neutral input online, so no key is ever stuck.

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
