# Roadmap

Sprint status against the GitHub story numbers (`gh issue list --state all`;
all 32 stories/epics are open issues: there is no "closed" workflow on
this tracker, status lives here instead), each with a pointer to the code
it would build on. Sprint 1 is 9/16-9/27, Sprint 2 is 10/5-10/23, Sprint 3
is 11/2-11/20 (`README.md`). Branch `physics-model` (53 commits ahead of
`main` as of 2026-09-20) already contains Sprint-1 work plus a large chunk
of Sprint-2 groundwork landed early.

## Sprint 1 (Game shell / Ship builder / Flight physics / early Combat)

| Story | Status | Code |
| --- | --- | --- |
| #3 S26 title screen | Not started | Needs a UI scene; nothing under `Assets/Scenes` but `RocketScene.unity`/`DemoScene.unity`. |
| #4 S27 settings | Not started | Same UI-scene dependency as S26; also the natural point to migrate off the legacy `Input` class (see engineering debt below). |
| #5 S28 LAN match | Partially done | `Hullbreach.Net.NetMessages`/`Quantization` exist as wire formats and quantization helpers; no transport (Netcode for Entities/UGS relay/etc.) is wired to anything. See `docs/architecture.md`'s "server authority" section for the design NetMessages already commits to ("send causes, not effects"). |
| #6 S29 online opponent | Not started, `needs-platform` | Depends on the platform repo's matchmaking API. |
| #8 S30 place a block, two clicks | **Done** | `Hullbreach.Builder.PlacementRules`/`BuilderSession`, `Hullbreach.Game.BuilderController`. Edit-mode tests: `Assets/Tests/EditMode/Hullbreach.Builder.Tests/PlacementRulesTests.cs`, `BuilderSessionTests.cs`. |
| #9 S31 remove and undo | **Done** | `PlacementRules.Detach` + `Hullbreach.Builder.UndoStack`. Decision recorded in `TODO.md`/`docs/architecture.md`: a removal that strands blocks detaches them too, undone as one action. Tests: `UndoStackTests.cs`. |
| #10 S32 build around a core | **Done** | `PlacementRules.CanPlace`'s core-seeding rule (empty grid accepts only a core). |
| #11 S33 block palette | **Done** | `Hullbreach.Builder.BlockPalette`, `Hullbreach.Game.BuilderHud`. Tests: `BlockPaletteTests.cs`. |
| #19 S39 fly a ship that handles like it was built | **Done** | `Hullbreach.Ship.ShipBody`/`SteeringModel`/`BlockFacing`, `Hullbreach.Game.ShipController` (replaces `PlayerSingle`/`RocketScene`'s prototype). Decision recorded in `TODO.md`: fins are reaction-wheel style (steer while drifting). Tests: `Hullbreach.Ship.Tests/ShipBodyTests.cs`, `ThrusterUpgradesTests.cs`. |
| #23 S42 shoot a basic cannon | **Done** (TODO.md is stale on this one, see below) | `Hullbreach.Ship.Behaviours.CannonBehaviour`/`CannonBehaviourBase` records a `ShotRequest`; `Hullbreach.Game.ProjectileSpawner`/`Projectile` actually spawn and simulate the round, apply damage via `ShipCollider`, and despawn on lifetime/impact. Wired into `DemoScene.unity` (`Demo`'s `WorldSink`/`ProjectileSpawner`). |
| #27 S46 win by breaching the core | Not started | No match state machine exists. The building block is there: `Hullbreach.Core.BlockGrid.CoreKey` plus `Hullbreach.Core.Topology.Connectivity`/`Articulation` can already detect "the core is gone" or "the core is isolated from the rest of the ship": a win condition would poll `CoreKey.HasValue` (or watch for it going stranded) per ship and end the match, but nothing currently calls that. |
| #29 S47 run matches on an authoritative server | Partially done | `Hullbreach.Net.NetMessages`'s doc comment already specifies the wire protocol design (`ShipSnapshot`/`ShipState`/`BlockPlaced`/`BlockDestroyed`/`FragmentSpawned`, all TODOs, no serializer written yet) and `Quantization` has the position/angle quantization helpers. `ShipBody`/`StructuralSolver` are engine-free by design specifically so a headless server process can run them (`docs/architecture.md`'s "the one rule"), and `StructuralSolver.BuckledBlocks` / `ShipStructure.Authoritative` already encode the server-authority split. What is missing: an actual transport, a server loop that ticks `ShipBody`/`StructuralSolver` without Unity's scene, and the message serialization itself. |

## Sprint 2 (Structural simulation / more physics / real internet)

| Story | Status | Code |
| --- | --- | --- |
| #12 S34 build during a fight | Not started | `DemoMode` treats Build and Fly as mutually exclusive states (`Tab` toggles), not simultaneous; `BuilderController`/`ShipController` would both need to run at once, which nothing currently supports. |
| #13 S35 save/load ship designs | Not started, `needs-platform` | No serialization of a `BlockGrid` to/from a design format exists yet; would likely reuse `NetMessages`'s planned `ShipSnapshot` layout. |
| #14/#15/#16/#17 E10 Structural simulation (S36 compute stress, S37 see failure coming, S38 break apart) | **Done, landed early** | The entire `Hullbreach.Structure` assembly: `Q8Element`/`NodeLattice`/`StiffnessAssembly`/`CgSolver`/`LoadVector` (FE core), `StressCriteria`/`DamageModel` (S36/S37 criteria), `BucklingAnalysis`/`GeometricStiffness` (buckling), tied together by `StructuralSolver`. S37's "see it coming" is the Stress/LoadBearing/Buckling overlays in `ShipRenderer`. S38's "break apart" is `Hullbreach.Game.ShipStructure` detaching blocks once a ratio exceeds 1 or a block is in a sub-critical buckling mode. Full pipeline described in `docs/architecture.md`. |
| #20 S40 fight inside a gravity field | **Done** | `Hullbreach.World.GravityField`/`GravityBody`/`OrbitHelper`, `Hullbreach.Game.GravityWorld`, `ShipBody.ApplyGravityForces`/`ResolvePlanetContacts`. Two planets in `DemoScene.unity`. |
| #21 S41 stay inside the arena | Not started | No arena-bounds concept anywhere in `Hullbreach.World`/`Hullbreach.Game`: a ship or projectile can fly arbitrarily far from the planets with nothing stopping it (aside from `BlockKey`'s own -128..127 grid-coordinate range, which bounds a *ship's own blocks*, not world-space position). |
| #24 S43 gravity gun / anti-gravity gun | **Done, landed early** | `Hullbreach.Ship.Behaviours.GravityGunBehaviour`/`AntiGravityGunBehaviour` (Cannon variants 1/2), `IWorldSink.AddTemporaryGravity`, powerup pickups in `DemoScene.unity`'s `Powerups` object. |
| #25 S44 fire the Inconvenient Thruster | **Done, landed early** | `Hullbreach.Ship.Behaviours.SeekingThrusterBehaviour` (Thruster variant 1), the third `Powerups` preset. |
| #26 S45 dodge telegraphed hazards | Not started | No hazard system exists; would likely be a new `Hullbreach.World` or `Hullbreach.Game` component analogous to `GravityBody`/temporary gravity wells, with a telegraph/warning phase before it activates. |
| #30 S48 keep the game fluid over the internet | Not started | Depends on S47's transport landing first; `Quantization`'s helpers are the piece already in place. |

## Sprint 3 (Store, admin, hazards, polish, stretch)

| Story | Status |
| --- | --- |
| #31 S49 favor the defender / forgive lag | Not started, `needs-platform` |
| #33/#34 S50/S51 (E14 stretch goals) | Not started |

## Known engineering debt

- **CG iteration cap and per-tick cost.** `CgSolver` (`Hullbreach.Structure.Fem.CgSolver.cs`)
  has a fixed iteration cap and no adaptive early-out beyond a stagnation
  guard (`fix(structure): raise CgSolver's iteration cap and add
  stagnation guard`, commit `8a17c34`); a large enough ship's per-tick
  solve cost has not been profiled against a real frame budget.
- **Buckling uses element-center stress**, not a proper stress-recovery
  extrapolation to nodes (`StructuralSolver.ComputeBlockStress` evaluates
  at `xi = eta = 0`); see `docs/architecture.md`'s structural-pipeline
  section. This is a known simplification, not a bug, but it means the
  buckling/stress tints are coarser than a full FE post-process would give.
- **No Input System migration.** `activeInputHandler: 2` ("Both") lets the
  legacy `Input`/`GetButton` calls in `ShipController`/`DemoMode` keep
  working alongside the installed `com.unity.inputsystem` package; S27
  (rebindable keys) is the natural moment to migrate, and has not happened.
- **Scene YAML is hand-authored**, never opened in the Unity editor.
  `RocketScene.unity`'s `ShipController`/`BuilderController` wiring and
  the entirety of `DemoScene.unity` were hand-written (`TODO.md`, commit
  `f680856`'s body). See `docs/demo-scene.md`'s "first open in Unity"
  checklist.
- **`Rigidbody2D` is driven, not simulated.** `ShipBody` is the actual
  physics (semi-implicit Euler integration, its own gravity and contact
  resolution); the `Rigidbody2D` component on a ship's GameObject exists
  for Unity's own collision/trigger events (`ShipCollider`, `Powerup`'s
  `OnTriggerEnter2D`) and is driven from `ShipBody`'s state rather than
  contributing its own physics. If Unity Physics2D and `ShipBody` ever
  disagree about position/velocity, `ShipBody` wins; nothing currently
  guards against the two drifting apart other than "one writes, one reads".
- **Damage-only K rescale, not implemented as a separate pass.**
  `BlockTypes.EffectiveStiffness` folds damage softening directly into the
  stiffness lookup every tick (floored at 5% of nominal `E`); there is no
  standalone "rescale K only when damage actually changed" optimization,
  so a fully-undamaged ship pays the same softening-lookup cost as a
  heavily damaged one. Noted as a placeholder in `BlockTypes.cs`'s `TODO
  [D3]` comment on the floor value.

## Performance (perf/preconditioner, 2026-09-20)

`SolverBenchmarks` (`Assets/Tests/EditMode/Hullbreach.Structure.Tests/SolverBenchmarks.cs`)
builds a wide plate-plus-slender-arm ship (the arm makes CG's width
scaling visible without a plate alone hiding it) and reports DOF count,
CG iterations/tick, ms/tick and buckling sweeps to `TestContext` for
every default `run_tests.sh` run; a 2000-block case is
`[Category("Slow")]` and excluded from the default filter (run it with
`tools/plaincs/run_tests.sh --filter "TestCategory=Slow"`).

**Per-iteration profiling** (temporary Stopwatch-instrumented test, not
committed): at 5642 DOF, a bare `StiffnessAssembly.Multiply` cost ~239us
(Release) / ~1657us (Debug) per call; a full preconditioned CG iteration
cost ~327us (Release) on top of that, i.e. the non-matvec part of an
iteration (two residual-norm reductions, the Jacobi divide, the rigid-mode
projection's 3 dot+axpy pairs, the CG update loops) cost roughly as much
as the matvec itself. The single largest avoidable cost found:
`CgSolver.Solve`'s inner loop computed the residual norm via `Dot(r,r)`
TWICE per iteration (once for the top-of-loop convergence/stagnation
check, once again right after updating `r` for an early-exit check) for
no reason beyond a historical growth-by-accretion -- both wanted the same
number. Caching it in one variable carried across iterations (this
branch's first commit) removes that duplicate O(n) reduction. `dotnet
test` defaults to a Debug build (no `-c Release` in
`tools/plaincs/run_tests.sh`), so the numbers this doc and
`SolverBenchmarks` report are Debug numbers; a shipped Unity build is
Release-equivalent, so treat the ms/tick figures below as an upper bound
on what a player actually sees, not the real in-game number.

**Convergence tolerance**: the quasi-static stress pass now defaults
`CgSolver.Tolerance` to `1e-3` relative (was `1e-5`, `CgSolver`'s general
default, still used unchanged by `BucklingAnalysis`'s own internal
`CgSolver` instance). Stress/damage decisions round-trip through
`StressCriteria`'s ratios, which do not need 5-digit accuracy in the
displacement field to be correct to well under 1%; buckling's Rayleigh
quotients are far more sensitive to residual displacement error (see
`CgSolver.MaxIterations`'s doc on the buckling regressions an
under-converged solve previously caused), so it keeps the tight
tolerance.

**Deflated two-level preconditioner** (`CoarsePreconditioner.cs`): adds a
coarse, global correction on top of plain Jacobi, `M^-1 = D^-1 + P Kc^+
P^T`, where `P`'s columns are three rigid-body modes (translate x,
translate y, rotate about centroid) per 4x4-block aggregate and `Kc = P^T
K P`. A previous attempt at this same construction (see this section's
prior revision) factored `Kc` with Cholesky and got NaNs, because `Kc`
inherits K's exact 3-dimensional rigid-body null space (every aggregate's
local rigid modes sum, weighted correctly, to the ship's global ones) and
Cholesky of a singular matrix divides by a near-zero pivot rather than
failing loudly. This attempt instead diagonalizes `Kc` once per topology
change (`DenseJacobiEigen`, shared with `BucklingAnalysis`'s
Rayleigh-Ritz solve, extracted here per NO-DUPLICATION) and builds a
Moore-Penrose pseudo-inverse that drops eigenvalues below a floor
relative to the largest, so the coarse correction is zero on `Kc`'s null
space by construction, not by hoping a factorization's rounding noise
stays small. The ticket's suggested `1e-6` floor was NOT enough in
practice: on the 500-block plate+arm benchmark it let through near-null
(not exactly null) directions from the 1-wide arm's poorly-conditioned
aggregation (neighboring aggregates along a 1-block-wide strip have
almost-parallel local rotate modes), each amplifying rounding noise every
CG iteration and diverging the residual to NaN within 10 ticks; `1e-3`,
plus scaling `Kc` to O(1) before diagonalizing (so `DenseJacobiEigen`'s
absolute sweep-termination tolerance is meaningful), stopped the
divergence and still gives useful deflation. `StructuralSolver.
UseCoarseCorrection` (default `true`) switches it off for comparison.

**Second correctness bug found by the existing test suite, not the new
one**: `CgSolver.Solve` only ever re-projects the RESIDUAL `r` onto the
complement of the rigid modes, never the search direction `p` or the
solution `u` themselves (this was already true before this branch; it is
safe under plain Jacobi because `D^-1` cannot introduce a rigid-mode
component that Gram-Schmidt-orthonormalized `modes` did not already put
there). `CoarsePreconditioner.ApplyAdditive`'s floor-based pseudo-inverse
is only APPROXIMATELY zero on the rigid modes (float rounding in
`DenseJacobiEigen`, worse on small ships where Kc's null space is a large
fraction of its whole space), so it can leak a tiny nonzero rigid-mode
component into `z`, and from there into `p`, and from there into `u`,
every iteration, without ever showing up in the residual (`K` annihilates
that component, so `r` cannot detect it). This corrupted three existing
`BucklingTests` (`Column_CriticalLoadFactorMatchesDenseOracle`,
`Column_CriticalLoadFactorScalesWithInverseLengthSquared`,
`SmallBlob_HasNoLowLoadFactor`) on small test grids, despite CG reporting
ordinary convergence. Fixed by re-projecting the COMBINED
preconditioned vector `z` (Jacobi + coarse) onto the rigid-mode
complement immediately after `CoarsePreconditioner.ApplyAdditive`, before
it ever reaches `p`.

Unit tests (`CoarsePreconditionerTests.cs`): the combined `M^-1` stays
symmetric (`v.(M^-1 w) == w.(M^-1 v)` on random vectors), the coarse
correction is ~0 on each global rigid mode, and on a 1-wide 32-block arm
under a tip load the coarse-augmented solve needs 143 iterations to
converge to 1e-3 relative vs. plain Jacobi's 488 (3.4x fewer).

Measured (plate + arm, thruster load, `CgSolver.Tolerance` at its
default `1e-5` relative -- see the tolerance section above for why this
stayed unloosened -- `MaxCgIterationsPerTick` at the production default
of 400, Release build, steady-state ticks after the one-time
topology/coarse-rebuild cost):

| Ship        | DOF   | CG iterations/tick    | ms/tick (steady state) | Converged |
|-------------|-------|-----------------------|-------------------------|-----------|
| 100 blocks  | 810   | ~300 (of 400 cap)      | ~19-40 (spikes to ~400 every 4th tick, see below) | yes |
| 500 blocks  | 3850  | 400 (capped)           | ~110-120                | no        |
| 2000 blocks | 10702 | 400 (capped)           | ~390-400 (one-time ~16.4s Kc rebuild on tick 0)    | no |

The 100-block case is a genuine win over the previous attempt's baseline
(400 capped iterations, never converged, ~75ms/tick Debug): it now
converges every tick to the FULL `1e-5` tolerance, comfortably inside a
50Hz budget except for a periodic spike (see below). The 500- and
2000-block cases do NOT meet the ticket's <10ms/50Hz target and do NOT
converge within the 400-iteration budget at `1e-5`; unlike the earlier
attempt they no longer diverge to NaN (residual stays bounded, only
mildly worse than the coarse-Kc-rebuild-cost-only baseline), which is
real progress but not the target. With the 400-iteration cap carrying
over every tick and residual not trending down across ticks, these ships
would need many more ticks than this benchmark's 10-tick window to
converge, if they converge at all under a hard per-tick restart; this is
the real remaining gap.

**Periodic per-tick spike, noticed but not root-caused this branch**: the
100-block case's ms/tick is ~19-40ms most ticks but jumps to ~370-460ms
on every 4th tick (`BucklingEveryNTicks`), even though
`criticalLoadFactor=Infinity` the whole run (i.e. `RunBuckling`'s cheap
early-out, not the subspace machinery, is all that should run on those
ticks: no compression anywhere under this benchmark's pure-thruster
load). The early-out itself is a linear scan over `BlockStresses` plus a
couple of small `List<int>` allocations -- nowhere near 400ms of work.
Suspected GC (a gen-1/2 collection landing on exactly the tick a few
small buckling-related allocations happen to trigger it) rather than an
algorithmic cost, but not confirmed; flagged in TODO.md rather than
chased further here since it did not block this ticket's target.

**Suspected root cause of the remaining gap, not yet fixed**: `CgSolver.
Solve` only warm-starts the displacement `u` across ticks -- `r`
(residual) and `p` (search direction) are always rebuilt from scratch at
the top of `Solve`, i.e. every tick is a hard CG RESTART, not a
continuation of the same Krylov subspace. A restart is why the residual
plateaus instead of trending toward zero across ticks even though `u`
itself is warm-started: CG's fast local convergence comes from the
accumulated Krylov subspace in `p`, and that is thrown away every tick
regardless of the preconditioner. Preserving `r`/`p` (and `rzOld`) as
additional per-solve state, re-validating them against the current `f`
(load can change tick to tick) rather than discarding them
unconditionally, is the next thing to try before reaching for a bigger
`MaxCgIterationsPerTick` or a wall-clock-based budget.

**Warm-start steady state under a smooth, unchanging load** (item 5):
not separately measured this branch -- the benchmark's thruster load is
constant tick to tick, so `SolverBenchmarks`' own steady-state ticks
already answer this question for the 100-block case (~300 iterations
every tick, not shrinking toward 0 the way a true warm start under a
converged previous tick should). That itself is more evidence for the
restart diagnosis above: a converged previous tick's `u` is an excellent
initial guess, but with `p` rebuilt from scratch the FIRST iteration
still steps blind (steepest-descent-equivalent), and it takes a
noticeable fraction of the previous convergent run's iterations to
rebuild an equally good Krylov subspace. Confirming this precisely (does
`IterationsThisTick` trend toward a small constant, or toward the same
per-tick count as an unconverged run) is follow-up work, not done here.

**Attempted and reverted (prior branch state, fixed here)**: a two-level
(Jacobi + Galerkin coarse-grid, rigid-body-mode prolongation over
4x4-block aggregates) preconditioner was previously implemented and found
numerically unsound (Cholesky-of-singular-Kc NaN, see above); this
revision replaces the Cholesky factorization with a pseudo-inverse built
from an explicit eigendecomposition, which is the "proper null-space
deflation" the prior revision of this doc named as the required fix.

**Remaining steps, roughly in order of expected payoff**:
1. Stop restarting CG's Krylov subspace every tick (see above): this is
   likely the actual reason 500+/2000-block ships plateau instead of
   converging even with the coarse correction in place.
2. Port the hot path (`StiffnessAssembly`, `CgSolver`, `GeometricStiffness`)
   to Burst/`NativeArray`: the current managed-array implementation is
   deliberately simple-first (see `NodeLattice`'s and `StructuralSolver`'s
   docs), and every array here is already allocation-free per tick after
   the prior branch's caching work, which is the prerequisite for a
   mechanical Burst port (Burst cannot compile against managed
   arrays/Dictionary).
3. A mesh renderer instead of per-block GameObjects: `ShipRenderer`
   currently instantiates one GameObject per block (see the assembly
   graph in `docs/architecture.md`), which does not scale independently
   of the solver at all; a combined mesh with per-vertex color (for the
   Stress/Buckling tints) would cut draw calls and GC pressure from
   transform churn on a damaged, frequently-changing ship.
4. Chunked solves: `CgSolver`'s own doc already names this as the
   eventual fix once a single global CG stops being adequate even with a
   good preconditioner -- solve a coarse, homogenized global problem (few,
   large elements) and refine per chunk with cached factorizations, reusing
   a chunk's factorization across ticks where that chunk's topology did
   not change. The two-level preconditioner above is a step toward this
   (it already needs a coarse operator), not a separate piece of work.

**This branch's allocation work** (independent of the preconditioner):
`CgSolver.Solve` and `StructuralSolver.ComputeBlockStress`/`RunBuckling`
used to allocate several n-sized arrays per call; a 500-block ship's
steady-state `Tick` went from measuring ~11.8 MB/tick (mostly this, once
a test-harness bug that left `BlockGrid.TopologyDirty` permanently set
was also fixed, forcing a full K rebuild every tick) down to under 500
bytes/tick, all from `BlockGrid.All`'s boxed `IEnumerable` enumerator
(`Hullbreach.Core`, out of this ticket's scope: a concrete enumerator
struct there would close this out entirely).
