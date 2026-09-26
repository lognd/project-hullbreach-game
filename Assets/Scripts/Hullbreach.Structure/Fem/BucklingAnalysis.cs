using System;
using System.Collections.Generic;
using System.Linq;

namespace Hullbreach.Structure
{
    // One converged buckling mode: a load factor, its DOF shape, and which
    // blocks carry the strain energy of that shape.
    // frob:doc docs/reference/hullbreach-structure.md#bucklingmode
    public sealed class BucklingMode
    {
        // Smallest positive lambda such that K + lambda*K_G is singular
        // along Shape; less than 1 means the CURRENT load already exceeds it.
        // frob:doc docs/reference/hullbreach-structure.md#bucklingmode
        public float LoadFactor;

        // Normalized so its largest-magnitude component is exactly 1 (a
        // shape, not a physical displacement).
        // frob:doc docs/reference/hullbreach-structure.md#bucklingmode
        public float[] Shape;

        // Per-block fraction (0..1, summing to ~1) of this mode's strain
        // energy: which blocks fold in this mode.
        // frob:doc docs/reference/hullbreach-structure.md#bucklingmode
        public Dictionary<int, float> BlockParticipation;
    }

    // Linearized buckling via block inverse (subspace) iteration; see
    // docs/reference/hullbreach-structure.md#bucklinganalysis for the method.
    // frob:doc docs/reference/hullbreach-structure.md#bucklinganalysis
    public sealed class BucklingAnalysis
    {
        // frob:doc docs/reference/hullbreach-structure.md#bucklinganalysis
        public int MaxSweepsPerTick = 2;

        // Ritz values are converged once every tracked lambda changes by
        // less than this between sweeps (relative to its own magnitude).
        // frob:doc docs/reference/hullbreach-structure.md#bucklinganalysis
        public float Tolerance = 2e-3f;

        // frob:doc docs/reference/hullbreach-structure.md#bucklinganalysis
        public int LastSweepCount { get; private set; }

        // Tick index (as passed to Step, not wall-clock) at which modes
        // were last published.
        // frob:doc docs/reference/hullbreach-structure.md#bucklinganalysis
        public int LastConvergedTick { get; private set; } = -1;

        // Deliberately its own CgSolver, never handed a
        // CoarsePreconditioner: see the reference page for why.
        readonly CgSolver _cg = new CgSolver();

        static readonly int DefaultCgIterations = new CgSolver().MaxIterations;

        // Across every inverse-iteration solve; what MaxCgIterationsPerTick caps.
        // frob:doc docs/reference/hullbreach-structure.md#bucklinganalysis
        public int LastCgIterationCount { get; private set; }

        // Strain-energy floor for publishing a mode; see the reference
        // page for why this is applied only at publish time, never mid-sweep.
        // frob:doc docs/reference/hullbreach-structure.md#bucklinganalysis
        public float MinStrainEnergyFraction = 1e-7f;

        // Compression floor for publishing a mode (load factor a/b must
        // stay below 1/this).
        // frob:doc docs/reference/hullbreach-structure.md#bucklinganalysis
        public float MinCompressionFraction = 1e-9f;

        int _dof = -1;
        int _m;
        int _modeCount;
        int _sweepsSinceReset;

        // Hard cap on cumulative sweeps since Reset before Step
        // force-publishes; mirrors CgSolver's MaxIterations behavior.
        // frob:doc docs/reference/hullbreach-structure.md#bucklinganalysis
        public int ForceConvergeAfterSweeps = 250;

        // Sweeps since Reset before Step will EVER report converged, so a
        // freshly seeded block cannot pass by coincidence.
        // frob:doc docs/reference/hullbreach-structure.md#bucklinganalysis
        public int MinSweepsBeforeConvergence = 30;

        // True once some sweep since Reset saw a nonzero mu; distinguishes
        // "never found coupling" from "had signal, then went silent".
        bool _sawNonzeroMu;

        // Consecutive sweeps every mu read exactly 0 since _sawNonzeroMu
        // went true; see the reference page for the collapse this detects.
        int _stuckSweeps;

        // Consecutive post-signal all-mu-zero sweeps before Step reseeds a
        // collapsed subspace instead of continuing to warm-start it.
        // frob:doc docs/reference/hullbreach-structure.md#bucklinganalysis
        public int StuckSweepsBeforeReseed = 100;

        float[][] _v;      // current subspace block
        float[][] _rhs;    // -K_G * v_j, also the CG right-hand side
        float[][] _y;      // CG solutions / next Ritz vectors
        float[][] _kv;     // K * y_j (Rayleigh-Ritz)
        float[][] _kgv;    // -K_G * y_j (Rayleigh-Ritz)

        float[,] _kr, _gr;   // projected m x m matrices
        float[,] _l;         // Cholesky factor of _kr
        float[,] _mid;       // L^-1 * Gr * L^-T
        float[,] _eigVecs;   // Jacobi eigenvectors of _mid
        float[] _mu;         // Jacobi eigenvalues of _mid
        float[] _lambda;     // 1/mu, this sweep
        float[] _lambdaPrev; // 1/mu, previous sweep
        int[] _order;        // ascending-lambda permutation

        // Last snapshot from a sweep that satisfied the ORDINARY tolerance
        // check; ExtractModes reads only this, never the raw working state.
        float[] _rqKPhi = Array.Empty<float>();
        float[] _rqKgPhi = Array.Empty<float>();
        float[] _rqDiag = Array.Empty<float>();

        // Orthonormalized copy of the rigid-body modes handed to Reset, so
        // ExtractModes can clean a published shape without a re-pass.
        float[][] _rigid = Array.Empty<float[]>();

        float[][] _publishedV;
        float[] _publishedLambda;

        // True once a sweep has satisfied the ordinary tolerance check and
        // _publishedV/_publishedLambda hold a trustworthy snapshot.
        bool _hasPublishedSnapshot;

        // frob:doc docs/reference/hullbreach-structure.md#bucklinganalysis
        public bool Converged { get; private set; }

        // (Re)allocates every work array and reseeds with a fixed
        // deterministic pattern. Call on topology/mode-count change.
        // frob:doc docs/reference/hullbreach-structure.md#bucklinganalysis
        public void Reset(int dof, int modeCount, float[][] rigidModes)
        {
            int m = modeCount + 2;
            _modeCount = modeCount;
            if (_dof != dof || _m != m)
            {
                _dof = dof;
                _m = m;
                _v = Alloc(m, dof);
                _rhs = Alloc(m, dof);
                _y = Alloc(m, dof);
                _kv = Alloc(m, dof);
                _kgv = Alloc(m, dof);
                _kr = new float[m, m];
                _gr = new float[m, m];
                _l = new float[m, m];
                _mid = new float[m, m];
                _eigVecs = new float[m, m];
                _mu = new float[m];
                _lambda = new float[m];
                _lambdaPrev = new float[m];
                _order = new int[m];
                _publishedV = Alloc(m, dof);
                _publishedLambda = new float[m];
                _rqKPhi = new float[dof];
                _rqKgPhi = new float[dof];
                _rqDiag = new float[dof];
                _rigid = Alloc(rigidModes.Length, dof);
            }

            for (int i = 0; i < rigidModes.Length; i++) Array.Copy(rigidModes[i], _rigid[i], dof);
            CgSolver.Orthonormalize(_rigid);

            SeedBlock(rigidModes, 0);

            // NaN, not +Infinity: a converged slot can legitimately report
            // lambda = +Infinity, and NaN never equals itself so "unset" stays distinct.
            for (int i = 0; i < _m; i++) _lambdaPrev[i] = float.NaN;
            _sweepsSinceReset = 0;
            _stuckSweeps = 0;
            _sawNonzeroMu = false;
            _hasPublishedSnapshot = false;
            Converged = false;
            LastSweepCount = 0;
            LastConvergedTick = -1;
        }

        // Fills the subspace block with a fixed deterministic seed pattern
        // (never RNG), salted so a mid-run reseed differs from the stuck state.
        void SeedBlock(float[][] rigidModes, int salt)
        {
            for (int k = 0; k < _m; k++)
            {
                var vk = _v[k];
                float freq = 1.3f + 0.7f * k + 0.11f * salt;
                for (int i = 0; i < _dof; i++)
                    vk[i] = (float)Math.Sin(freq * (i + 1) * 0.6180339887f + k + salt);
            }

            foreach (var vk in _v)
                CgSolver.Project(vk, rigidModes);
            CgSolver.Orthonormalize(_v);
        }

        static float[][] Alloc(int m, int dof)
        {
            var a = new float[m][];
            for (int i = 0; i < m; i++) a[i] = new float[dof];
            return a;
        }

        // The coarse correction to hand this analysis's own CG, or null
        // for plain Jacobi; see the reference page.
        // frob:doc docs/reference/hullbreach-structure.md#bucklinganalysis
        public CoarsePreconditioner Coarse;

        // Total CG iterations Step may spend across all inverse-iteration
        // solves in one call; see the reference page.
        // frob:doc docs/reference/hullbreach-structure.md#bucklinganalysis
        public int MaxCgIterationsPerTick = int.MaxValue;

        // Runs up to MaxSweepsPerTick sweeps, warm-started from the
        // previous call. Returns true once Ritz values stop moving.
        // frob:doc docs/reference/hullbreach-structure.md#bucklinganalysis
        public bool Step(StiffnessAssembly k, GeometricStiffness kg, float[][] rigidModes, int tick)
        {
            int sweeps = 0;
            bool converged = false;
            int cgBudget = MaxCgIterationsPerTick;
            LastCgIterationCount = 0;

            for (int iter = 0; iter < MaxSweepsPerTick; iter++)
            {
                sweeps++;
                _sweepsSinceReset++;
                for (int j = 0; j < _m; j++)
                {
                    kg.Multiply(_v[j], _rhs[j]);
                    for (int i = 0; i < _dof; i++) _rhs[j][i] = -_rhs[j][i];
                    CgSolver.Project(_rhs[j], rigidModes);

                    // Warm-start CG from the current subspace vector: the
                    // whole point of carrying _v across sweeps/ticks.
                    Array.Copy(_v[j], _y[j], _dof);
                    if (cgBudget > 0)
                    {
                        _cg.MaxIterations = cgBudget == int.MaxValue ? DefaultCgIterations : cgBudget;
                        _cg.Solve(k, _rhs[j], _y[j], rigidModes, Coarse);
                        if (cgBudget != int.MaxValue) cgBudget -= _cg.LastIterationCount;
                        LastCgIterationCount += _cg.LastIterationCount;
                    }

                    // PROJECT THE CG SOLUTION, not just its right-hand
                    // side: see the reference page for the Mono failure this prevents.
                    CgSolver.Project(_y[j], _rigid);
                }

                CgSolver.Orthonormalize(_y);

                for (int j = 0; j < _m; j++)
                {
                    k.Multiply(_y[j], _kv[j]);
                    kg.Multiply(_y[j], _kgv[j]);
                    for (int i = 0; i < _dof; i++) _kgv[j][i] = -_kgv[j][i];
                }

                for (int a = 0; a < _m; a++)
                for (int b = 0; b < _m; b++)
                {
                    _kr[a, b] = CgSolver.Dot(_y[a], _kv[b]);
                    _gr[a, b] = CgSolver.Dot(_y[a], _kgv[b]);
                }
                // Symmetrize away rounding asymmetry before Cholesky/Jacobi,
                // which both assume exact symmetry.
                Symmetrize(_kr, _m);
                Symmetrize(_gr, _m);

                SolveGeneralizedEigen();

                // STUCK-SUBSPACE DETECTION AND RESEED: see the reference
                // page for why this is a genuine fixed point to reseed past.
                bool allZero = true;
                for (int i = 0; i < _m; i++)
                {
                    if (_mu[i] != 0f) { allZero = false; break; }
                }
                if (!allZero) _sawNonzeroMu = true;

                _stuckSweeps = (allZero && _sawNonzeroMu) ? _stuckSweeps + 1 : 0;
                if (_stuckSweeps >= StuckSweepsBeforeReseed)
                {
                    // Salted by _sweepsSinceReset so the fresh seed differs
                    // from whatever seed the stuck state originated from.
                    SeedBlock(rigidModes, _sweepsSinceReset);
                    for (int i = 0; i < _m; i++) _lambdaPrev[i] = float.NaN;
                    _stuckSweeps = 0;
                    _sawNonzeroMu = false;
                    converged = false;
                    continue;
                }

                for (int a = 0; a < _m; a++) _order[a] = a;
                Array.Sort(_order, (p, q) => _lambda[p].CompareTo(_lambda[q]));

                // New subspace vectors are Ritz combinations of _y, via
                // _rhs as scratch so we never overwrite a _v slot in use.
                for (int outIdx = 0; outIdx < _m; outIdx++)
                {
                    int src = _order[outIdx];
                    var dst = _rhs[outIdx];
                    Array.Clear(dst, 0, _dof);
                    for (int j = 0; j < _m; j++)
                    {
                        float c = _eigVecs[j, src];
                        if (c == 0f) continue;
                        var yj = _y[j];
                        for (int i = 0; i < _dof; i++) dst[i] += c * yj[i];
                    }
                }
                for (int outIdx = 0; outIdx < _m; outIdx++)
                    Array.Copy(_rhs[outIdx], _v[outIdx], _dof);

                var sortedLambda = new float[_m];
                for (int outIdx = 0; outIdx < _m; outIdx++)
                    sortedLambda[outIdx] = _lambda[_order[outIdx]];

                // _lambda itself must move to the same order as _v:
                // ExtractModes reads _lambda[idx]/_v[idx] as one mode.
                Array.Copy(sortedLambda, _lambda, _m);

                // Only the requested modeCount smallest-lambda slots need
                // to settle; the 2 spares can wander indefinitely.
                converged = true;
                int tracked = Math.Min(_modeCount, _m);
                for (int i = 0; i < tracked; i++)
                {
                    float prev = _lambdaPrev[i];
                    float cur = sortedLambda[i];
                    if (float.IsNaN(prev))
                    {
                        // Sweep 0 for this slot: no history to compare yet.
                        converged = false;
                    }
                    else if (float.IsInfinity(prev) || float.IsInfinity(cur))
                    {
                        if (prev != cur) converged = false;
                    }
                    else
                    {
                        float scale = Math.Max(1e-6f, Math.Abs(prev));
                        if (Math.Abs(cur - prev) > Tolerance * scale) converged = false;
                    }
                }
                Array.Copy(sortedLambda, _lambdaPrev, _m);

                // Do NOT let a post-signal all-mu-zero sweep satisfy
                // convergence via the branch above; see the reference page.
                if (allZero && _sawNonzeroMu) converged = false;

                if (_sweepsSinceReset < MinSweepsBeforeConvergence) converged = false;

                // PUBLISHED SNAPSHOT: only a sweep that satisfies the
                // ordinary tolerance check is trustworthy enough to read.
                if (converged)
                {
                    for (int i = 0; i < _m; i++) Array.Copy(_v[i], _publishedV[i], _dof);
                    Array.Copy(_lambda, _publishedLambda, _m);
                    _hasPublishedSnapshot = true;
                }
                else if (_sweepsSinceReset >= ForceConvergeAfterSweeps)
                {
                    // Force-publish only the last stable snapshot; if none
                    // exists yet, keep iterating instead.
                    converged = _hasPublishedSnapshot;
                }

                if (converged) break;
            }

            LastSweepCount = sweeps;
            Converged = converged;
            if (converged) LastConvergedTick = tick;
            return converged;
        }

        static void Symmetrize(float[,] a, int n)
        {
            for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                float avg = 0.5f * (a[i, j] + a[j, i]);
                a[i, j] = avg;
                a[j, i] = avg;
            }
        }

        // Solves the small projected generalized eigenproblem via
        // Cholesky + cyclic Jacobi; see the reference page for the derivation.
        void SolveGeneralizedEigen()
        {
            Cholesky(_kr, _m, _l);

            // temp = L^-1 * _gr (solve L * temp = _gr, column by column).
            var temp = _mid; // reuse as scratch before it holds the final M
            for (int col = 0; col < _m; col++)
            {
                for (int i = 0; i < _m; i++)
                {
                    float sum = _gr[i, col];
                    for (int k = 0; k < i; k++) sum -= _l[i, k] * temp[k, col];
                    temp[i, col] = sum / _l[i, i];
                }
            }

            // M = temp * L^-T, i.e. solve L * M^T = temp^T row by row of M.
            var m2 = new float[_m, _m];
            for (int row = 0; row < _m; row++)
            {
                for (int i = 0; i < _m; i++)
                {
                    float sum = temp[row, i];
                    for (int k = 0; k < i; k++) sum -= _l[i, k] * m2[row, k];
                    m2[row, i] = sum / _l[i, i];
                }
            }
            Symmetrize(m2, _m);
            Array.Copy(m2, _mid, m2.Length);

            // Spare slots under weak stress can drive a near-zero pivot
            // to +-Infinity; sanitize rather than chase it.
            for (int i = 0; i < _m; i++)
            for (int j = 0; j < _m; j++)
                if (!float.IsFinite(_mid[i, j])) _mid[i, j] = 0f;

            DenseJacobiEigen.Solve(_mid, _m, _mu, _eigVecs);

            // c_i = L^-T * w_i (back-substitute L^T c = w for each column).
            var coeffs = new float[_m, _m];
            for (int col = 0; col < _m; col++)
            {
                for (int i = _m - 1; i >= 0; i--)
                {
                    float sum = _eigVecs[i, col];
                    for (int k = i + 1; k < _m; k++) sum -= _l[k, i] * coeffs[k, col];
                    coeffs[i, col] = sum / _l[i, i];
                }
            }
            Array.Copy(coeffs, _eigVecs, coeffs.Length);

            // mu are Ritz values of A = K^-1*(-K_G); lambda = 1/mu.
            // Near-zero/negative mu is represented as a signed infinity.
            for (int i = 0; i < _m; i++)
            {
                float mu = _mu[i];
                _lambda[i] = Math.Abs(mu) > 1e-9f
                    ? 1f / mu
                    : (mu >= 0f ? float.PositiveInfinity : float.NegativeInfinity);
            }
        }

        // Lower-triangular Cholesky, a = l*l^T; `a` is assumed SPD (an
        // indefinite _kr would be a programmer bug).
        static void Cholesky(float[,] a, int n, float[,] l)
        {
            for (int i = 0; i < n; i++)
            for (int j = 0; j <= i; j++)
            {
                float sum = a[i, j];
                for (int k = 0; k < j; k++) sum -= l[i, k] * l[j, k];
                if (i == j)
                    l[i, j] = (float)Math.Sqrt(Math.Max(sum, 1e-12f));
                else
                    l[i, j] = sum / l[j, j];
            }
            for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                l[i, j] = 0f;
        }

        // Reads the requested positive, sub-threshold-safe modes out of
        // the converged subspace, ascending by load factor.
        // frob:doc docs/reference/hullbreach-structure.md#bucklinganalysis
        public List<BucklingMode> ExtractModes(Hullbreach.Core.BlockGrid grid, StiffnessAssembly assembly, GeometricStiffness kg, int modeCount)
        {
            var result = new List<BucklingMode>();

            // Scale for the strain-energy floor below: the largest diagonal
            // entry of K, i.e. the stiffest single dof in the ship.
            assembly.Diagonal(_rqDiag);
            double maxDiag = 0.0;
            for (int i = 0; i < _dof; i++) maxDiag = Math.Max(maxDiag, Math.Abs(_rqDiag[i]));

            var keys = grid.All.Select(kvp => kvp.Key).ToList();
            keys.Sort();

            float h = Hullbreach.Core.BlockType.Width;
            var nodeIds = new int[NodeLattice.NodesPerElement];
            var dofs = new int[Q8Element.DofCount];
            var ue = new float[Q8Element.DofCount];

            for (int idx = 0; idx < _m && result.Count < modeCount; idx++)
            {
                // Skip a slot the reduced eigenproblem already called
                // meaningless; the published load factor comes from below.
                float reducedLambda = _publishedLambda[idx];
                if (!(reducedLambda > 1e-6f) || float.IsInfinity(reducedLambda)) continue;

                var shape = (float[])_publishedV[idx].Clone();
                // Belt and braces with the sweep-time projection: clean the
                // shape here too rather than trust every upstream path.
                CgSolver.Project(shape, _rigid);
                float maxAbs = 0f;
                for (int i = 0; i < _dof; i++) maxAbs = Math.Max(maxAbs, Math.Abs(shape[i]));
                if (maxAbs > 1e-12f)
                    for (int i = 0; i < _dof; i++) shape[i] /= maxAbs;

                // PUBLISH-TIME RAYLEIGH QUOTIENTS: a = phi^T K phi, b =
                // -phi^T K_G phi, in double; see the reference page for why.
                assembly.Multiply(shape, _rqKPhi);
                kg.Multiply(shape, _rqKgPhi);

                double a = 0.0, b = 0.0, phiSq = 0.0;
                for (int i = 0; i < _dof; i++)
                {
                    double si = shape[i];
                    a += si * _rqKPhi[i];
                    b -= si * _rqKgPhi[i];
                    phiSq += si * si;
                }

                // No strain energy along this shape relative to K's own
                // scale: a near-singular reduced pivot, not a mode.
                if (a < MinStrainEnergyFraction * maxDiag * phiSq) continue;
                // No meaningful compression along it: nothing to buckle.
                if (b <= MinCompressionFraction * a) continue;

                float lambda = (float)(a / b);
                if (!(lambda > 1e-6f) || float.IsInfinity(lambda)) continue;

                var participation = new Dictionary<int, float>();
                var energies = new List<(int key, float energy)>();
                float total = 0f;

                foreach (var key in keys)
                {
                    Hullbreach.Core.BlockKey.Unpack(key, out int x, out int y);
                    NodeLattice.NodesOf(x, y, nodeIds);
                    for (int i = 0; i < NodeLattice.NodesPerElement; i++)
                    {
                        int dense = assembly.NodeMap[nodeIds[i]];
                        dofs[2 * i] = 2 * dense;
                        dofs[2 * i + 1] = 2 * dense + 1;
                    }
                    for (int i = 0; i < Q8Element.DofCount; i++) ue[i] = shape[dofs[i]];

                    grid.TryGet(key, out var block);
                    float e = Hullbreach.Core.BlockTypes.EffectiveStiffness(block);
                    byte poissonClass = Hullbreach.Core.BlockTypes.Get(block.TypeId).PoissonClass;
                    var kHat = Q8Element.KHatFor(poissonClass, h);

                    float energy = 0f;
                    for (int i = 0; i < Q8Element.DofCount; i++)
                    {
                        float rowSum = 0f;
                        for (int j = 0; j < Q8Element.DofCount; j++)
                            rowSum += kHat[i, j] * ue[j];
                        energy += ue[i] * rowSum;
                    }
                    energy *= e;
                    energy = Math.Max(0f, energy);

                    energies.Add((key, energy));
                    total += energy;
                }

                if (total > 1e-12f)
                {
                    foreach (var (key, energy) in energies)
                    {
                        float frac = energy / total;
                        if (frac > 1e-4f) participation[key] = frac;
                    }
                }

                result.Add(new BucklingMode
                {
                    LoadFactor = lambda,
                    Shape = shape,
                    BlockParticipation = participation,
                });
            }

            // Slots come sorted by the REDUCED lambda, but the published
            // Rayleigh quotients can reorder near-degenerate slots; re-sort.
            for (int i = 1; i < result.Count; i++)
            {
                var item = result[i];
                int j = i - 1;
                while (j >= 0 && result[j].LoadFactor > item.LoadFactor)
                {
                    result[j + 1] = result[j];
                    j--;
                }
                result[j + 1] = item;
            }

            return result;
        }
    }
}
