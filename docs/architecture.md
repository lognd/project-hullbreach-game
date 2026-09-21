# Architecture

This is the shape of the code under `Assets/Scripts`, and why it is split
the way it is. Read this before touching `Hullbreach.Ship` or
`Hullbreach.Structure`; almost every surprising decision in those two
traces back to one rule stated once here.

## The one rule: engine-free simulation, Unity-only adapters

`Hullbreach.Core`, `Hullbreach.World`, `Hullbreach.Structure`,
`Hullbreach.Ship`, `Hullbreach.Builder`, and `Hullbreach.Net` contain
**no `UnityEngine` reference at all**. They are plain C#: `Unity.Mathematics`
for vector math (it is a math library, not an engine dependency) and
nothing else. `Hullbreach.Game` is the only assembly that references
`UnityEngine`, and it is deliberately thin: `MonoBehaviour` adapters that
hold Inspector wiring and lifecycle (`Awake`/`Update`/`FixedUpdate`) and
otherwise just call into the plain assemblies.

This buys three concrete things, in order of how often they matter day to
day:

1. **Tests run in milliseconds with no Unity license.** `tools/plaincs`
   compiles every non-`Game` assembly plus the NUnit tests under
   `Assets/Tests/EditMode` with the stock .NET SDK and runs them with
   `dotnet test`. CI does this on every PR (`plaincs` job in
   `.github/workflows/ci.yml`); there is no Unity install in CI at all.
   See `docs/testing.md`.
2. **The dedicated server (S47) can run the simulation without Unity's
   object model.** A headless server process can new up a `ShipBody`,
   step it, and read `AppliedForcesThisStep`/`PendingShots` without ever
   loading a scene or a `GameObject`.
3. **Burst-compilable hot paths**, if that is ever needed: `Block` is a
   blittable readonly struct specifically so the FE assembly loop could be
   Burst-compiled without redesigning the data model (see the doc comment
   on `Assets/Scripts/Hullbreach.Core/Grid/Block.cs`).

Corollary: **server authority**. `CONTRIBUTING.md` states the policy
("clients send input through ghost commands; anything a client could lie
about must be validated or recomputed on the server") and the assembly
split is what makes it possible to actually enforce it later: the same
`ShipBody.Step` that runs on a client for local prediction is the exact
code that will run authoritatively on the server, with `IWorldSink`
swapped for a server-side implementation instead of `WorldSink`
(`Hullbreach.Game`). See `StructuralSolver.BuckledBlocks`'s doc comment
for the concrete instance of this that already exists: it says outright
that only the server may use it to decide a block breaks, because the
float FE solve is not guaranteed bit-identical across machines: "send
causes, not effects": broadcast the *event* ("this block died"), never
let each client independently recompute and risk disagreeing.

## Assembly dependency graph

```
                         Hullbreach.Game  (UnityEngine; MonoBehaviour adapters)
                        /   |    |    |   \
                       /    |    |    |    \
                Builder  Ship  Net World  (Structure, via Ship & directly)
                     \      \    |   /   /
                      \      \   |  /   /
                       \      \  | /   /
                        \      \ |/   /
                         \      X    /
                          \    / \  /
                           Structure  (also -> Core)
                              |
                            Core   (Unity.Mathematics, Unity.Collections only)
```

Concretely, from each `.asmdef`'s `references`:

- `Hullbreach.Core` -> `Unity.Mathematics`, `Unity.Collections`. The
  foundation: grid, block types, mass, topology. Nothing else in the repo
  depends on anything outside this list, which is what makes Core usable
  from every other assembly without a cycle.
- `Hullbreach.World` -> `Core`. Gravity fields and orbit math; knows about
  blocks only through `Core`, nothing about ships.
- `Hullbreach.Structure` -> `Core`. The Q8 FE solver, buckling, damage. No
  dependency on `Ship`: a ship is "a `BlockGrid` plus applied forces" as
  far as structure is concerned; it does not know what a thruster is.
- `Hullbreach.Ship` -> `Core`, `Structure`, `World`. `ShipBody` is the
  simulation core: it owns a `BlockGrid`, steps block behaviours, applies
  gravity, integrates motion, and resolves planet contacts.
- `Hullbreach.Builder` -> `Core`. Placement rules, clearance, undo, the
  palette. Independent of `Ship`/`Structure` because placement validity is
  purely a grid-geometry question.
- `Hullbreach.Net` -> `Core`. Wire messages and quantization; not wired to
  a transport yet (see `docs/roadmap.md`, S47/S48).
- `Hullbreach.Game` -> all of the above, plus `UnityEngine`. Scene-facing
  adapters: `ShipController`, `BuilderController`, `ShipRenderer`,
  `ShipStructure`, `WorldSink`, `GravityWorld`, `Powerup`/`PowerupSpawner`,
  `Projectile`/`ProjectileSpawner`, `DemoMode`, `BuilderHud`,
  `CameraFollow`.

If you find yourself wanting to `using UnityEngine` inside `Ship`,
`Structure`, `Builder`, `Core`, `World`, or `Net`, that is a sign the code
belongs in `Hullbreach.Game` instead, or that the plain assembly needs a
new interface (like `IWorldSink`) that `Game` implements.

## The block grid: `BlockKey` and the modifier byte

`Assets/Scripts/Hullbreach.Core/Grid/BlockKey.cs` packs a signed 2D grid
coordinate into one `int`: both axes are biased by `-BlockKey.Min` (128)
into `0..255`, then `x` occupies the high byte and `y` the low byte
(`(bx << 8) | by`). Range is `-128..127` on each axis. This one packed
`int` is simultaneously the dictionary key for `BlockGrid`'s backing store
and the value that goes on the wire, so there is only one identity to keep
consistent. Grid convention: block `(x, y)` occupies `[x, x+1] x [y, y+1]`,
so its center is `(x + 0.5, y + 0.5)`.

Every `Block` (`Assets/Scripts/Hullbreach.Core/Grid/Block.cs`) is a
`readonly struct { byte TypeId; byte Modifiers; byte Damage; }`: no heap
allocation, blittable, cache-friendly. Everything shared across all blocks
of the same type (mass, stiffness, strength) lives in `BlockType`, looked
up by `TypeId`; that flyweight split is why there is no `Block` subclass
per block kind.

`Modifiers` packs three independent fields, one byte, each owned by a
different piece of code that only touches its own bits:

| Bits | Field | Owner | Meaning |
| --- | --- | --- | --- |
| 0-1 | Facing | `Hullbreach.Core.Facing` | `0`=+y, `1`=+x, `2`=-y, `3`=-x, ship-local, before ship `Rotation`. Only Cannon and Fin use this (Thruster/RetroThruster/Core/Hull/Armor ignore it: they have a fixed or no direction). |
| 2-3 | Ramp (`ThrusterUpgrades`) | `Hullbreach.Ship.ThrusterUpgrades` | 0..3, indexes `SecondsToFull = {1.0, 0.6, 0.35, 0.15}`: how fast a thruster/retro/fin ramps its throttle toward a target. Lives in `Ship` (not `Core`) since only Ship's ramped channels care, but the bit position is fixed here so `Facing` and `Variant` can coexist in the same byte. |
| 4-7 | Variant | `Hullbreach.Core.BlockVariants` | 0..15, the behaviour-extension id: variant 0 is always a type's base behaviour (plain Cannon, plain Thruster); nonzero variants are alternates registered against the same `TypeId` in `BehaviourRegistry` (e.g. Cannon variant 1 is the gravity gun). |

`Facing` and `BlockVariants` live in `Core` rather than `Ship` specifically
so that `Hullbreach.Builder`'s placement rules (which need to know "what
cell is ahead of this Cannon") can use the identical encoding without
depending on `Hullbreach.Ship`. `ThrusterUpgrades` lives in `Ship` because
nothing outside Ship's ramped channels reads it, but it is careful to
mask only `0b1100` so it never disturbs the facing or variant bits.

## `ShipBody.Step`: the per-tick order of operations

`Assets/Scripts/Hullbreach.Ship/ShipBody.cs`, method `Step(in ShipInput
input, float dt)`. This runs once per fixed tick, called from
`ShipController.FixedUpdate` in the demo scene (and, eventually, from the
server's own tick loop). In order:

1. **Rebuild derived views if the grid's topology changed** (`if
   (Grid.TopologyDirty) RebuildDerivedViews()`), which recomputes cached
   key lists (`ThrusterKeys`, `RetroKeys`, `FinKeys`, `WeaponKeys`) so the
   behaviour loops below never have to filter the whole grid by type every
   tick.
2. **Clear this tick's force log** (`AppliedForcesThisStep`) and latch
   `FireRequested` from input; both exist for other systems (structure,
   UI) to read after `Step` returns.
3. **Bail out if the ship has no mass** (no blocks survived; even the
   core can be stripped by cascading detaches): skip integration entirely
   rather than divide by zero.
4. **`TickPowerups(dt)`**: counts down every active powerup transform
   (see below) and reverts any that expired back to the block's base
   variant, *before* behaviours run so a powerup that just expired this
   tick does not fire.
5. **`StepBehaviours` for each ramped channel and weapon group**, in a
   fixed order: `ThrusterKeys`, `RetroKeys`, `FinKeys`, `WeaponKeys`. Each
   call resolves every block's `IBlockBehaviour` via `BehaviourRegistry`
   and calls `Step(ref BlockContext)` on it (see
   `docs/adding-a-block-behaviour.md` for what that means). "Ramped
   channels" (thrusters/retros/fins) ramp a stored throttle toward a
   target via `ThrusterUpgrades.RampRate`; weapons (cannons and their
   variants) tick a per-block cooldown and fire when pressed and ready.
6. **`ApplyGravityForces()`**: per-block body force: for every block,
   `Gravity.AccelerationAt(worldCenter) * blockMass`, applied at that
   block's own ship-local center via `AddForceAtPoint`. This is what makes
   gravity torque-correct (a lopsided ship spins under a tidal gradient)
   and why gravity shows up in `AppliedForcesThisStep` exactly like any
   other applied force: the structural solver does not special-case it.
7. **Integrate**: compute `worldForce / mass` and `torque / inertia`,
   then **semi-implicit (symplectic) Euler**: velocity updates first,
   then position uses the *new* velocity. Chosen because it is what Box2D
   itself does, so the plain-C# simulation stays consistent with how
   `Rigidbody2D` will eventually feel if/when the two are reconciled (see
   `docs/roadmap.md`'s engineering-debt note on `Rigidbody2D` being driven,
   not simulated).
8. **Clear the force/torque accumulators** for the next tick.
9. **`ResolvePlanetContacts(dt)`**: after integration, tests every block
   center against `Gravity` for surface penetration, pushes the ship out
   of the single deepest penetration, then resolves each contacting
   block's normal velocity with a restitution impulse, bleeds tangential
   velocity by friction, and applies contact damage to whichever block hit
   first if the impact speed exceeded `ShipBody.ContactDamageSpeed` (3 m/s
   default): the same damage path a projectile hit uses.

Powerup expiry runs before behaviours specifically so "the last tick of a
powerup" still fires with the powerup's behaviour, and "the tick it
expires" already fires with the plain one: there is no tick where a
timer reads zero but the upgraded behaviour still runs.

## Structural pipeline: Q8 FE, inertia relief, PCG, stress, damage, buckling

`Hullbreach.Structure.StructuralSolver.Tick(grid, appliedForces, dt)` is
called once per tick from the game layer (`ShipStructure`, in
`Hullbreach.Game`, using `ShipBody.AppliedForcesThisStep` as the force
list). It is a stress *readout*, not something `ShipBody.Step` calls
directly: Structure has no dependency on Ship (see the assembly graph).

1. **Rebuild the stiffness matrix `K` if topology changed**: tracked by
   `StructuralSolver`'s own `_lastRebuiltCount` compared against
   `grid.Count`, plus an explicit `MarkTopologyChanged()` for a same-count
   type swap. `StiffnessAssembly.Rebuild` assembles `K` from one **Q8
   element per block** (`Q8Element`, an 8-node quadratic quad on a shared
   `NodeLattice`, so adjacent blocks share edge nodes) using the
   precomputed `KHat` table indexed by `PoissonClass`: `K_e = E * KHat`,
   so changing a material's `YoungsModulus` never needs re-deriving the
   element stiffness itself.
2. **Assemble the load vector**: point forces from `appliedForces` (i.e.
   everything `ShipBody` pushed through `AddForceAtPoint` this tick) via
   `LoadVector.AddPointForce`, plus **inertia relief**
   (`LoadVector.ApplyInertiaRelief`), because the ship is not fixed to
   anything, an unbalanced applied-force set would otherwise accelerate
   the whole FE mesh rigidly with no way to reach static equilibrium;
   inertia relief subtracts out exactly the rigid-body-consistent inertial
   load so the solve becomes a well-posed quasi-static problem.
3. **Solve with PCG, under a per-tick budget**: `CgSolver.Solve`, using the
   three rigid-body modes (`LoadVector.RigidBodyModes`) so the singular
   (free-floating) stiffness matrix still has a well-defined particular
   solution. `StructuralSolver.MaxCgIterationsPerTick` (default 400) caps
   how much CG work one `Tick` may spend; when a ship is too wide for that
   budget (see `CgSolver`'s doc: iterations scale with the ship's width in
   elements), the PARTIAL displacement is kept as the next tick's warm
   start rather than blocking the frame, and `StructuralSolver.Converged`
   is false until a later tick's warm start finally gets under
   `CgSolver.Tolerance` (relative to `|f|`, with an absolute floor via
   `Tolerance * Math.Max(1, |f|)`, so a near-zero load never burns
   iterations chasing noise). "Converged", concretely, means the tick's
   `BlockStresses` reflect a displacement field within `Tolerance` of the
   true quasi-static solution for that tick's load; a false `Converged`
   means they LAG the true answer by however far the residual still is.
   Buckling (step 7) only runs on a converged tick, since its geometric
   stiffness is built from that same, possibly-still-settling stress
   field. See `docs/roadmap.md`'s Performance section for measured
   iteration counts and the preconditioner debt.
4. **Reduce to per-block stress** (`ComputeBlockStress`): evaluates strain
   at each Q8 element's center (`xi = eta = 0`, i.e. the block's own
   center, not a proper stress-recovery/extrapolation to nodes (an
   intentional simplification, and the reason buckling below is also
   element-center-based, see `docs/roadmap.md`), applies the constitutive
   relation scaled by `BlockTypes.EffectiveStiffness` (which folds in
   damage softening: a yielded block gets less stiff so it sheds load to
   its neighbors), and reduces to von Mises plus principal stresses.
5. **Stress criteria** (`StressCriteria`, `Hullbreach.Structure.Failure`):
   `DuctileRatio` (von Mises vs. `YieldStress`, softened by
   `DamageFraction`) and `BrittleRatio` (max tensile principal vs.
   `SpallStress`, min compressive vs. `CompressiveStress`). Both are
   **client-safe**: every machine's solve produces the same ratios given
   the same inputs, so both are fine to use for a color tint
   (`ShipRenderer`'s Stress overlay uses `max(DuctileRatio, BrittleRatio)`)
   or, per the game layer's own decision, to detach a block once a ratio
   exceeds 1 (see `ShipStructure` in `Hullbreach.Game`).
6. **Damage** (`Hullbreach.Structure.Failure.DamageModel`): hysteresis so
   damage only accumulates, never heals on its own, and the softening
   floor (5% of nominal `E`) keeps `K` from ever going singular even at
   full damage.
7. **Linearized buckling** (`RunBuckling`, throttled to run at most every
   `BucklingEveryNTicks` ticks, default 4): assembles the geometric
   stiffness `K_G` from the *current* stress state
   (`GeometricStiffness.Rebuild`), then runs subspace iteration
   (`BucklingAnalysis`) toward the lowest eigenvalues of the generalized
   buckling eigenproblem. Cheap early-out: if no element anywhere is in
   meaningful compression, `K_G` is positive semidefinite and there is no
   positive load factor to find, so the whole subspace machinery is
   skipped.
   - `BlockStress.BucklingRatio`: **client-safe**: a continuous float
     (`1 / CriticalLoadFactor`, scaled by strain-energy participation in
     the single critical mode), the same on every machine's own solve,
     meant only for the Buckling overlay's tint.
   - `StructuralSolver.BuckledBlocks`: **server-authoritative, NOT
     client-safe**: the union of every sub-critical mode's
     highest-participation blocks (cumulative to
     `BucklingParticipationThreshold`). The doc comment on this property is
     explicit about why: the float FE solve and subspace iteration are not
     guaranteed bit-identical across machines, so only the authoritative
     simulation may use this to decide a block breaks and then *broadcast*
     that as an event. A client that independently reads this and detaches
     a block itself can disagree with the server and desync: this is the
     concrete instance of "send causes, not effects" mentioned above.

## Placement rules: verdicts, clearance, detach

`Hullbreach.Builder.PlacementRules.CanPlace(grid, key, typeId, modifiers,
out PlacementVerdict why)` is the full placement check, in order:

1. **Range** (`BlockKey.InRange`) -> `OutOfRange`.
2. **The core-seeding rule** (S32): an empty grid accepts only a `Core`,
   anywhere in range (`NeedsEmptyGrid` if not-core-into-empty,
   `CoreAlreadyPlaced` if core-into-nonempty).
3. **Occupancy** -> `Occupied`.
4. **4-adjacency** to an existing block -> `NotAdjacent`.
5. **Reserved-cell clearance**, both directions:
   - the new block must not land inside any *existing* neighbor's reserved
     cells (`Clearance.TryReservedCells` on each of the new key's four
     neighbors) -> `InsideReservedCell`;
   - the new block's *own* reserved cells (its exhaust/muzzle/side space)
     must themselves be empty -> `BlocksMuzzle` (Cannon), `BlocksFin`
     (Fin), or `BlocksExhaust` (Thruster/RetroThruster).
6. **Required anchor** (`Clearance.RequiredAnchor`): a Fin's
   `Facing.Behind` cell must hold a non-Fin block -> `FinNeedsHull`.

`Clearance` (`Assets/Scripts/Hullbreach.Builder/Clearance.cs`) is the
single source of "what cells does this block reserve", shared by both
directions of the check above so "is my footprint clear" and "am I inside
someone else's footprint" can never drift apart:

| TypeId | Reserved cells |
| --- | --- |
| Thruster | fixed ship-local `(x, y-1)` (exhaust), independent of `Modifiers`: a thruster has no facing of its own |
| Cannon, Fin | `Facing.Ahead(key, modifiers)`: one cell, in the block's own facing |
| RetroThruster | `(x+1, y)` and `(x-1, y)` (its two side nozzles) |
| Core, Hull, Armor | none |

`PlacementRules.Detach(grid, key, removed)` is the S31 removal/detach
rule: removing a block is always allowed except the core
(`CanRemove`), and if the removed block was an **articulation point**
(`Hullbreach.Core.Topology.Articulation`, a fast iterative-Tarjan
computation) then everything that becomes unreachable from the core
(`Connectivity.FindDetached`) is removed along with it, in one call. The
articulation check is a deliberate fast path: if the removed key was not a
cut vertex, nothing else could possibly disconnect, so the flood fill is
skipped. This is a documented product decision (see `TODO.md`'s "Decision
on S31"): a removal that would strand blocks is *allowed*, not refused, so
single-click removal is always available near a bottleneck; the caller
undoes the whole batch as one `UndoStack` action.

## Where to go next

- Adding a new weapon/thruster variant or a whole new block type:
  `docs/adding-a-block-behaviour.md`.
- What is and is not verified in the Unity editor, and the demo scene's
  object layout: `docs/demo-scene.md`.
- Running tests, what CI checks, writing a new test:
  `docs/testing.md`.
- Sprint status against the GitHub story numbers and known engineering
  debt: `docs/roadmap.md`.
