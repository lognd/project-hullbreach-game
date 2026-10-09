# Stress-to-color mapping

How a block's failure ratio becomes a color, in the ship overlay and in
the hull warning banner, and where each rule lives. Written for S37
("See where my ship is about to fail", T-0078). Everything here is
current behaviour; nothing is proposed.

## The one input: the failure ratio

Every color below is a function of a per-block **failure ratio**, where
`1.0` means "failing now" and `0.0` means unloaded. The ratios come from
`StructuralSolver.BlockStresses` (`Hullbreach.Structure`):

| Ratio | Meaning |
| --- | --- |
| `DuctileRatio` | von Mises stress over the damage-softened yield stress |
| `BrittleRatio` | tensile principal over spall stress, or compressive principal over compressive strength, whichever is larger |
| `BucklingRatio` | the block's share of the critical buckling mode, 1.0 at the critical load factor |

The value both views use is the **largest of the three**:

```
ratio = max(DuctileRatio, BrittleRatio, BucklingRatio)
```

Because the solve is deterministic, every machine computes the same
ratios from the same inputs, so tinting is safe on a client (see
[architecture.md](architecture.md), "Stress criteria").

## Overlay: a continuous gradient (`ShipRenderer`)

`ShipRenderer.StressColor` (`Assets/Scripts/Hullbreach.Game/ShipRenderer.cs`),
used when `Overlay == OverlayMode.Stress`:

```
t = clamp(ratio, 0, 1)
t <  0.5 : lerp(green,  yellow, 2 * t)
t >= 0.5 : lerp(yellow, red,    2 * (t - 0.5))
```

`green`, `yellow` and `red` are Unity's `Color.green` (0, 1, 0),
`Color.yellow` (1, 0.92, 0.02) and `Color.red` (1, 0, 0). A linear
interpolation in RGB, so the anchor points are:

| ratio | color |
| --- | --- |
| 0.00 | green |
| 0.25 | yellow-green, about (0.5, 0.96, 0.01) |
| 0.50 | yellow |
| 0.75 | orange, about (1, 0.46, 0.01) |
| 1.00 and above | red (clamped; there is no color beyond failure) |

The overlay's ratio includes buckling because `ShipStructure` assigns
`ShipRenderer.ExtraRatioSource` to a lookup of `BucklingRatio`, and the
Stress case takes `max(ductile-or-brittle, ExtraRatioSource(key))`. A
`ShipRenderer` with no `ShipStructure` beside it has no extra source and
therefore ignores buckling, and with no `Solver` set it draws every block
green.

On top of any overlay (even `None`), a block whose ratio is at or above
`ShipRenderer.FlashRatioThreshold` pulses toward alarm red (1, 0.1, 0.1).
`DemoMode` sets the threshold to its `CriticalRatio`, 0.8, each frame.

The other overlays are separate mappings and not part of this one:
`Buckling` is `lerp(blue, red, clamp(BucklingRatio))`, `Damage` is white to
black by `DamageFraction`, `LoadBearing` is magenta on articulation points.

## Banner: three bands (`DemoMode` + `HullWarningModel`)

`DemoMode.UpdateWarning` takes the maximum of the same three-way ratio over
every block in the player's solver and picks a band. The thresholds are
public constants on `DemoMode`:

| Band | Condition | Banner color (`HudColor`) |
| --- | --- | --- |
| `Ok` | max ratio below `StrainRatio` (0.5) | `WarningOkGreen`, (0.3, 1, 0.4) |
| `Strain` | `StrainRatio` (0.5) up to `CriticalRatio` (0.8) | `WarningStrainYellow`, (1, 0.85, 0.2) |
| `Critical` | max ratio at or above `CriticalRatio` (0.8), **or** the solver's critical load factor below `CriticalLoadFactorFloor` (1.5) | red (1, 0.15, 0.15), pulsed by `HullWarningModel.Build` |

The banner's band edges are chosen to line up with the overlay: the
Strain band starts at 0.5, exactly where the gradient reaches yellow, and
Critical starts at 0.8, the same value as the per-block flash. The one
banner trigger with no per-block counterpart is the load-factor floor,
which is a ship-wide buckling margin, not a block ratio.

## Build mode and combat

There is one tint function. Build mode and Fly mode are the same
`ShipRenderer` on the same ship, so a block has the same color for the same
ratio in both. What differs:

- The overlay is only cycled in Fly (`DemoMode.Update` reads the `O` key
  inside the `State == Fly` branch). The setting persists across Tab, so a
  ship put in the Stress overlay and then switched to Build keeps showing it.
- Build mode freezes the ship (`ShipController.SimulationEnabled = false`,
  so `ShipBody.Step` stops and `AppliedForcesThisStep` keeps the last flight
  tick's forces). `ShipStructure.FixedUpdate` has no such guard and keeps
  solving, so in Build the overlay shows the stress of the CURRENT blocks
  under the LAST flight loads, and re-solves after each placement or
  removal. It does not show a load the player has not flown.
- The banner is a Fly-mode element only (top-center, hidden in Build).

## Known gaps

- **No colorblind-safe palette.** Green to red is the worst pair for
  red-green color blindness. S37 criterion 3 (T-0077) is open. The
  gradient is hard-coded in `ShipRenderer.StressColor` with Unity types,
  while the banner uses `HudColor` in the engine-free `Hullbreach.Hud`;
  the two share their thresholds only by convention (0.5, 0.8). A palette
  work item should move the mapping into `Hullbreach.Hud` once, so a plain
  test can pin the anchors above and both views read it.
- The thresholds 0.5 and 0.8 are duplicated between `DemoMode`
  (`StrainRatio`, `CriticalRatio`) and the gradient's midpoint; they are
  not derived from each other.
