# Hullbreach.Structure reference

Per-type reference for `Assets/Scripts/Hullbreach.Structure`, linked from the code by
`// frob:doc docs/reference/hullbreach-structure.md#<anchor>`. One heading per
public type; each heading carries the `frob:describes` lines for that
type and its public members. Architecture-level context lives in
[architecture.md](../architecture.md).

### DamageModel

<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Failure/DamageModel.cs::DamageModel -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Failure/DamageModel.cs::DamageModel.FailRatio -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Failure/DamageModel.cs::DamageModel.RecoverRatio -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Failure/DamageModel.cs::DamageModel.Accumulate -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Failure/DamageModel.cs::DamageModel.SofteningFactor -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Failure/DamageModel.cs::DamageModel.ShouldDetach -->

Damage accumulation as cheap pseudo-plasticity. "Ductile" means YIELD, not
instant fracture: if blocks snapped the moment von Mises crossed yield it
would read brittle no matter what the criterion is called, and real
plasticity (return mapping, history variables, a nonlinear solve) is far
too much machinery here. The substitute: on overshoot, accumulate damage
and SOFTEN the block (reduce its E and its yield stress). A softened block
sheds load to its neighbors, which is what yielding actually does: you get
visible bending before breaking, load redistribution for free, and the
solve stays linear. It also reuses the E multiplier the upgrades already need.

- `FailRatio`/`RecoverRatio`: break above `FailRatio`, but do not un-break
  until dropping below `RecoverRatio`: without this hysteresis band,
  blocks would chatter in and out of existence right at the threshold.
- `Accumulate`: accumulates damage proportional to the overshoot above
  yield, clamped to 0..1 (represented as a byte 0..255). Below yield
  (ratio <= 1) damage does not change: this models plastic accumulation,
  not elastic loading/unloading fatigue.
- `SofteningFactor`: stiffness multiplier from damage, floored at 0.05 so
  a block never reaches exactly zero stiffness, which would make K
  singular in a way inertia relief does not account for.
- `ShouldDetach`: should this block detach this tick? Ductile failure uses
  the hysteresis band described above. Brittle failure is instantaneous
  (no hysteresis: a crack does not partially open).

### StressCriteria

<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Failure/StressCriteria.cs::StressCriteria -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Failure/StressCriteria.cs::StressCriteria.VonMises -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Failure/StressCriteria.cs::StressCriteria.Principal -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Failure/StressCriteria.cs::StressCriteria.DuctileRatio -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Failure/StressCriteria.cs::StressCriteria.BrittleRatio -->

Reduces a 2D stress tensor to the scalars the failure model compares
against. Both invariants are about ten flops from the same tensor, so
computing both is effectively free.

VON MISES is `sqrt(3 J2)`, built from the DEVIATORIC (shape-changing) part
of stress only. It deliberately ignores hydrostatic pressure: you cannot
yield metal by squeezing it uniformly from every side. That
pressure-insensitivity is exactly what makes it the DUCTILE criterion.

MAX PRINCIPAL (Rankine) is the BRITTLE criterion: cracks open
perpendicular to maximum tension, and pressure very much does matter,
which is why brittle solids are hugely stronger in compression.

The ductile/brittle split is physically real, not a gameplay hack:
materials embrittle at high strain rate. Slow structural loading yields;
a projectile impact spalls.

- `VonMises`: von Mises equivalent stress, plane stress.
- `Principal`: principal stresses; returns the larger in `major`, the
  smaller in `minor`.
- `DuctileRatio`: ductile utilization: von Mises over the
  (damage-reduced) yield stress. Drives the S37 green-to-red tint. 1.0
  means failing. The denominator shrinks with damage (floored via
  `DamageModel`'s softening curve) so a damaged block is STRUCTURALLY
  weaker: sustained fire eventually causes a structural failure rather
  than only an HP kill.
- `BrittleRatio`: brittle utilization from the impulsive load case. Uses
  max TENSILE principal stress against spall strength, and the (much
  larger) compressive limit separately: that asymmetry is most of what
  makes armor feel like armor.

### NodeLattice

<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/NodeLattice.cs::NodeLattice -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/NodeLattice.cs::NodeLattice.NodesPerElement -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/NodeLattice.cs::NodeLattice.Offsets -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/NodeLattice.cs::NodeLattice.PackNode -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/NodeLattice.cs::NodeLattice.UnpackNode -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/NodeLattice.cs::NodeLattice.NodesOf -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/NodeLattice.cs::NodeLattice.BuildNodeMap -->

Global node addressing for the Q8 mesh, on a lattice at TWICE the block
resolution. Block `(i,j)` owns the 8 doubled-lattice points around it,
excluding the center (Q8 is the serendipity element; Q9 would use the
center too):

```
    (2i,2j+2)---(2i+1,2j+2)---(2i+2,2j+2)
        |                          |
    (2i,2j+1)                 (2i+2,2j+1)      center (2i+1,2j+1)
        |                          |           is NOT a node
     (2i,2j)----(2i+1,2j)-----(2i+2,2j)
```

The point of this scheme: node SHARING between adjacent blocks falls out
by construction. Block `(i+1,j)` independently computes `(2i+2, 2j)`,
`(2i+2, 2j+1)` and `(2i+2, 2j+2)` and gets byte-identical ids. There is no
dedup pass and no tolerance-based point merging to get wrong.

Node ordering is the standard Q8 convention: corners 1-4 counterclockwise
from the lower left, then midsides 5-8 starting between corners 1 and 2.

- `NodesPerElement`: 8, the Q8 node count.
- `Offsets`: doubled-lattice offsets of the 8 nodes, relative to `(2i,
  2j)`, in standard Q8 order.
- `PackNode`: packs a doubled-lattice coordinate `(dx, dy)` into a single
  injective int id, by biasing both axes into non-negative range (Bias =
  1024: BlockKey covers x,y in [-128,127], so the doubled lattice covers
  roughly [-256,256], and 1024 leaves comfortable headroom) and packing x
  into the high bits, y into the low bits (Shift = 16, since biased
  coordinates fit in 16 bits).
- `UnpackNode`: exact inverse of `PackNode`.
- `NodesOf`: writes the 8 global node ids of block `(x,y)` into `into`
  (length 8), in the standard `Offsets` order.
- `BuildNodeMap`: builds the dense node map for a whole grid: assigns
  each distinct node id a contiguous index 0..n-1. Blocks are visited in
  ascending key order (and nodes within a block in standard Q8 order) so
  that two independent builds over the same grid produce byte-identical
  maps: required for client/server agreement without shipping the map itself.

### Q8Element

<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/Q8Element.cs::Q8Element -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/Q8Element.cs::Q8Element.NodeCount -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/Q8Element.cs::Q8Element.DofCount -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/Q8Element.cs::Q8Element.ReferenceNodes -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/Q8Element.cs::Q8Element.NuFor -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/Q8Element.cs::Q8Element.KHatFor -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/Q8Element.cs::Q8Element.ShapeFunctions -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/Q8Element.cs::Q8Element.ShapeDerivatives -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/Q8Element.cs::Q8Element.StrainDisplacement -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/Q8Element.cs::Q8Element.ConstitutiveUnit -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/Q8Element.cs::Q8Element.UnitStiffness -->

The 8-node serendipity quadrilateral, and its unit stiffness matrix.

WHY Q8 AND NOT Q4: the bilinear Q4 cannot represent the curvature a
bending member needs, so it fakes it with spurious shear and comes out far
too stiff ("shear locking"). For a ship made of beams and braces that
would be disqualifying.

WHY A *UNIT* STIFFNESS: for isotropic plane stress, `D = E / (1 - nu^2) *
[[1, nu, 0], [nu, 1, 0], [0, 0, (1-nu)/2]]`, so E factors out as a scalar.
Since every block is the same axis-aligned unit square, `K_e = E *
KHat(nu)` with KHat precomputed ONCE per Poisson class at startup.
Stiffness upgrades and damage softening are then a scalar multiply, never
an integration.

The uniform grid also collapses the isoparametric machinery: the map is
`x = x_c + (h/2) * xi`, so the Jacobian is the constant `(h/2) * I` and
`det J = h^2 / 4`. No per-Gauss-point Jacobian inversion.

- `ReferenceNodes`: node positions on the reference square `[-1,1]^2`, in
  standard Q8 order. Tests use these to build rigid-body modes.
- `NuFor`: Poisson's ratio per quantized class. 0 = ordinary structural
  steel (0.30), 1 = a softer/rubbery class (0.25), 2 = a stiffer, more
  incompressible class (0.35). Quantized because KHat is not linear in
  nu, so a continuous nu would defeat the precomputation.
- `KHatFor`: the cached unit stiffness matrix (16x16, E=1, thickness 1)
  for a given Poisson class and block side length h.
- `ShapeFunctions`/`ShapeDerivatives`: the 8 shape functions (and their
  xi/eta derivatives) at `(xi, eta)`.
- `StrainDisplacement`: the 3x16 strain-displacement matrix at `(xi,
  eta)` for a block of side `h`.
- `ConstitutiveUnit`: plane-stress constitutive matrix divided by E, i.e.
  `D = E * DHat(nu)`.
- `UnitStiffness`: KHat, the 16x16 element stiffness for E = 1, thickness
  1, side `h`; `KHat = integral of B^T DHat B` over the element, by 3x3
  Gauss. 2x2 (reduced integration) would admit a spurious zero-energy
  mode per element; this is precomputed once at startup so the extra
  cost is irrelevant.

### LoadVector

<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/LoadVector.cs::LoadVector -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/LoadVector.cs::LoadVector.LoadVector -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/LoadVector.cs::LoadVector.QuasiStatic -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/LoadVector.cs::LoadVector.Impulsive -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/LoadVector.cs::LoadVector.AddPointForce -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/LoadVector.cs::LoadVector.ApplyInertiaRelief -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/LoadVector.cs::LoadVector.RigidBodyModes -->

Builds the right-hand side, including INERTIA RELIEF.

THE PROBLEM: a ship in space has no supports, so K is singular with a
3-dimensional null space (translate x, translate y, rotate). `K u = f` has
a solution only when f is orthogonal to that null space, which physically
means net force zero and net torque zero. An accelerating ship does not
satisfy that.

THE WRONG FIX: pin the core. One line, but it is false physics: the
pinned node supplies reaction forces, so stress piles up at the core and a
distant thruster reads as a lever against it.

THE RIGHT FIX (this file): d'Alembert. Work in the accelerating frame and
add the inertial body force to every block:

```
a = F_net / M,   alpha = tau_net / I
f_eff_i = f_applied_i - m_i * (a + alpha x r_i)
with  alpha x r = alpha * (-r.y, r.x)  in 2D
```

Summing forces gives `F_net - M a = 0` and summing moments gives
`tau_net - I alpha = 0`, so `f_eff` is self-equilibrated BY CONSTRUCTION
and the system becomes solvable with nothing pinned.

The solution u is still only determined up to a rigid-body mode, but B
annihilates rigid modes, so the STRESS does not care. Only orthogonalize u
against the modes if you want to draw the deformed shape.

- `QuasiStatic`: the quasi-static case: thrust, gravity wells, contact.
  Checked against von Mises (ductile).
- `Impulsive`: the impulsive case: projectile impacts this tick only.
  Checked against max tensile principal stress (brittle). Separate
  because linear FE superposes exactly, so the two load cases can share
  one K and one assembly.
- `AddPointForce`: scatters a force applied at a world (ship-local) point
  into the nodal load vector, distributing it over the containing
  element's nodes by shape-function weight.
- `ApplyInertiaRelief`: applies inertia relief to `target`, in place.
  Returns the rigid-body acceleration it solved for, which the caller
  also wants for integrating the actual ship motion. Net force/torque are
  read off of `target` itself, and inertial body forces are then
  distributed per BLOCK across that block's 4 corner nodes (a
  lumped-mass simplification; midside nodes carry no mass in this
  scheme, which is standard practice and keeps the distribution
  trivial). Uses point-mass inertia (mass concentrated at each block's
  center), NOT `grid.Mass.InertiaAboutCenterOfMass`: that value also
  folds in each block's own spin inertia, which has no counterpart in
  this lumped-corner-mass distribution; using the wrong I here would
  make alpha inconsistent with how torque is actually cancelled, leaving
  a residual net torque.
- `RigidBodyModes`: the three rigid-body modes as DOF vectors, for the
  projection above and for the solver's orthogonalization: translate x =
  `(1, 0)` at every node, translate y = `(0, 1)` at every node, rotate =
  `(-y, x)` at the node at `(x, y)`.

### StiffnessAssembly

<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/StiffnessAssembly.cs::StiffnessAssembly -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/StiffnessAssembly.cs::StiffnessAssembly.NodeMap -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/StiffnessAssembly.cs::StiffnessAssembly.NodeRestPositions -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/StiffnessAssembly.cs::StiffnessAssembly.DofCount -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/StiffnessAssembly.cs::StiffnessAssembly.RowPointers -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/StiffnessAssembly.cs::StiffnessAssembly.ColumnIndices -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/StiffnessAssembly.cs::StiffnessAssembly.Rebuild -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/StiffnessAssembly.cs::StiffnessAssembly.Multiply -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/StiffnessAssembly.cs::StiffnessAssembly.Diagonal -->

Scatters each element's 16x16 into the sparse global K. The accumulation
at shared nodes IS the structural connection: two blocks are joined
precisely because they write into the same rows. K comes out symmetric
positive SEMI-definite. It is singular, by three, because a free-floating
ship has three rigid-body modes: see LoadVector for how that is handled
rather than papered over.

- `NodeMap`: dense node id -> contiguous index, index*2 is the DOF
  offset. Rebuilt whenever `Rebuild` runs.
- `NodeRestPositions`: rest position of each dense node, in ship-local
  units (lattice coordinate / 2). Needed by LoadVector to locate the
  element containing a point and by rigid-body mode construction.
- `RowPointers`/`ColumnIndices`: the CSR row pointers/column indices of
  K, exposed read-only so a second matrix with the SAME sparsity pattern
  (GeometricStiffness) can be built without re-deriving the pattern from
  the grid. Chosen over duplicating StiffnessAssembly's
  element-loop/BuildCsr machinery or making the element matrix
  pluggable: the sparsity pattern of K and K_G is identical (both come
  from the same node connectivity), so sharing the pattern and
  scattering only values is the smaller, more obviously-correct surface.
  Column indices are sorted ascending within each row. Callers must not
  mutate either array.
- `Rebuild`: build sparse K for the whole grid, from scratch. Rebuild
  only when topology is dirty (or after a stiffness-affecting damage
  change, since this recomputes everything rather than rescaling in
  place: simplicity over the incremental-rescale optimization, since
  assembly at this scale is cheap). Every `(row,col)` pair that shares an
  element is kept in the pattern even when THIS element's value is
  exactly zero: the pattern must be a superset of every matrix that can
  ever be assembled over the same connectivity, in particular
  GeometricStiffness, whose local 16x16 is nonzero at some `(i,j)` where
  KHat happens to be exactly zero.
- `Multiply`: `y = K * x`. The only operation CG needs.
- `Diagonal`: writes the diagonal of K into `into`, for the Jacobi preconditioner.

### GeometricStiffness

<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/GeometricStiffness.cs::GeometricStiffness -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/GeometricStiffness.cs::GeometricStiffness.DofCount -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/GeometricStiffness.cs::GeometricStiffness.AttachSparsity -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/GeometricStiffness.cs::GeometricStiffness.Rebuild -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/GeometricStiffness.cs::GeometricStiffness.Multiply -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/GeometricStiffness.cs::GeometricStiffness.ElementMatrix -->

The geometric (initial-stress) stiffness K_G: the part of the tangent
stiffness that comes from stress already in the structure rather than
from material response. It is what makes a compressed member go unstable:
K_G is (locally) negative-definite along a compressive principal
direction, so `K + lambda*K_G` loses positive-definiteness as the load
factor lambda grows. That crossing point is buckling.

PER-ELEMENT G AND S: at a Gauss point, G is the 4x16 matrix of
displacement-gradient rows (not strain, gradients, so shear does not get
the usual engineering factor of 2):

```
row 0: dN/dx for u   row 1: dN/dy for u
row 2: dN/dx for v   row 3: dN/dy for v
```

and `S = blockdiag(sigma, sigma)` with `sigma = [[Sxx,Txy],[Txy,Syy]]`, so
`K_G_e = integral G^T S G dV`, by the same 3x3 Gauss rule as
`Q8Element.UnitStiffness`.

CONSTANT-STRESS SIMPLIFICATION: this uses ONE stress tensor per element
(the element-CENTER stress StructuralSolver already computes for
BlockStress), not the true Gauss-point stress, which varies over the
element. That is an approximation: a per-Gauss-point stress, recovered
the same way BlockStress recovers the center stress but at each of the 9
points, would be more accurate. It is deliberately not done here: it
would 9x the per-tick stress recovery cost for a correction that only
matters right at a stress concentration, and the buckling analysis
already only runs every `BucklingEveryNTicks` ticks.

SPARSITY: shares K's exact CSR row/column pattern via `AttachSparsity`
(see StiffnessAssembly.RowPointers for why sharing the pattern beats
re-deriving it). Only `Rebuild`'s VALUE scatter runs per
buckling-eligible tick; `Multiply` is the same sparse mat-vec as
StiffnessAssembly, over K_G's own values.

- `DofCount`: mirrored from the attached StiffnessAssembly so callers can
  size DOF-length work vectors.
- `AttachSparsity`: adopts `assembly`'s current CSR row/column pattern by
  reference (StiffnessAssembly does not mutate those arrays in place;
  Rebuild always allocates fresh ones), so holding a reference across
  ticks is safe until the next topology Rebuild, at which point the
  caller must call AttachSparsity again. Resizes the value array only
  when the pattern's nnz actually changed.
- `Rebuild`: re-scatters K_G's values for the current stress state, in
  place. Allocation-free on the hot path. Blocks are visited in ascending
  key order (matching NodeLattice.BuildNodeMap) so float accumulation at
  shared nodes happens in the same order every time: required for the
  determinism the buckling analysis promises.
- `Multiply`: `y = K_G * x`, identical CSR mat-vec pattern to StiffnessAssembly.Multiply.
- `ElementMatrix`: the 16x16 element geometric stiffness for a constant
  element stress `(Sxx,Syy,Txy)` on a side-`h` block: integral of `G^T S
  G` by 3x3 Gauss, mirroring Q8Element.UnitStiffness's structure exactly
  so the two are easy to compare line for line.

### CgState

<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CgState.cs::CgState -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CgState.cs::CgState.LoadChangeTolerance -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CgState.cs::CgState.StagnationTicks -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CgState.cs::CgState.ResidualGrowthSlack -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CgState.cs::CgState.SuspensionTicks -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CgState.cs::CgState.DivergenceFactor -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CgState.cs::CgState.ContinuedFromLastTick -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CgState.cs::CgState.TicksSinceRestart -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CgState.cs::CgState.Invalidate -->

Caller-owned Krylov state that lets `CgSolver.Solve` CONTINUE one
conjugate-gradient run across several ticks instead of restarting it
every tick.

WHY THIS EXISTS: CgSolver warm-starts the displacement `u`, but `u` alone
is not what makes CG fast. CG's superlinear phase comes from the Krylov
subspace accumulated in the search direction `p` (and the conjugacy
bookkeeping in `rz`), and rebuilding `p` from the preconditioned residual
at the top of every Solve throws that away: the first iteration of every
tick is a blind steepest-descent step, and a ship too wide to converge
inside `MaxCgIterationsPerTick` never compounds its per-tick budget. Its
residual oscillates around a plateau across ticks instead of trending to
zero, which is exactly what the 500- and 2000-block SolverBenchmarks
cases did.

WHEN A CONTINUATION IS VALID, and why this is the dangerous part: a
continued CG is a single CG run on ONE fixed linear system. Its conjugacy
relations (`p_i^T K p_j = 0`) are statements about a particular K, a
particular preconditioner M^-1 and a particular right-hand side f. Change
any of the three between ticks and the stored `r`/`p`/`rz` describe a
system that no longer exists: the iteration does not merely converge
slower, it minimizes the wrong quadratic and can diverge. So:
- f: checked numerically by Solve on every call
  (`|f_new - f_old| / |f_new| < LoadChangeTolerance`).
- K and M^-1: NOT detectable from inside Solve, so the owner must call
  `Invalidate` on any topology rebuild, damage rescale, or preconditioner
  change. StructuralSolver does this in the same branch that rebuilds
  the assembly and the coarse operator; a new caller that forgets is the
  one way to misuse this class.

WHAT IS CARRIED: the residual `r`, the search direction `p` and `r.z`,
i.e. everything one CG iteration needs from the previous one, and all
three together or none. Two cheaper-looking variants were tried and are
both WRONG. Keeping `p` alone and recomputing `r = f - K u` each tick
breaks the alpha/beta relations outright: the 100-block benchmark
diverged to 1e14 within two ticks. Gating the continuation on agreement
between the carried recursive residual and the true one is also wrong,
though for a subtler reason: at this conditioning in float32 the two
disagree by 5% of `|f|` even WITHIN a single tick's solve (measured on
the 100-block case, recursive 5e-4 against a true 3.4 at `|f| = 58`),
which is PCG's attainable-accuracy floor rather than anything
continuation did, so such a gate simply refuses every continuation.

HOW A BAD CONTINUATION IS CAUGHT: every tick that improves on the best
residual since the restart snapshots `u` with it, and a continuation that
then breaks down, blows up (`ResidualGrowthSlack`) or simply stops
improving (`StagnationTicks`) is rolled back to that snapshot and
suspended for `SuspensionTicks` ticks. So a ship that should not be
continuing (the 500- and 2000-block benchmark ships, which do not
converge inside their budget at all) pays one tick and then behaves
exactly like the old restart-every-tick solver, and never publishes a
displacement worse than that solver's.

ALLOCATION: buffers are sized once per dof count and reused, so a
steady-state tick allocates nothing here.

- `LoadChangeTolerance`: relative change in the load vector above which
  Solve restarts instead of continuing. 1e-6 is "the same load,
  re-derived in float" (inertia relief and the point-force scatter
  re-run every tick and are not bit-stable), not "a load that drifted a
  little": a genuinely drifting load is a different linear system and
  gets an honest restart.
- `StagnationTicks`: consecutive ticks a continued run may fail to
  improve on its best residual before it is rolled back and restarted.
  The primary health check, because it separates a ship converging
  slowly (bounces upward, but the best keeps falling) from a ship not
  converging at all (the best stops moving while the current creeps up).
  3 is two bounces' worth of patience.
- `ResidualGrowthSlack`: emergency bound on a single tick, as a multiple
  of the best residual since the restart. 100x is far outside anything a
  healthy run does and far inside the ~1000x an unguarded diverging run reached.
- `SuspensionTicks`: restart-only ticks to serve after a rolled-back
  continuation before trying again. Not permanent, because a ship can be
  temporarily hard (a transient load spike) without being permanently
  unsolvable; not zero, because retrying every tick on a ship that
  genuinely cannot converge inside its budget would throw away half of
  every second tick.
- `DivergenceFactor`: the same emergency bound as `ResidualGrowthSlack`,
  but checked once per ITERATION rather than once per tick, so a
  continuation that has genuinely gone bad stops immediately instead of
  spending the rest of the budget making `u` worse. Looser than the
  per-tick bound because a single iteration's residual is far noisier
  than a whole tick's.
- `ContinuedFromLastTick`: true when the most recent Solve continued the
  previous tick's Krylov subspace rather than restarting it.
- `TicksSinceRestart`: ticks (Solve calls) since the last restart, 0 on
  the tick that restarted.
- `Invalidate`: drops the stored Krylov subspace, so the next Solve
  rebuilds `r`/`p` from the current `u` and `f`. Call whenever K or the
  preconditioner changed: nothing else can detect that.

### CgSolver

<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CgSolver.cs::CgSolver -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CgSolver.cs::CgSolver.MaxIterations -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CgSolver.cs::CgSolver.Tolerance -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CgSolver.cs::CgSolver.LastIterationCount -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CgSolver.cs::CgSolver.LastResidualNorm -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CgSolver.cs::CgSolver.Converged -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CgSolver.cs::CgSolver.ContinuedFromLastTick -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CgSolver.cs::CgSolver.Solve -->

Jacobi-preconditioned conjugate gradient, warm-started.

CG only needs `K * v`, never K itself, which is why StiffnessAssembly
exposes just `Multiply`.

CONVERGENCE, and why this eventually stops scaling: iterations grow like
`sqrt(condition number)`, and for 2D elasticity `kappa ~ h^-2`, so the
count grows like the ship's width in elements. The intuition is exact:
each multiply by K propagates information exactly one element further, so
telling the bow that the stern fired takes ~L iterations. Warm-starting
hides this while loads change smoothly, but combat changes topology and
destroys the warm start, which is when you would need it most.

The fix, when you get there, is chunking: solve a coarse homogenised
problem globally (few, large elements, so information crosses fast) and
refine per chunk with cached factorisations. Do NOT build that yet. Keep
this interface taking a region and its boundary conditions, and the flat
version becomes the one-chunk case for free.

- `MaxIterations`: iteration cap. WAS 200, which is far below what even a
  modest ship needs: a 1-wide, 32-block column (326 dof) measured at
  ~650 iterations to hit Tolerance, so the old cap silently returned an
  under-converged (force-imbalanced) displacement field for anything
  longer than a stub. That fed wrong element stresses into
  GeometricStiffness and biased BucklingAnalysis's Rayleigh quotients
  enough to break the Euler `P_cr ~ 1/L^2` trend (it read closer to
  `1/L`, and non-monotonically at that). 4000 is cheap per call (a plain
  sparse mat-vec) and CG still exits the moment Tolerance is met.
- `Tolerance`: stopping criterion, RELATIVE to `|f|`: Solve exits once
  `|r| <= Tolerance * |f|`. It used to be `Tolerance * max(1, |f|)`,
  which is the same thing only for ships loaded past `|f| = 1` and an
  ABSOLUTE bound of 1e-5 below that. Every buckling test drives a
  self-equilibrated end load with `|f|` on the order of 0.04, so that
  floor let CG stop at ~2.5e-4 relative residual while still reporting
  Converged, and the displacement error left at that point was
  preconditioner-dependent, moving a 12-block column's critical load
  factor to 0.008 against the dense oracle's 0.149. A relative criterion
  means Tolerance means the same thing at every load scale.
- `LastIterationCount`: iterations the last Solve actually took. Watch
  this grow with ship size: it is the scaling wall, made visible.
- `LastResidualNorm`: Euclidean norm of the (rigid-mode-projected)
  residual after the last Solve returned, whether or not it met
  Tolerance: StructuralSolver uses this to decide whether a tick's
  partial solve is trustworthy enough to run buckling against.
- `Converged`: true when the last Solve's `LastResidualNorm` actually
  met Tolerance before hitting MaxIterations; false means the returned
  `u` is a partial, still-improving warm start.
- `ContinuedFromLastTick`: true when the last Solve continued the
  caller-owned CgState's Krylov subspace instead of restarting from `u`.
- `Solve`: standard PCG with `M = diag(K)`, optionally augmented by a
  deflated coarse correction (see CoarsePreconditioner) when `coarse` is
  non-null: `M^-1 = D^-1 + P Kc^+ P^T`. Warm-starts from the `u` passed
  in. Because K is singular, the residual (and the initial load) are
  re-projected onto the complement of the rigid-body modes every
  iteration, so rounding cannot slowly excite them: Gram-Schmidt every
  iteration is affordable at this problem size. KRYLOV CONTINUATION:
  pass a caller-owned CgState to continue the previous call's iteration
  (same r, p and rz) instead of restarting the subspace from `u`. Read
  CgState's doc before doing so: a continuation is only valid while K,
  the preconditioner and f all stay put, and only the last of those
  three is something this method can check for itself.
  STAGNATION GUARD: tracks the best (smallest) residual norm seen and
  how long ago it improved. A right-hand side that is genuinely near
  machine-zero (e.g. BucklingAnalysis solving `K*y = -K_G*v` for a v the
  current, nearly-uncompressed K_G maps to machine-zero) cannot be
  driven under `tolAbs` by more iterations; without this, raising
  MaxIterations to cover the large, genuinely slow-converging problems
  this solver also sees meant every such degenerate call burned the
  ENTIRE cap chasing round-off noise that was never going to shrink.
  BEST-U SNAPSHOT AND ROLLBACK: `u` is what the stress field and the
  next tick's warm start are read from, so a continuation that wanders
  must never leave it worse than the best this run ever had. Every tick
  that improves on the best residual since the restart snapshots `u`
  alongside it; a continuation that then goes bad restores that
  snapshot instead of publishing its own worse answer. "Goes bad" is
  judged by STAGNATION ACROSS TICKS, not by this tick's residual alone:
  PCG minimizes the energy norm, not `|r|`, so a healthy continued run
  under a small per-tick budget bounces its residual upward for several
  ticks at a time; what the ships that must NOT continue look like
  instead is a best residual that stops improving at all while the
  current one creeps upward tick after tick (the 500-/2000-block
  plate+arm case: unguarded, that creep reached ~1e5 from ~1e2 over ten ticks).

### CoarsePreconditioner

<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CoarsePreconditioner.cs::CoarsePreconditioner -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CoarsePreconditioner.cs::CoarsePreconditioner.AggregateBlockSpan -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CoarsePreconditioner.cs::CoarsePreconditioner.AggregateCount -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CoarsePreconditioner.cs::CoarsePreconditioner.MaxAggregates -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CoarsePreconditioner.cs::CoarsePreconditioner.MaxAggregateBlockSpan -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CoarsePreconditioner.cs::CoarsePreconditioner.LastAggregateBlockSpan -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CoarsePreconditioner.cs::CoarsePreconditioner.Rebuild -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/CoarsePreconditioner.cs::CoarsePreconditioner.ApplyAdditive -->

Deflated two-level additive preconditioner: adds a coarse, global
correction on top of CgSolver's plain Jacobi so information can cross the
whole ship in O(1) preconditioner applications instead of one CG
iteration per element along the longest path.

CONSTRUCTION: nodes are grouped into aggregates of `AggregateBlockSpan` x
`AggregateBlockSpan` blocks by lattice coordinate (deterministic, sorted
by aggregate coordinate so rebuilds are bit-reproducible). Each aggregate
gets its own 3 rigid-body modes (translate x, translate y, rotate about
the aggregate's centroid), Gram-Schmidt orthonormalized within the
aggregate; stacking every aggregate's 3 modes as columns is the
prolongation P (never materialized densely: P is block-diagonal by
aggregate, so applying it or its transpose is O(dof)). The coarse
operator is `Kc = P^T K P`, a dense `(3 * aggregateCount)`-square matrix,
built once per topology change from one K-multiply per column.

WHY A PSEUDO-INVERSE, NOT A CHOLESKY, OF Kc: a previous attempt built
this same Kc and factored it with Cholesky, and got NaNs. The reason is
structural, not a bug to patch around: K's global 3-dimensional
rigid-body null space lies exactly in `range(P)` (every aggregate's own
rigid modes sum, weighted correctly, to the ship's global rigid modes),
so Kc inherits that same 3-dimensional null space and is exactly
singular, not just ill-conditioned. Cholesky of a singular matrix does
not fail loudly; it produces a near-zero pivot, divides by it, and hands
back coefficients amplified by whatever rounding noise happened to leak
into that pivot, which is the NaN. The fix used here is to diagonalize Kc
once (DenseJacobiEigen, cheap at this size) and build its Moore-Penrose
pseudo-inverse, DROPPING every eigenvalue below 1e-6 times the largest:
those dropped directions are exactly Kc's null space, so `Kc^+` applied
to `P^T r` is, by construction, zero on them. Combining `M^-1 = D^-1
(Jacobi) + P Kc^+ P^T` keeps the whole preconditioner symmetric positive
semidefinite on the residual's actual subspace (the complement of the
rigid modes, which CgSolver already projects onto every iteration), so
PCG's convergence theory still applies.

ALLOCATION: every array is sized once by `Rebuild` (call only on a
topology change, same cadence as StiffnessAssembly.Rebuild) and reused;
`ApplyAdditive` is allocation-free.

SCALE BEFORE DIAGONALIZING: Kc's entries carry K's actual stiffness units
(can be 1e5-1e8), but DenseJacobiEigen's sweep-termination tolerance is
an ABSOLUTE bound on the remaining off-diagonal sum of squares. Left
unscaled, Jacobi silently stops (at maxSweeps) with real off-diagonal
error still in the matrix, and the eigenvalues Kc's exact null space maps
to land at whatever rounding noise is left, not at zero, some of which
measured above the pseudo-inverse's 1e-6-relative floor, giving those
directions a large-but-finite `1/lambda` instead of being dropped
(observed directly on the 500-block benchmark: residual grew from 1e7 to
1e13 over 10 ticks before this fix). Dividing by Kc's largest entry first
makes the matrix O(1) so the same absolute tolerance is now meaningfully
tight, and multiplying the resulting eigenvalues back by that scale undoes
it exactly (eigenvectors are scale-invariant).

The eigenvalue floor is 3e-4 of the largest eigenvalue, not the more
obvious 1e-6: measured directly on the 500-block plate+arm benchmark, a
1e-6 floor let through near-null (but not exactly null) directions from
the 1-wide arm's poorly-conditioned aggregation (neighboring aggregates
along a 1-block-wide strip have almost-parallel local rotate modes, not
the exact 3-dimensional global rigid-body null space), each with a huge
but finite `1/lambda` that amplified rounding noise every CG iteration
and diverged the residual from 1e4 to NaN over 10 ticks. 3e-4 costs a
little coarse-correction quality on those directions (they fall back to
Jacobi-only) in exchange for never dividing by anything that small.

WHY BOTH SIDES OF `ApplyAdditive` ARE PROJECTED, not just the output:
`Kc^+`'s eigenvalue floor drops Kc's null space only to within
DenseJacobiEigen's rounding, so the raw correction leaks a small
rigid-body component, which CG cannot see (K annihilates it) but which
accumulates in `p` and `u` every iteration and destroys the cancellation
in the `B*u` strain evaluation downstream: that is what corrupted three
BucklingTests' geometric stiffness under Mono, whose float rounding
differs from .NET's. Making the correction EXACTLY rigid-mode-free by
construction removes the leak at its source instead of mopping it up in
CgSolver. The projection is applied on the INPUT too, not only the
output, because `(I-Q) M0` is not a symmetric operator while
`(I-Q) M0 (I-Q)` is (Q is a symmetric projector, Kc^+ is symmetric), and
PCG's convergence theory needs M^-1 symmetric: see
`CoarsePreconditionerTests.CombinedPreconditioner_IsSymmetric`.

- `AggregateBlockSpan`: aggregate width/height in blocks, before the
  `MaxAggregates` cap may coarsen it. 4 is fine enough that the coarse
  correction resolves per-aggregate rigid motion on the ships where that
  matters (small ones, which never hit the cap at all).
- `AggregateCount`: number of aggregates in the most recent Rebuild
  (Kc is 3x this).
- `MaxAggregates`: aggregate count above which Rebuild coarsens the
  aggregation (doubling the span) rather than pay a cubic
  eigendecomposition. 64 was measured against 100 and against no cap at
  all: on a 2000-block ship the eigendecomposition went 3938 ms (155
  aggregates) -> 113 ms (100, span 8) -> 88 ms (64, span 8), and on a
  500-block ship 254 ms (69 aggregates, no coarsening under a 100 cap)
  -> 17 ms (31, span 8). The coarser space did not cost CG anything
  measurable on either ship; both got FASTER per tick as well, because
  `ApplyAdditive`'s dense coarse solve is O(m^2) every iteration.
- `MaxAggregateBlockSpan`: ceiling on the automatic coarsening, so a
  pathological ship cannot aggregate itself down to a handful of blocks
  and a coarse space too small to carry a useful correction.
- `LastAggregateBlockSpan`: span the most recent Rebuild actually used,
  which is `AggregateBlockSpan` unless the cap above coarsened it.
- `Rebuild`: (re)builds the aggregation, P's local mode coefficients,
  Kc, and Kc's pseudo-inverse from `k`'s current sparsity/values. Call
  whenever StiffnessAssembly.Rebuild ran.
- `ApplyAdditive`: adds the coarse correction `(I-Q) P Kc^+ P^T (I-Q) r`
  into `z` (in place, `z` already expected to hold the Jacobi term
  `D^-1 r`), where Q projects onto the ship's three global rigid-body
  modes.

### BucklingMode

<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/BucklingAnalysis.cs::BucklingMode -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/BucklingAnalysis.cs::BucklingMode.LoadFactor -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/BucklingAnalysis.cs::BucklingMode.Shape -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/BucklingAnalysis.cs::BucklingMode.BlockParticipation -->

One converged buckling mode: a load factor and the DOF shape it belongs
to, plus which blocks carry the strain energy of that shape.

- `LoadFactor`: smallest positive lambda such that `K + lambda*K_G` is
  singular along `Shape`. Less than 1 means the CURRENT load already
  exceeds the buckling load for this shape.
- `Shape`: the mode's DOF displacement vector, normalized so its
  largest-magnitude component is exactly 1 (a shape, not a physical
  displacement: the eigenproblem only fixes it up to scale).
- `BlockParticipation`: per-block fraction (0..1, summing to ~1 over the
  ship) of this mode's strain energy `phi^T K phi`. Answers WHICH blocks
  fold in this particular mode.

### BucklingAnalysis

<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/BucklingAnalysis.cs::BucklingAnalysis -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/BucklingAnalysis.cs::BucklingAnalysis.MaxSweepsPerTick -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/BucklingAnalysis.cs::BucklingAnalysis.Tolerance -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/BucklingAnalysis.cs::BucklingAnalysis.LastSweepCount -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/BucklingAnalysis.cs::BucklingAnalysis.LastConvergedTick -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/BucklingAnalysis.cs::BucklingAnalysis.LastCgIterationCount -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/BucklingAnalysis.cs::BucklingAnalysis.MinStrainEnergyFraction -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/BucklingAnalysis.cs::BucklingAnalysis.MinCompressionFraction -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/BucklingAnalysis.cs::BucklingAnalysis.ForceConvergeAfterSweeps -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/BucklingAnalysis.cs::BucklingAnalysis.MinSweepsBeforeConvergence -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/BucklingAnalysis.cs::BucklingAnalysis.StuckSweepsBeforeReseed -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/BucklingAnalysis.cs::BucklingAnalysis.Converged -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/BucklingAnalysis.cs::BucklingAnalysis.Reset -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/BucklingAnalysis.cs::BucklingAnalysis.Coarse -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/BucklingAnalysis.cs::BucklingAnalysis.MaxCgIterationsPerTick -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/BucklingAnalysis.cs::BucklingAnalysis.Step -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/Fem/BucklingAnalysis.cs::BucklingAnalysis.ExtractModes -->

Linearized buckling: the smallest positive load factors lambda solving
the generalized eigenproblem `(K + lambda*K_G) phi = 0`, i.e. `K phi =
-lambda*K_G phi`.

METHOD: block inverse (subspace) iteration, Bathe-style:
1. iterate `y_j = K^-1 * (-K_G * v_j)` for every vector in the block
   (CgSolver does the `K^-1` apply; K is singular by 3 rigid modes, so
   every vector and every CG right-hand side is projected onto their
   complement first, exactly like CgSolver already does for its own residual);
2. Gram-Schmidt orthonormalize the block;
3. Rayleigh-Ritz: project both K and `-K_G` onto the block (small `m x
   m` matrices) and solve THAT generalized eigenproblem exactly via
   Cholesky + Jacobi (both dense, ~40 lines, fine at `m <= ~8`);
4. replace the block with the Ritz vectors (sorted by ascending lambda)
   and repeat.

The block is `m = requested modes + 2` vectors: the two extras give the
iteration room to sort out near-degenerate modes without losing one of
the ones actually asked for.

WHY NOT PLAIN POWER ITERATION ON `K^-1*(-K_G)` DIRECTLY: under uniform
compression `-K_G` is positive-semidefinite, and repeated `K^-1`
application makes the block converge to the largest eigenvalues of that
positive operator, i.e. exactly the smallest positive lambda: no shift
needed. Mixed tension/compression can still put spurious large-magnitude
negative-lambda directions in the block; the non-positive ones are
filtered out in `ExtractModes` rather than chased by the iteration, which
is why the block carries two spares.

REAL-TIME / NO PER-TICK ALLOCATION: every work array here is sized once
by `Reset` (on topology change or mode-count change) and reused. `Step`
runs at most `MaxSweepsPerTick` sweeps and returns without publishing
anything until Ritz values stop moving; the caller (StructuralSolver)
keeps calling Step tick after tick and only reads modes out once it
returns true, so the subspace is warm-started for free across ticks (the
load changes smoothly, so after the first topology change only a sweep or
two is normally needed). Sweep budgets are TICK counts, never
wall-clock, and the only "randomness" is a fixed deterministic seed
pattern (no RNG), so two runs over identical inputs produce
bit-identical output: see `BucklingTests.Analysis_IsBitDeterministic`.

SERVER-ONLY DECISION, CLIENT-SAFE TINT: the float FE solve is not
guaranteed bit-identical across machines (different CPUs/JIT), so any
THRESHOLD decision made from it (which load factor crossed 1, which
blocks therefore break) must be made in exactly one place (the
authoritative server) and broadcast as an event, never re-derived
locally. That is what `StructuralSolver.BuckledBlocks` is: consume it
only on the authority. Clients may read `BlockStress.BucklingRatio` (a
continuous tint, not a decision) freely, because a client's own tint
disagreeing slightly with another machine's tint is invisible, while a
client independently deciding a block died is a desync.

STUCK-SUBSPACE DETECTION: under Mono, a divide by a near-zero Cholesky
pivot occasionally lands on a merely-huge-but-finite value rather than
the intended signed-infinity sentinel, which Jacobi's plane rotations
then mix into every slot; once collapsed this way the subspace is a
genuine fixed point (Gram-Schmidt only rescales a vector whose norm is
already informative). `_sawNonzeroMu` distinguishes "never found
coupling" from "had signal, then went silent" so only the latter, a
genuine collapse, triggers a reseed after `StuckSweepsBeforeReseed`
consecutive all-mu-zero sweeps.

PROJECT THE CG SOLUTION, NOT JUST ITS RIGHT-HAND SIDE: CgSolver
re-projects only its residual, so a rigid-body component that leaks into
the returned solution would compound across warm starts. This is what put
a rigid-dominated vector in the LOWEST slot of a 12-block column's
converged subspace under Mono (direction cosines 0.62/-0.68/-0.78 against
the three rigid modes), publishing a load factor 17x below the true one
(0.0086 against the dense oracle's 0.149) while the correct mode sat in slot 1.

PUBLISH-TIME RAYLEIGH QUOTIENTS: `ExtractModes` publishes `a / b` with
`a = phi^T K phi` and `b = -phi^T K_G phi`, evaluated with the SPARSE
operators against the published shape directly (in double), rather than
the reduced problem's `1/mu`. Two reasons: (1) it makes the number
independent of the reduced problem's rounding path (Mono and .NET keep
float intermediates at different widths; on a slender column those
diverged far enough that Mono published 0.0086 where .NET read 0.149);
(2) it gives a meaningful rejection test (`MinStrainEnergyFraction`,
`MinCompressionFraction`) that the reduced route cannot, since a Ritz
slot whose pivot went near-singular still yields a finite, positive,
stable-looking lambda despite carrying essentially no strain energy.

- `MaxSweepsPerTick`: sweeps to run per Step call: the per-tick cost cap.
- `Tolerance`: Ritz values are considered converged once every tracked
  lambda changes by less than this between sweeps (relative to its own
  magnitude).
- `LastSweepCount`: sweeps actually run by the most recent Step call.
- `LastConvergedTick`: tick index (as passed to Step, NOT a wall-clock
  time) at which the subspace last converged and modes were published.
- `LastCgIterationCount`: CG iterations the most recent Step spent in
  total, across every inverse-iteration solve: what
  `MaxCgIterationsPerTick` actually caps.
- `MinStrainEnergyFraction`: strain-energy floor for publishing a mode,
  relative to `max(diag K) * |phi|^2`. Applied ONLY at publish time,
  never during a sweep: pivots legitimately dip mid-sweep and zeroing
  those rows (a previous attempt) broke healthy convergence.
- `MinCompressionFraction`: compression floor for publishing a mode: `b
  = -phi^T K_G phi` must exceed this fraction of `a = phi^T K phi`.
- `ForceConvergeAfterSweeps`: hard cap, in cumulative sweeps since the
  last Reset, after which Step force-publishes whatever the block
  currently holds even if the per-slot tolerance check has not settled.
- `MinSweepsBeforeConvergence`: sweeps since Reset before Step will EVER
  report converged, so a freshly seeded block's transient
  near-linear-dependence cannot pass by coincidence.
- `StuckSweepsBeforeReseed`: consecutive post-signal all-mu-zero sweeps
  before Step gives up warm-starting from a collapsed subspace and
  reseeds it fresh instead. Deliberately patient, comfortably above the
  several sweeps a HEALTHY run can legitimately spend mid
  self-correction with every slot transiently silent.
- `Converged`: true once the last Step call converged and `ExtractModes`
  is safe to call against the current subspace.
- `Reset`: (re)allocates every work array for `dof` degrees of freedom
  and a block of `modeCount` + 2 vectors, and reseeds the subspace with
  a fixed deterministic pattern. Call whenever the topology (dof count)
  or the requested mode count changes.
- `Coarse`: the coarse correction to hand this analysis's own CG, or
  null for plain Jacobi. Attaching one was long avoided on the grounds
  that the inverse iteration's Rayleigh quotients amplify any
  preconditioner-dependent error in K^-1; that concern predates
  `CoarsePreconditioner.ApplyAdditive` projecting BOTH its input and its
  output, which made the correction exactly rigid-mode-free. With that
  in place every BucklingTests case passes unchanged, and the 100-block
  benchmark's buckling tick halved.
- `MaxCgIterationsPerTick`: total CG iterations Step may spend across
  ALL of its inverse-iteration solves in one call. A sweep runs one CG
  solve per block vector, each previously free to run to CgSolver's own
  4000-iteration cap, so "2 sweeps" could mean 16 full solves and
  several hundred milliseconds on a ship whose quasi-static solve costs
  20 (measured: 370-480 ms every 4th tick at 100 blocks). Budget
  exhausted means a block vector keeps its warm start for this tick,
  costing convergence SPEED, not correctness.
- `Step`: runs up to `MaxSweepsPerTick` subspace-iteration sweeps,
  warm-started from wherever the previous call left off. Returns true
  (and sets `Converged`) once the tracked Ritz values stop moving, at
  which point `tick` is recorded in `LastConvergedTick` and
  `ExtractModes` is safe to call.
- `ExtractModes`: reads the requested number of positive,
  sub-threshold-safe modes out of the converged subspace (call only
  when `Step` last returned true), ascending by load factor. Blocks are
  visited in ascending key order so the resulting `BlockParticipation`
  dictionaries are built deterministically.

### BlockStress

<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::BlockStress -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::BlockStress.VonMises -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::BlockStress.Major -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::BlockStress.Minor -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::BlockStress.DuctileRatio -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::BlockStress.BrittleRatio -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::BlockStress.Sxx -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::BlockStress.Syy -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::BlockStress.Txy -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::BlockStress.BucklingRatio -->

Per-block stress result for one tick: the S36/S37 API. `VonMises` and the
principal stresses are read from the QUASI-STATIC solve (ductile); the
ratios summarize both criteria for whatever wants to color a block or
decide it should break.

- `Sxx`/`Syy`/`Txy`: raw element-center stress tensor components
  (quasi-static case), plane-stress convention `sigma =
  [[Sxx,Txy],[Txy,Syy]]`. GeometricStiffness needs the tensor itself, not
  just the von Mises/principal reductions.
- `BucklingRatio`: buckling risk tint: `1 / CriticalLoadFactor` scaled by
  this block's strain-energy participation in the CRITICAL (lowest load
  factor) buckling mode; 0 when the block does not participate in that
  mode or no sub-critical mode exists. CLIENT-SAFE: unlike
  `StructuralSolver.BuckledBlocks`, this is a continuous float derived
  the same way on every machine's own solve and is meant for a color
  tint only: it must never be used to decide that a block breaks.

### StructuralSolver

<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.StructuralSolver -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.MaxCgIterationsPerTick -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.Converged -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.ResidualNorm -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.IterationsThisTick -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.ContinuedFromLastTick -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.TicksSinceRestart -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.DofCount -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.MarkTopologyChanged -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.UseCoarseCorrection -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.BlockStresses -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.LoadScale -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.MaterialStiffnessScale -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.Tick -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.BucklingEnabled -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.BucklingEveryNTicks -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.BucklingModeCount -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.BucklingMaxCgIterationsPerTick -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.BucklingMaxSweepsPerTick -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.BucklingParticipationThreshold -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.BucklingModes -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.CriticalLoadFactor -->
<!-- frob:describes Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.BuckledBlocks -->

Ties NodeLattice/Q8Element/StiffnessAssembly/LoadVector/CgSolver together
for one simulation tick, and reduces the resulting displacement field to
a per-block stress.

TOPOLOGY TRACKING: this class does NOT call `grid.ClearDirty()`; the ship
branch owns that flag's lifecycle (other systems, e.g.
Connectivity/Articulation, also read it). Instead it snapshots
`grid.TopologyDirty` at the start of Tick and remembers whether it has
already rebuilt for the current dirty streak, using its own
`_lastRebuiltCount` compared against `grid.Count` as a cheap proxy: if
the block count changed since the last rebuild, or the caller explicitly
asks via `MarkTopologyChanged`, this rebuilds. This avoids ever mutating
state owned by another module while still not re-assembling every tick.

PER-TICK CONVERGENCE BUDGET: `MaxCgIterationsPerTick` bounds how much CG
work one Tick call may spend on the quasi-static solve. A ship large
enough that CG cannot reach tolerance within the budget keeps its PARTIAL
displacement as next tick's warm start rather than blocking the frame or
discarding progress, so `Converged` can be false for several ticks in a
row while the solve slowly catches up as the warm start improves. Stress
and damage decisions (`BlockStresses`) always use whatever displacement
is available, so on an unconverged tick they LAG the true quasi-static
answer by however far the residual still is from tolerance; this is
deliberate (better a slightly stale stress field than a stalled frame)
and is why buckling (which depends on that same stress field for its
geometric stiffness) only runs when `Converged`.

The constructor's CG `Tolerance` is NOT loosened to 1e-3 despite
stress/damage decisions only needing that much accuracy: this branch
tried exactly that and found it silently breaks buckling, because
`RunBuckling`'s gate is `StructuralSolver.Converged`, which is this SAME
CgSolver instance's Tolerance. Loosening it to 1e-3 made CG stop (and
report Converged=true) well before the displacement field was accurate
enough for GeometricStiffness's Rayleigh quotients, regressing three
BucklingTests cases exactly the way CgSolver's own MaxIterations doc
warns an under-converged solve does. Loosening this safely needs a
SEPARATE, tighter tolerance gate for "safe to run buckling this tick"
decoupled from "safe to stop CG this tick"; until then this stays at
CgSolver's own 1e-5 default.

`LoadScale` converts GAMEPLAY force units into the solver's normalized
material units before the solve. BlockType deliberately normalizes
materials (hull yield = 1.0, E = 1.0) for conditioning, while thrust and
gravity are authored in whatever units make the ship fly nicely
(ThrustPerBlock 10, planet mu 900). Those two scales have no reason to
agree, and they did not: the demo ship's own thrusters put every block
far past yield on the first tick, so the ship disintegrated the instant
play mode started. The fix is one honest conversion factor, not a
disabled failure check: the whole load vector (applied forces AND the
inertia relief that balances them) is multiplied by this, so the solve
stays linear, stresses scale exactly with it, and the geometric stiffness
that buckling is built from scales consistently too. Calibrated so the
stock demo ship at full thrust sits around 0.3 of yield while a long
unsupported arm still fails.

`MaterialStiffnessScale` is the ratio of real Young's modulus to yield
stress that BlockType's normalized material table leaves out, applied to
the published buckling load factors. BlockType normalizes hull to `E =
1.0` AND `yield = 1.0`, i.e. a material that yields at unit strain. Real
structural metals yield nearer 0.1% strain: `E/yield` is several hundred.
That omission is harmless for STRESS, which for a given self-equilibrated
load is independent of E (strain scales as `1/E`, stress as E times
strain). It is NOT harmless for BUCKLING: the critical load factor is the
ratio of elastic to geometric stiffness, so it scales directly with E.
Left at 1, the demo's 9-block ship read as buckling at 13% of its own
thrust, and ShipStructure detached the blocks that "buckled": a rubber
ship folding up, not a metal one. Scaling the published load factors is
exactly equivalent to solving with E multiplied by this and leaving
everything else alone, and it keeps the default at 1 so every existing
BucklingTests case (all calibrated against `E = 1`) is untouched.

- `MaxCgIterationsPerTick`: per-tick CG iteration budget: the
  quasi-static solve is warm-started from whatever displacement the
  previous tick left, so a ship too large to fully converge within one
  frame keeps making progress across ticks instead of either blocking
  the frame or silently returning nonsense. 400 is a starting point, not
  a measured number.
- `Converged`: true when the most recent Tick's quasi-static solve met
  CgSolver's tolerance within `MaxCgIterationsPerTick`.
- `ResidualNorm`: `CgSolver.LastResidualNorm` from the most recent
  Tick's quasi-static solve.
- `IterationsThisTick`: `CgSolver.LastIterationCount` from the most
  recent Tick.
- `ContinuedFromLastTick`: true when this tick's quasi-static solve
  continued the previous tick's Krylov subspace (see CgState) rather
  than restarting it.
- `TicksSinceRestart`: ticks since the quasi-static solve last
  restarted its Krylov subspace; 0 on a restarting tick.
- `DofCount`: degrees of freedom in the current assembly (2 per node).
- `MarkTopologyChanged`: call when the caller knows topology changed but
  the block count happens to be unchanged (e.g. a block swapped for a
  different type at the same key): Count alone cannot detect that.
- `UseCoarseCorrection`: on by default: augments CgSolver's plain Jacobi
  with CoarsePreconditioner's deflated coarse correction, which is what
  lets iteration counts grow sublinearly with ship width instead of
  tracking it directly. Kept switchable so the plain-Jacobi path stays
  available for comparison/regression.
- `BlockStresses`: per-block stress results from the most recent Tick.
- `Tick`: runs one structural solve: rebuilds K if topology changed,
  applies the given point forces plus inertia relief, solves for
  displacement, and fills `BlockStresses`.
- `BucklingEnabled`: master switch; on by default. Off entirely skips
  the geometric-stiffness assembly and subspace iteration.
- `BucklingEveryNTicks`: buckling is attempted at most once every this
  many ticks (a tick-count throttle, not a wall-clock one). Between
  eligible ticks the previously published modes stand.
- `BucklingModeCount`: modes requested from the subspace iteration.
- `BucklingMaxCgIterationsPerTick`: forwards to
  `BucklingAnalysis.MaxCgIterationsPerTick`. Total CG iterations the
  buckling sweep may spend in one tick: the quasi-static solve has had a
  per-tick budget since `MaxCgIterationsPerTick` was introduced; the
  buckling path never did, which is the whole of the periodic spike the
  100-block benchmark showed (370-480 ms every fourth tick against 20 ms
  for a normal one, Release). 200 is measured, not guessed: it puts that
  tick at 29-39 ms, and the next step down (100, 9 ms) under-converges
  the inverse iteration badly enough to break
  `Column_CriticalLoadFactorScalesWithInverseLengthSquared` (the Euler
  `1/L^2` trend read 1.18 instead of 4).
- `BucklingMaxSweepsPerTick`: forwards to
  `BucklingAnalysis.MaxSweepsPerTick`.
- `BucklingParticipationThreshold`: cumulative strain-energy fraction
  (descending by block participation) that defines "the blocks that
  fold" in a sub-critical mode: e.g. 0.5 means the fewest
  highest-energy blocks whose participation sums to half the mode's energy.
- `BucklingModes`: most recent converged buckling modes, ascending by
  load factor. Empty when buckling is disabled, not yet converged since
  the last topology change, or the ship has no compression anywhere.
- `CriticalLoadFactor`: smallest load factor among `BucklingModes`, or
  `+infinity` when there is none.
- `BuckledBlocks`: union, across every mode with `LoadFactor <= 1`, of
  the blocks making up `BucklingParticipationThreshold` of that mode's
  strain energy: i.e. every block that some independent sub-critical
  fold wants to break, combined, because a ship can fold in two places
  at once and both must break. SERVER-AUTHORITATIVE, NOT CLIENT-SAFE:
  the float FE solve is not bit-identical across machines. Only the
  authoritative simulation may read this list to decide a block dies and
  then BROADCAST that as an event; a client independently reading this
  and detaching a block itself can disagree with the server and desync.
  Clients must use `BlockStress.BucklingRatio` (a tint, not a decision) instead.
