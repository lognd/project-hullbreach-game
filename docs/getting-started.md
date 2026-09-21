# Getting started (no programming needed)

This guide is for someone who has never used Unity or written code. It
walks you through installing the editor, playing the demo, changing
numbers to see what happens, and making small additions by copying
something that already exists. You do not need to understand the physics
or the code to follow it.

A few words this guide uses a lot, defined once here:

- **Unity**: the program (called "the editor") that runs the game while
  you are working on it, and that turns the project into a real game you
  can double-click and play.
- **The Inspector**: the panel, normally on the right side of the editor
  window, that shows the settings of whatever object you have clicked on.
- **The Hierarchy**: the panel, normally on the left, that lists every
  object in the current scene, like a table of contents.
- **The Project panel**: the panel, normally at the bottom, that lists
  every file in the project, like a file browser.
- **A scene**: one "level" or "screen" of the game, saved as a single
  file. This project's playable scene is called `DemoScene`.
- **A GameObject**: one thing in a scene, for example the player's ship,
  a planet, or the camera. GameObjects are listed in the Hierarchy.
- **A component**: one piece of behavior or data attached to a
  GameObject, shown as a box in the Inspector. A GameObject can have
  several components at once (for example, the player ship has a
  component that draws it and a separate one that flies it).
- **Play mode**: pressing the triangular Play button at the top of the
  editor runs the game inside the editor window itself, so you can test
  changes without building a separate program.

## 1. Install and open the project

1. Go to unity.com and download **Unity Hub**. Unity Hub is not the game
   editor itself; it is a small launcher that manages which editor
   versions you have installed and which projects you have opened.
2. Install Unity Hub and open it. You may be asked to sign in or create a
   free Unity account; a personal account is fine.
3. In Unity Hub, open the **Installs** tab and click **Install Editor**
   (or "Locate" if you already have installers). Pick version
   **6000.0.43f1**, exactly that version, not a newer or older one. This
   matters because the project's settings file
   (`ProjectSettings/ProjectVersion.txt`) records that exact version, and
   Unity offers to silently upgrade the whole project the first time you
   open it with a different version. An upgrade rewrites hundreds of
   settings files and can quietly break things that nobody has tested
   yet; matching the version avoids that entirely.
4. In Unity Hub, open the **Projects** tab and click **Add** (sometimes
   labeled "Add project from disk"). Browse to wherever you downloaded or
   cloned this repository and select that folder (the one containing a
   folder named `Assets`).
5. Click the project's name to open it. The first open can take several
   minutes: Unity is reading every file in `Assets` and building an
   internal cache. Let it finish.
6. Once the editor window opens, find the **Project panel** (usually at
   the bottom) and open the folders `Assets > Scenes`. Double-click
   `DemoScene` to open it. The Hierarchy panel (left side) should now
   list five objects: `Main Camera`, `Demo`, `PlayerShip`, `TargetShip`,
   `Gravity`, `Powerups`.
7. Press the **Play** button (the triangle icon at the top-center of the
   editor). The game now runs inside the editor. See section 2 for what
   to do once it is running.
8. Press **Play** again (the same button, which now looks pressed-in) to
   stop. Nothing you did while playing is saved to the project; Play mode
   is always safe to experiment in.

### Git basics, in five lines

Git is the tool that tracks every change to the project's files and lets
several people share one project without overwriting each other's work.
You do not need to understand how it works internally, just these five
habits:

1. **Pull before you start working**, so you have everyone else's latest
   changes first.
2. Make your changes (in the editor, or in a text file).
3. **Commit** your changes with a short message describing what you did,
   for example "add a third planet to the demo scene".
4. **Push** when you are done, so everyone else can get your changes.
5. If you are ever told to "force push," **ask a teammate for help
   first**: it can overwrite other people's work and is hard to undo.

## 2. Playing the demo

The demo scene starts in **Build mode**, with the player ship frozen in
place so you can safely edit it.

- Press **Tab** to switch between **Build mode** (edit the ship) and
  **Fly mode** (fly it).

### Build mode

- Press a number key **1** through **7** to pick which block type you
  are about to place (see the table below for what each number is).
- Move the mouse over the grid: the block preview turns **green** if it
  can be placed there, or **red** with a short reason if it cannot (see
  "hover messages" below).
- **Left click** to place a block. Some block types (Cannon and Fin)
  need a direction, so placing one of those takes two clicks: the first
  click chooses the cell, the second click chooses which way it faces.
- **Right click** to remove a block. Removing a block that other blocks
  depend on for their only connection back to the ship's core removes
  those blocks too, in one click; there is no confirmation, but
  Ctrl+Z (see below) undoes the whole removal as one step.
- **Ctrl+Z** undoes your last placement or removal; **Ctrl+Shift+Z**
  redoes it.
- **Esc** cancels an in-progress two-click placement (after the first
  click, before the second).

### The seven block types

1. **Core**: the heart of the ship. Every ship must have exactly one, and
   it must always be reachable from every other block, or the ship falls
   apart. It is the toughest block in the game.
2. **Hull**: a plain structural block. It holds the ship together and
   does nothing else.
3. **Armor**: like Hull, but heavier and tougher against sudden impacts
   (it resists being cracked open by a hit far more than Hull does, at
   the cost of extra mass).
4. **Thruster**: pushes the ship forward. It needs empty space directly
   behind it for its exhaust; you cannot place one with something
   blocking that space.
5. **Cannon**: fires projectiles in the direction it faces. It needs
   empty space directly in front of it (its muzzle) to be placeable.
6. **Fin**: steers the ship (turns it left or right) instead of moving it
   forward or back. It needs empty space in front of it, and it must be
   placed directly behind a non-Fin block (it cannot be the outermost
   tip of the ship on its own).
7. **RetroThruster**: pushes the ship backward, using two small nozzles
   on its left and right sides instead of one behind it. It needs both
   of those side cells empty.

### Hover messages while placing (Build mode)

When the preview turns red, the reason shown is one of these, in plain
words:

- **"out of range"**: that cell is outside the ship's buildable area.
- **"occupied"**: there is already a block there.
- **"not adjacent"**: the new block would not be touching any existing
  block edge-to-edge; every block must connect to the ship.
- **"needs an empty grid"**: you are trying to place something other
  than a Core on a completely empty ship; the very first block of any
  ship must be a Core.
- **"core already placed"**: you are trying to place a second Core; a
  ship may only have one.
- **"blocks the thruster exhaust"**: something is sitting directly
  behind a thruster (or in the sideways nozzle slots of a
  RetroThruster); nothing can sit directly behind a thruster or it has
  nowhere to push against.
- **"blocks the muzzle"**: something is sitting directly in front of a
  Cannon, in the direction it is about to face.
- **"blocks the fin"**: something is sitting directly in front of a Fin,
  in the direction it is about to face.
- **"inside a reserved cell"**: the cell you are trying to place into is
  itself reserved as another block's exhaust, muzzle, or nozzle space,
  even if nothing is drawn there.
- **"fin needs hull"**: a Fin must be placed directly behind an existing
  non-Fin block; it cannot hang off the ship with nothing solid behind
  it.

### Fly mode

- **W** / **S** (or Up/Down arrows): fire the forward thrusters / the
  retro (reverse) thrusters.
- **A** / **D** (or Left/Right arrows): fire the fins to turn left/right.
- **Space**: fire every cannon on the ship that is not still cooling
  down.
- **O**: cycle through the visual overlays (see below).
- **R**: reset the ship. In this demo scene the reset drops the ship back
  onto a preset circular orbit around the big planet, rather than
  putting it at dead rest; it does not restore any blocks you lost to
  damage.

### Overlays

Press **O** in Fly mode to cycle through five coloring modes for the
ship's blocks. These are purely visual; they never change how the ship
flies.

- **None**: each block shows its normal type color.
- **Stress**: blocks shade from green (low structural stress) to red
  (near breaking).
- **LoadBearing**: blocks that are the *only* connection holding part of
  the ship together are tinted magenta; losing one of these would split
  the ship.
- **Damage**: blocks shade from white (undamaged) to black (heavily
  damaged); damage only ever gets worse, never heals on its own.
- **Buckling**: blocks shade from blue to red based on how close they are
  to buckling (crumpling under compression) rather than simply cracking.

### Planets

The demo has two: a large planet below the player's starting orbit and a
smaller moon off to the side. Both pull the ship toward them with
gravity, and both have a solid surface: flying (or crashing) into one
bounces the ship off, and a hard enough impact damages whichever block
hit first.

### Powerups

Three colored, spinning discs float near where the player starts. Flying
the ship's collider into one temporarily changes the nearest matching
block on the ship (for example, the nearest Cannon or Thruster) into a
more powerful version for a limited number of seconds, then it reverts to
normal on its own. The always-on panel in the bottom-left corner of the
screen lists any powerup currently active on your ship and how many
seconds are left. Ten seconds after a powerup is collected, a fresh one
respawns in the same spot.

## 3. Tweaking things without touching code

Every table below lists numbers you can change **in the Inspector**,
without opening a single code file. To edit one: click the named
GameObject in the Hierarchy panel (left side), find the named component
in the Inspector (right side), and change the number field. Try Play
mode after each change to see the effect; stop and change it again if you
do not like it.

### `Gravity` GameObject, `GravityWorld` component

| Field | What you will SEE change | Sensible range |
| --- | --- | --- |
| `Planets` list, `Position` (per planet) | Where that planet sits in the world; ships and projectiles get pulled toward this point. | Anywhere; the demo's planets sit tens of units apart. |
| `Planets` list, `Mu` (per planet) | How strongly that planet pulls things toward it (its "mass," in the units this game's gravity math uses). Bigger numbers mean a stronger pull and tighter orbits. | 50 to 2000 for a moon-to-large-planet range; the demo's big planet and small moon differ by about 10x. |
| `Planets` list, `Radius` (per planet) | How big the planet is drawn, and how far from its center a ship or projectile starts colliding with its surface. | 4 to 25. |
| `Planets` list, `Color` | The planet's color. | Any color. |
| `Planets` list, `Soft Radius Factor` (per planet) | How far out (as a multiple of `Radius`) the pull stays softened near the center instead of following the raw inverse-square law, so a close pass never blows up. `0` (an unset row) falls back to the default `1.5`; the soft radius is always at least 2 units even for a small well. | 1 to 3 (or leave at 0 for the default). |
| `Surface Restitution` | How bouncy every planet's surface is on impact (higher bounces the ship away harder; lower feels sticky). | 0.0 (no bounce) to 1.0 (perfectly bouncy). |

### `Powerups` GameObject, `PowerupSpawner` component

| Field | What you will SEE change | Sensible range |
| --- | --- | --- |
| `Presets` list, `Position` (per powerup) | Where that pickup floats. | Anywhere reachable by the player. |
| `Presets` list, `Seconds` | How long the upgrade lasts once collected, shown counting down in the bottom-left panel. | 3 to 30 seconds. |
| `Presets` list, `Radius` | How big the pickup disc is drawn and how close you must fly to collect it. | 0.4 to 1.5. |
| `Presets` list, `Color` | The pickup's color. | Any color. |
| `Presets` list, `Display Name` | The label shown in the bottom-left panel while the upgrade is active. | Any short text. |
| `Respawn Seconds` | How long after a pickup is collected before a fresh one appears in the same spot. | 5 to 60 seconds. |

### `Main Camera` GameObject, `Camera` component

| Field | What you will SEE change | Sensible range |
| --- | --- | --- |
| `Size` (only shown when the camera's `Projection` is set to `Orthographic`, which this scene uses) | How much of the world is visible at once: a bigger number zooms out, a smaller number zooms in. | 5 to 40. |

### `Main Camera` GameObject, `Camera Follow` component

| Field | What you will SEE change | Sensible range |
| --- | --- | --- |
| `Smoothing` | How quickly the camera catches up to the ship; higher numbers make the camera snap to the ship faster, lower numbers make it lag behind more smoothly. | 1 to 15. |

### `Demo` GameObject, `Projectile Spawner` component

These control every cannon shot fired in the scene, since one spawner is
shared by every ship.

| Field | What you will SEE change | Sensible range |
| --- | --- | --- |
| `Speed` | How fast a fired shot travels across the screen. | 5 to 60. |
| `Impulse` | How hard the ship is pushed backward (recoil) each time it fires. | 0 to 10. |
| `Damage` | How much damage one hit does to the block it strikes. | 5 to 100. |
| `Lifetime` | How many seconds a shot travels before disappearing on its own if it never hits anything. | 1 to 10. |
| `Radius` | How big the shot is drawn, and roughly how close it needs to pass to count as a hit. | 0.05 to 0.5. |

### `PlayerShip` and `TargetShip` GameObjects, `Ship Controller` component

| Field | What you will SEE change | Sensible range |
| --- | --- | --- |
| `Blocks` list, per entry (`X`, `Y`, `Type Id`, `Modifiers`) | The ship's starting layout of blocks: see section 4 below for exactly what these four numbers mean. | See section 4. |
| `Thruster Mounts` | Not required to change; leave as authored unless you know a specific visual effect needs a mount point. | n/a |

| `Thrust Per Block` | How hard each forward thruster pushes. Bigger means faster take-off and harder spins from off-center thrusters. | 5 to 30 |
| `Retro Thrust Per Block` | How hard each retro thruster pushes you backward. | 2 to 15 |
| `Fin Force` | How hard each fin turns the ship. | 2 to 20 |
| `Cannon Cooldown` | Seconds between shots for every cannon. | 0.1 to 2 |

**Note on the one thing that is not yet an Inspector field.** How fast a
thruster "ramps up" to full power is controlled by 2 hidden bits packed
into a block's `Modifiers` number (see section 4) rather than by its own
Inspector field. Changing it today means editing the number by hand;
treat this as "not in the Inspector yet."

## 4. Adding things by copying an existing entry

Everything in this section works by selecting an existing list entry in
the Inspector, using its right-click menu (or the small "+"/context menu
at the bottom of the list) to duplicate it, and then editing only the
copy's numbers.

### Add a third planet

1. In the Hierarchy, click **Gravity**.
2. In the Inspector, find the `Gravity World` component's `Planets` list.
3. Right-click the row number of an existing planet entry (for example
   the moon) and choose **Duplicate Array Element**, or increase the
   list's **Size** field by one to add a fresh blank entry.
4. Set the new entry's `Position`, `Mu` (its pull strength), `Radius`,
   and `Color` to whatever you like; see the ranges in section 3.
5. Press Play to confirm the new planet appears and pulls the ship
   toward it.

### Add another powerup

1. In the Hierarchy, click **Powerups**.
2. In the Inspector, find the `Powerup Spawner` component's `Presets`
   list.
3. Duplicate an existing entry the same way as above.
4. Change the new entry's `Position`, `Seconds`, `Color`, `Display Name`,
   and `Radius`. The `Variant` and `Base Type Id` numbers pick which
   upgrade it grants and which block type it applies to; leave these
   matching one of the existing entries unless you are also doing
   section 5 below, since inventing a new number here without matching
   code behind it does nothing.
5. Press Play and fly through it to confirm it grants the expected
   upgrade.

### Change the starting ship layout

1. In the Hierarchy, click **PlayerShip** (or **TargetShip**).
2. In the Inspector, find the `Ship Controller` component's `Blocks`
   list. Each entry has four numbers:
   - **X** and **Y**: the block's position on the ship's own grid, with
     `(0, 0)` being wherever the Core sits. Moving one unit in X moves
     one block-width sideways; moving one unit in Y moves one
     block-width forward/backward.
   - **Type Id**: which of the seven block types this is, using the same
     order as the number keys in Build mode: `0` = Core, `1` = Hull,
     `2` = Armor, `3` = Thruster, `4` = Cannon, `5` = Fin,
     `6` = RetroThruster.
   - **Modifiers**: a single number that currently only matters for
     Cannon and Fin, and only its lowest two possible values in practice
     for a beginner: it picks which way the block faces, relative to the
     ship's own front. `0` = facing the ship's forward direction,
     `1` = facing the ship's right, `2` = facing the ship's backward
     direction, `3` = facing the ship's left. Leave it at `0` for every
     other block type.
3. Add, remove, or duplicate entries the same way as the planet and
   powerup lists above. Every ship needs exactly one Core entry, and
   every other block should end up connected, edge to edge, back to it,
   the same rule Build mode enforces when you place blocks by hand.
4. Press Play to see the new layout (the ship is rebuilt from this list
   every time Play starts).

### Move the target ship

1. In the Hierarchy, click **TargetShip**.
2. In the Inspector, find the **Transform** component at the top (every
   GameObject has one) and change its `Position` values.
3. Press Play to confirm the target now sits somewhere else, still
   stationary.

## 5. Adding a new weapon or thruster behaviour, with as little theory as possible

This is the one place this guide asks you to look at code, but only to
copy a file and change a few lines it points at exactly; you do not need
to understand the surrounding math.

1. In the `Assets/Scripts/Hullbreach.Ship/Behaviours` folder, find the
   behaviour closest to what you want (for example
   `SeekingThrusterBehaviour.cs` for a thruster variant, or
   `GravityGunBehaviour.cs` for a cannon variant). Copy that file and
   rename the copy, keeping the same folder, for example
   `SlowThrusterBehaviour.cs`.
2. Inside the copied file, rename the class itself to match the new file
   name (the class name and file name must match).
3. Change the two or three numbers that make it behave differently. For
   example, in a copy of `SeekingThrusterBehaviour.cs` you might see a
   line close to this shape:

   ```csharp
   public float TurnStrength = 2f;
   ```

   Change the number to make the new variant stronger or weaker, for
   example:

   ```csharp
   public float TurnStrength = 0.5f;
   ```

4. Open `Assets/Scripts/Hullbreach.Ship/Behaviours/BehaviourRegistry.cs`
   and find the method `RegisterDefaults()`. Add one line, following the
   pattern of the lines already there, picking a variant number that is
   not already used for that same block type (Thruster already uses `0`
   for plain and `1` for Seeking, so a new thruster variant would be
   `2`):

   ```csharp
   Register(Hullbreach.Core.BlockTypes.Thruster, 2, new SlowThrusterBehaviour());
   ```

5. Add a powerup for it in the scene, following section 4's "Add another
   powerup" steps, setting `Base Type Id` to the same block type and
   `Variant` to the same number you just registered.
6. Press Play, collect the new powerup on a ship that has a block of that
   type, and confirm the new behaviour is what fires or thrusts.

**Do not touch these files for this recipe**: anything in
`Hullbreach.Core`, `Hullbreach.Structure`, or `Hullbreach.Builder`, and
`ShipBody.cs` itself. None of them need to change to add a variant, and
editing them risks breaking every other block on every ship, not just
your new one.

## 6. Checking you did not break anything

After any change to a `.cs` file (skip this step if you only changed
numbers in the Inspector and did not touch a code file):

1. Open a terminal. On Windows, use **Git Bash** (installed alongside
   Git) or **WSL**; the plain Windows Command Prompt will not work here.
2. Run:

   ```
   tools/plaincs/run_tests.sh
   ```

3. Look at the last few lines of output. If you see the word
   **"Passed!"**, your change did not break any of the project's
   automated checks.
4. If you instead see **"Failed!"**, undo your last change (in the
   editor, Ctrl+Z; for a code file, revert it in your git tool) and try
   again more carefully, or ask for help before continuing.

Separately, whenever you are in Play mode, keep an eye on the **Console**
panel in the editor (Window > General > Console if it is not already
open). Any message shown in **red** means something went wrong: stop,
read the message, and undo your most recent change if you do not
understand it, rather than continuing to build on top of it.

## 7. Where to look next

- `docs/architecture.md`: how the code is organized and why, for when you
  are ready to read code, not just copy it.
- `docs/adding-a-block-behaviour.md`: the fuller version of section 5
  above, including how to add an entirely new block type, not just a new
  variant of an existing one.
- `docs/demo-scene.md`: the exact layout of every object in `DemoScene`,
  including every wired-up reference between them.
- `docs/testing.md`: how the automated tests work and how to write a new
  one.
- `docs/roadmap.md`: what is built, what is in progress, and what is not
  started yet, mapped to the course's story numbers.
