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

## Performance (perf/solver-ticks, 2026-09-21)

All numbers below are **Release** (`tools/plaincs/run_tests.sh` builds
with `-c Release` as of this branch; `dotnet test` defaults to Debug,
which is what every earlier revision of this section reported and is
not what a shipped Unity player runs). Machine: WSL2 on this dev box,
single-threaded managed code, no Burst. `SolverBenchmarks` now runs 20
ticks and prints a `TICKMS` line with min/median/max over the
steady-state ticks plus the tick-0 rebuild on its own, because an
average is exactly what hid the periodic spike below for two branches.

| Ship | DOF | it/tick (steady) | ms/tick median | ms/tick max | one-time rebuild | converged |
| --- | --- | --- | --- | --- | --- | --- |
| 100 blocks | 810 | 0 | 0.17 | 43 (buckling tick) | 95 ms | yes, on tick 1 |
| 500 blocks | 3850 | 400 (capped) | 100 | 104 | 157 ms | no |
| 2000 blocks | 10702 | 400 (capped) | 289 | 294 | 571 ms | no |

**What is real-time at 50 Hz (20 ms/tick) on this machine.** A
100-block ship is, with room to spare: once it converges (tick 1, 127
CG iterations, 22 ms) a steady tick costs 0.17 ms, because CG continues
its Krylov subspace across ticks and immediately sees that the residual
is already under tolerance. Its only cost above that is the buckling
sweep every 4th tick at 29-49 ms, which is over budget on the tick it
lands and would need either `BucklingEveryNTicks` raised or the sweep
split across ticks to hide. 500 blocks is NOT real-time: 100 ms/tick,
five frames' worth, and it does not converge at all inside a
400-iteration budget. 2000 blocks is not close: 289 ms/tick, and the
first tick after any topology change costs an extra 571 ms. In one
line: **about 100 blocks per ship is what this solver runs in real
time today; 500 and up needs the Burst/chunking work below, not more
tuning.**

**Krylov continuation across ticks** (`CgState`, `CgSolver.Solve`'s
`state` parameter, `StructuralSolver.ContinuedFromLastTick` /
`TicksSinceRestart`). `Solve` used to rebuild `r` and `p` from the
warm-started `u` on every call, so every tick was a hard CG restart and
the per-tick budget never compounded. It now carries `r`, `p` and `rz`
between ticks and continues whenever the load is the same system
(`|f_new - f_old| / |f_new| < 1e-6`, one allocation-free fused pass);
`StructuralSolver` invalidates the state on anything that changes K or
the preconditioner (topology rebuild, damage rescale, coarse correction
toggled), which is the invariant `Solve` cannot check for itself.
Effect at 100 blocks: ~300 iterations and 26-32 ms every tick became 0
iterations and 0.17 ms. `CgContinuationTests` pins the compounding
property directly: at a 50-iteration budget the same ship converges in
415 total iterations over 9 ticks, exactly matching one unbudgeted
solve's 415.

Guarding it was most of the work, and two obvious designs are wrong.
Keeping `p` and recomputing `r = f - K u` each tick (residual
replacement) breaks CG outright, because `p` is conjugate to the
residual history that produced it: the 100-block case diverged to 1e14
within two ticks. Gating continuation on agreement between the carried
recursive residual and the true one refuses every continuation, for a
reason worth writing down: **at this conditioning in float32 the two
disagree by ~5% of |f| even within a single tick's solve** (measured at
810 DOF: recursive 5e-4 against a true `|f - K u|` of 3.4, at |f| =
58). That is PCG's attainable-accuracy floor, not drift the
continuation introduced, and it means `Converged` has always meant
"converged to about 5% relative on this ship", never 1e-5. What works
instead is judging health by stagnation ACROSS ticks: every tick that
improves on the best residual since the restart snapshots `u` with it,
and a continuation that breaks down, blows up past 100x that best, or
fails to improve it for 3 consecutive ticks is rolled back to the
snapshot and suspended for 8 ticks. The 500- and 2000-block ships,
which never converge inside their budget, take exactly one rolled-back
tick and then behave like the old restart-every-tick solver, never
publishing a displacement worse than it would have.

**The periodic spike, root-caused.** The 100-block case's ~370-480 ms
every `BucklingEveryNTicks`-th tick was not GC and not a missed
early-out, which is what the previous revision of this section and
TODO.md both guessed. Instrumenting `RunBuckling` showed the early-out
correctly declining to fire: under the benchmark's thruster load plus
inertia relief, **98 of 100 blocks are in genuine compression** (minor
principal stress -217 against a peak von Mises of 374). The claim that
the load never compresses anything was simply false. The real cause is
that the buckling path had no per-tick budget at all: one `Step` runs
`MaxSweepsPerTick` sweeps, each solving one inverse-iteration CG per
block vector, and each of those could run to `CgSolver`'s own
4000-iteration cap, i.e. up to 16 unbounded solves on a tick whose
quasi-static solve is capped at 400 iterations. Two fixes:
`BucklingAnalysis.MaxCgIterationsPerTick` caps the total CG iterations
across every solve in a `Step` (200, from `StructuralSolver`), and the
buckling CG now gets the same `CoarsePreconditioner` as the
quasi-static solve (it was denied one on precision grounds that predate
`ApplyAdditive` projecting both sides; all 8 `BucklingTests` including
the dense-oracle comparison pass unchanged, and that change alone
halved the tick). Result: 370-480 ms -> 29-49 ms. 200 is the measured
floor, not a guess: at 100 the tick costs 9 ms but the inverse
iteration under-converges enough to break
`Column_CriticalLoadFactorScalesWithInverseLengthSquared` (the Euler
1/L^2 trend reads 1.18 instead of 4). Also fixed on the way, the
compression early-out's floor was an absolute -1e-3 on the minor
principal stress, meaning whatever the load scale made it mean; it is
now a fraction (1e-3) of the ship's peak von Mises with a 1e-6 absolute
backstop, so a genuinely tension-only ship early-outs at any load
scale.

**Coarse-operator rebuild cost.** The 16.4 s (Debug) / 4.13 s
(Release) one-time rebuild at 2000 blocks was 3.94 s of
`DenseJacobiEigen` alone, running up to 100 cyclic sweeps over the
465-square `Kc` that 155 aggregates produce. `Kc` is dense and the
decomposition is cubic in it, so `CoarsePreconditioner.Rebuild` now
doubles the aggregate span (4 -> 8 -> 16) until the aggregate count
fits `MaxAggregates` = 64:

| Ship | aggregates | eigen | rebuild total | ms/tick |
| --- | --- | --- | --- | --- |
| 500 blocks | 69 -> 31 (span 8) | 254 ms -> 17 ms | 480 ms -> 157 ms | 142 -> 100 |
| 2000 blocks | 155 -> 53 (span 8) | 3938 ms -> 88 ms | 3809 ms -> 571 ms | 353 -> 289 |

Both ships got faster per TICK as well, because `ApplyAdditive`'s dense
coarse solve is O(m^2) on every CG iteration, not just at rebuild, and
neither ship's residual band moved (60-2600 over 20 ticks, no
divergence). The other option in the ticket, replacing the
eigendecomposition with a Cholesky of `Kc` projected onto the
rigid-mode complement plus a shift, was implemented and measured before
being dropped: it is cheaper still (35 ms at 2000 blocks) and its
deflation is exactly right for the EXACT null space (Qc = P^T applied
to the three global rigid modes, orthonormalized in coarse space,
projected off both sides), but it diverged the 500- and 2000-block
benchmarks to NaN. The reason is the same one the eigenvalue floor
exists for: a 1-block-wide arm's aggregation produces NEAR-null
directions that are not in the global rigid space at all, and a shift
only attenuates those (1/(lambda + 3e-4), up to 3300x) where the
pseudo-inverse's floor drops them outright. A shift large enough to
bound them would smother the correction itself. Do not re-run this
experiment without first fixing the aggregation of 1-wide members.

**Still open after this branch**: 500+ block ships do not converge
inside a 400-iteration budget and are not real-time; `Converged` means
~5% relative, not `Tolerance`, on any ship this ill-conditioned (a
double-precision or residual-replacement-with-restart CG would be the
honest fix); and the buckling tick, while 12x cheaper, still exceeds a
50 Hz frame on its own at 100 blocks. The next steps below are
unchanged in order.

## Performance history (perf/preconditioner, 2026-09-20)

Kept for the reasoning, not the numbers: everything below is a **Debug**
build and predates Krylov continuation, the buckling budget and the
aggregate cap. Where it disagrees with the section above, the section
above is current. Two of its conclusions are now known to be wrong and
are corrected there: the periodic spike was not GC, and the benchmark
load does compress the ship.

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

## Mono/.NET parity for the Structure edit-mode tests (perf/physics-todos, 2026-09-21)

Every test in `Assets/Tests/EditMode/Hullbreach.Structure.Tests` now passes
under BOTH `tools/plaincs` (.NET 8, 207/207 across the whole edit-mode
suite) and the real Unity 6000.0.43f1 editor on Mono (43/43 for the
Structure filter). Four were failing under Mono; three independent root
causes, none of them actually Mono-specific (each was reproducible under
.NET once the rounding was nudged, which is how they were diagnosed).

**1. `CgSolver`'s stopping criterion was absolute below |f| = 1.**
`tolAbs = Tolerance * max(1, |f|)` means a self-equilibrated end load of
|f| ~ 0.04 (every `BucklingTests` load case) stopped CG at ~2.5e-4
relative residual while still reporting `Converged`. The displacement
error left at that point is PRECONDITIONER-DEPENDENT, so switching
`CoarsePreconditioner` on changed the element stress field feeding
`GeometricStiffness` and moved a 12-block column's critical load factor
from the dense oracle's 0.149 to 0.008. Now `tolAbs = Tolerance * |f|`:
`Tolerance` means the same thing at every load scale.

**2. `BucklingAnalysis` never projected its CG SOLUTIONS onto the
rigid-mode complement**, only their right-hand sides, despite the class
doc claiming the invariant. `CgSolver` re-projects only the residual, and
`K` annihilates a rigid-body component, so CG returns one without the
residual ever seeing it; the subspace is warm-started from those vectors,
so the leak compounds sweep over sweep. Under Mono this put a
rigid-dominated vector in the LOWEST slot of a 12-block column's converged
subspace (direction cosines 0.62 / -0.68 / -0.78 against the three rigid
modes), where `phi^T K phi` collapses but `-phi^T K_G phi` does not, i.e.
a spuriously tiny load factor, with the correct mode sitting in slot 1.
One `CgSolver.Project` per block vector per sweep fixes it.

**3. Load factors are now published as direct Rayleigh quotients.**
`ExtractModes` computes `a = phi^T K phi` and `b = -phi^T K_G phi` with
the sparse operators against the published shape, in double accumulation,
and publishes `a / b`; the reduced eigenproblem's `1 / mu` is now only a
pre-filter for "this slot has no signal". This takes Mono's and .NET's
differing float intermediate widths out of the published number (the
reduced route runs the shape through a Cholesky, a Jacobi sweep and a
reciprocal), and gives a meaningful rejection test the reduced route
cannot: a slot whose `K_r` pivot went near-singular still yields a
finite, stable-looking lambda, but `a` shows directly that its shape
carries no strain energy. Checked at PUBLISH time only, never during a
sweep, because pivots legitimately dip mid-sweep (a previous attempt at
zeroing those rows broke healthy convergence). Measured agreement with
`DenseEigenOracle` on the same K/K_G pair: 0.14947617 vs 0.14946215
(N=12), 0.08331143 vs 0.08333989 (N=16), 14.036202 vs 14.036202 (2x2
blob).

**`CoarsePreconditioner` is now exactly rigid-mode-free by
construction**: `ApplyAdditive` projects both its input residual and its
coarse correction onto the complement of the ship's three global
rigid-body modes, so `M^-1 = D^-1 + (I-Q) P Kc^+ P^T (I-Q)`. Projecting
BOTH sides, not just the output, is what keeps `M^-1` symmetric, which
PCG's convergence theory needs. This replaces the one-sided
`Project(z, modes)` `CgSolver` used to apply after the coarse call (which
also re-projected the Jacobi term, something the plain-Jacobi path
deliberately does not do): the leak is removed at its source instead.
`CoarsePreconditionerTests.CoarseCorrection_AnnihilatesRigidModes`'s
threshold went from a data-dependent 5e-3 (Mono measured 0.0496 against
an effective 0.0456) to 1e-4 relative.

**Two test assertions were wrong, not the solver** (both now agree with
`DenseEigenOracle` exactly, which is why this is a test fix and not a
regression): `SmallBlob_HasNoLowLoadFactor` asserted `> 20` against a
blob whose true critical load factor is 14.036202 (the old number came
from the reduced path's rounding), now `> 10`; and
`TwoSeparateArms_UnderCompression_BuckleIndependently` asserted that the
two lowest sub-critical modes' TOP blocks sit on different arms, on a
ship that is symmetric under reflection about the diagonal and therefore
has every buckling eigenvalue at multiplicity 2. The subspace returns the
symmetric/antisymmetric combinations of each degenerate pair (measured
strain-energy splits 0.479/0.479, 0.499/0.499, 0.481/0.481,
0.496/0.496 between the arms), so which arm holds the single largest
block is decided by rounding alone. It now asserts what is actually
runtime-independent and what the test's own doc says it cares about: the
buckled blocks span both arms.

