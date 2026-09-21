# The demo scene

`Assets/Scenes/DemoScene.unity`, registered as the first
`EditorBuildSettings` entry alongside `RocketScene`. Hand-authored YAML
(commit `f680856`, "author DemoScene with a flyable/buildable player
ship") -- see the checklist at the bottom before you trust anything about
it beyond "it parses and CI's tree check passes".

## Object / fileID layout

Five root GameObjects, fileIDs grouped by the thousands digit so it is
obvious which object a component belongs to just from its fileID:

| fileID prefix | Object | Components (in order) |
| --- | --- | --- |
| `2000xxx` | (camera scaffolding: Transform, Camera, etc. -- `fileID 1` block for the built-in camera setup) | -- |
| n/a | **Main Camera** (`2000001`) | Transform, Camera, GUILayer/AudioListener, **`CameraFollow`** (target = PlayerShip's Transform, smoothing = 5) |
| `2001xxx` | **Demo** | Transform, **`DemoMode`**, **`ProjectileSpawner`**, **`BuilderHud`**, **`WorldSink`** |
| `2002xxx` | **PlayerShip** | Transform, `Rigidbody2D`, **`ShipController`**, **`ShipRenderer`**, **`ShipCollider`**, **`ShipStructure`**, **`BuilderController`** |
| `2003xxx` | **TargetShip** | Transform, `Rigidbody2D`, **`ShipController`**, **`ShipRenderer`**, **`ShipCollider`**, **`ShipStructure`** (no `BuilderController` -- it is never edited) |
| `2004xxx` | **Gravity** | Transform, **`GravityWorld`** |
| `2005xxx` | **Powerups** | Transform, **`PowerupSpawner`** |

`DemoMode` (on **Demo**, fileID `2001003`) wires everything by direct
fileID reference in its Inspector fields: `playerShip: {fileID: 2002004}`
(PlayerShip's `ShipController`), `playerBody: {fileID: 2002003}`
(PlayerShip's `Rigidbody2D`), `builder: {fileID: 2002008}`
(PlayerShip's `BuilderController`), `builderHud: {fileID: 2001005}`
(Demo's own `BuilderHud`), `playerRenderer: {fileID: 2002005}`,
`playerStructure: {fileID: 2002007}`. It also carries `startInOrbit: 1`,
`orbitStartPosition: {x: 0, y: -30}`, `orbitBodyIndex: 0` -- so pressing
**R** in Fly mode drops the player onto a circular orbit around the big
planet (index 0 in `Gravity`'s `GravityWorld.planets` list) starting at
`(0, -30)`, rather than resetting to dead rest at the origin.

`WorldSink` (Demo, fileID `2001006`) points `spawner:` at `2001004`, the
`ProjectileSpawner` on the same object -- this is the concrete
`IWorldSink` implementation every `ShipBody` on a ship in this scene uses
(see `docs/architecture.md`'s note on `IWorldSink`/`NullWorldSink`).

`ProjectileSpawner` (Demo, fileID `2001004`) carries the default
`ProjectileSpec` numbers directly in the Inspector: `speed: 20, impulse:
2, damage: 25, lifetime: 3, radius: 0.1` -- the same defaults as
`ProjectileSpec.Default` in code, duplicated here because the scene
authors its own spawner rather than reading the ship's spec (worth
noting if the two ever need to diverge on purpose).

## Positions and bodies

- **PlayerShip** starts at the world origin, frozen (`Rigidbody2D.simulated
  = false`) while in Build mode.
- **TargetShip** sits fixed at `(0, 24)`, a stationary 3x3 hull-plus-armor
  block with no `BuilderController` (never edited), there purely as
  something to shoot at.
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

## Overlays and the HUD

`ShipRenderer.Overlay` (`OverlayMode`): `None` (plain per-type color),
`Stress` (green-to-red by `max(DuctileRatio, BrittleRatio)`),
`LoadBearing` (magenta on articulation points), `Damage` (white-to-black
by `DamageFraction`), `Buckling` (blue-to-red by `BlockStress
.BucklingRatio`). The always-on `OnGUI` panel (bottom-left) shows mode,
per-mode controls, current overlay, mass, block count, and in Fly mode:
speed, angular speed, critical load factor (`inf` when nowhere near
buckling), and one line per active powerup (`DemoMode.DrawActivePowerups`,
e.g. "Cannon (0,2): Gravity gun 6.2 s").

## Resetting

`R` in Fly mode calls `ShipController.ResetTo`/`ResetToOrigin`. This only
resets the *player* ship's position/velocity/rotation -- it does not
re-run `BuilderSession` or restore the ship to its as-authored block
layout, so a ship that lost blocks to combat or a buckling failure stays
lost after a reset. There is no scene-reset ("restart the whole demo")
control; reopening the scene in the editor (or `Ctrl+P`/`Ctrl+P` to stop
and restart Play mode) is the only full reset.

## "First open in Unity" checklist

Everything below was authored as hand-written YAML or hand-reasoned C#
by someone without a Unity install (see `README.md`'s "Where the project
actually is right now" and the commit body of `f680856`), and none of it
has ever been confirmed inside the actual editor. Whoever opens this
project in Unity 6000.0.43f1 for the first time should walk this list
before trusting the demo scene or the RocketScene handoff notes:

- [ ] `DemoScene.unity` opens without a "missing script" warning on any
      of the five root objects or their children -- every `m_Script` GUID
      above must resolve to the `.cs` file it names.
  - `frob:todo` equivalent: `TODO.md`'s "Open RocketScene in Unity and
    confirm the hand-edited ShipController and BuilderController wiring,
    then commit the regenerated metas" -- the same concern applies to
    DemoScene, which is newer and has never been opened at all.
- [ ] Every fileID cross-reference in `DemoMode`, `WorldSink`, and
      `ProjectileSpawner` resolves to the object it claims to (the table
      above) -- a typo'd fileID silently becomes a null Inspector
      reference that only shows up as a `Debug.LogError` at Play time or a
      `NullReferenceException`.
- [ ] `PlayerShip`'s block layout (core/hull/thrusters/retro/cannon/fins,
      per the `f680856` commit body) actually matches what the grid
      contains once `ShipController.Awake` seeds it -- confirm in the
      Scene view, not just by reading YAML.
- [ ] `Rigidbody2D.simulated` correctly starts `false` on `PlayerShip` in
      Build mode and flips on entering Fly mode (`DemoMode.ApplyState`);
      confirm the ship does not drift or fall during Build.
- [ ] `EditorBuildSettings.asset` still lists the missing `MainMenu`/
      `GameSceneOld`/`GameResources` scenes alongside `RocketScene` and
      `DemoScene` (per `README.md`) -- confirm a build actually launches
      `DemoScene` first rather than erroring on a missing scene index.
- [ ] URP materials/shaders compile cleanly on first import ("everything
      is pink" in `README.md`'s "Things that bite people").
- [ ] The generated 1x1 white sprite (`ShipRenderer.MakeSprite`) renders
      correctly under URP 2D -- confirm block colors and the powerup pulse
      actually look like intended tints, not the shader's error magenta.
- [ ] `PowerupSpawner`'s three presets spawn, are collectible, and respawn
      10 seconds later exactly once confirmed working in-editor (never
      run under Play mode by the authors).
- [ ] Buckling and stress overlays render a believable gradient on the
      3x3 `TargetShip` after a few shots -- the FE/buckling pipeline has
      only ever been exercised by `Assets/Tests/EditMode` (see
      `docs/testing.md`), never inside a live scene.
