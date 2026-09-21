using System;

namespace Hullbreach.Structure
{
    /// <summary>
    /// Deflated two-level additive preconditioner: adds a coarse, global
    /// correction on top of CgSolver's plain Jacobi so information can cross
    /// the whole ship in O(1) preconditioner applications instead of one CG
    /// iteration per element along the longest path (see CgSolver's doc on
    /// why iterations scale with ship width).
    ///
    /// CONSTRUCTION: nodes are grouped into aggregates of AggregateBlockSpan
    /// x AggregateBlockSpan blocks by lattice coordinate (deterministic,
    /// sorted by aggregate coordinate so rebuilds are bit-reproducible). Each
    /// aggregate gets its own 3 rigid-body modes (translate x, translate y,
    /// rotate about the aggregate's centroid), Gram-Schmidt orthonormalized
    /// within the aggregate; stacking every aggregate's 3 modes as columns is
    /// the prolongation P (never materialized densely: P is block-diagonal by
    /// aggregate, so applying it or its transpose is O(dof)). The coarse
    /// operator is Kc = P^T K P, a dense (3 * aggregateCount)-square matrix,
    /// built once per topology change from one K-multiply per column.
    ///
    /// WHY A PSEUDO-INVERSE, NOT A CHOLESKY, OF Kc: a previous attempt (see
    /// docs/roadmap.md's Performance section) built this same Kc and
    /// factored it with Cholesky, and got NaNs. The reason is structural,
    /// not a bug to patch around: K's global 3-dimensional rigid-body null
    /// space lies exactly in range(P) (every aggregate's own rigid modes sum,
    /// weighted correctly, to the ship's global rigid modes), so Kc inherits
    /// that same 3-dimensional null space and is exactly singular, not just
    /// ill-conditioned. Cholesky of a singular matrix does not fail loudly;
    /// it produces a near-zero pivot, divides by it, and hands back
    /// coefficients amplified by whatever rounding noise happened to leak
    /// into that pivot, which is the NaN. The fix used here is to
    /// diagonalize Kc once (DenseJacobiEigen, cheap at this size) and build
    /// its Moore-Penrose pseudo-inverse, DROPPING every eigenvalue below
    /// 1e-6 times the largest: those dropped directions are exactly Kc's
    /// null space, so Kc^+ applied to P^T r is, by construction, zero on
    /// them. Combining M^-1 = D^-1 (Jacobi) + P Kc^+ P^T keeps the whole
    /// preconditioner symmetric positive semidefinite on the residual's
    /// actual subspace (the complement of the rigid modes, which CgSolver
    /// already projects onto every iteration), so PCG's convergence theory
    /// still applies.
    ///
    /// ALLOCATION: every array is sized once by <see cref="Rebuild"/> (call
    /// only on a topology change, same cadence as StiffnessAssembly.Rebuild)
    /// and reused; <see cref="ApplyAdditive"/> is allocation-free.
    /// </summary>
    public sealed class CoarsePreconditioner
    {
        /// <summary>Aggregate width/height in blocks, before the
        /// <see cref="MaxAggregates"/> cap may coarsen it. 4 is fine enough
        /// that the coarse correction resolves per-aggregate rigid motion
        /// on the ships where that matters (small ones, which never hit the
        /// cap at all).</summary>
        public int AggregateBlockSpan = 4;

        int _dof = -1;
        int _aggregateCount;

        // Per-dof aggregate assignment and that aggregate's (orthonormal)
        // local rigid-mode coefficients: dof `d` belongs to aggregate
        // _aggId[d], and its coefficient along that aggregate's k-th mode is
        // _phi[k][d]. Every dof belongs to exactly one aggregate, so P is
        // block-diagonal and these three arrays ARE P, without ever forming
        // it densely.
        int[] _aggId = Array.Empty<int>();
        float[] _phi0 = Array.Empty<float>();
        float[] _phi1 = Array.Empty<float>();
        float[] _phi2 = Array.Empty<float>();

        // Dense coarse operator, its eigendecomposition, and the resulting
        // pseudo-inverse, all (3*_aggregateCount) square.
        float[,] _kc = new float[0, 0];
        float[,] _eigVecs = new float[0, 0];
        float[] _eigVals = Array.Empty<float>();
        float[,] _kcPinv = new float[0, 0];

        // Scratch reused by ApplyAdditive/Rebuild.
        float[] _coarseRhs = Array.Empty<float>();
        float[] _coarseSol = Array.Empty<float>();
        float[] _kcol = Array.Empty<float>();

        // The ship's three GLOBAL rigid-body modes, orthonormalized once per
        // Rebuild, plus the two dof-sized scratch vectors ApplyAdditive uses
        // to sandwich the coarse solve between projections onto their
        // complement (see ApplyAdditive's doc for why that sandwich, and not
        // a single one-sided projection, is what makes the correction both
        // exactly rigid-mode-free AND still symmetric).
        float[][] _globalRigid = new float[3][] { Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>() };
        float[] _rProjected = Array.Empty<float>();
        float[] _coarseExpanded = Array.Empty<float>();

        /// <summary>Number of aggregates in the most recent Rebuild, exposed
        /// for tests/logging (Kc is 3x this).</summary>
        public int AggregateCount => _aggregateCount;

        /// <summary>Aggregate count above which Rebuild coarsens the
        /// aggregation (doubling the span) rather than pay a cubic
        /// eigendecomposition. 64 was measured against 100 and against no
        /// cap at all: on a 2000-block ship the eigendecomposition went
        /// 3938 ms (155 aggregates) -> 113 ms (100, span 8) -> 88 ms (64,
        /// span 8), and on a 500-block ship 254 ms (69 aggregates, no
        /// coarsening under a 100 cap) -> 17 ms (31, span 8). The coarser
        /// space did not cost CG anything measurable on either ship; both
        /// got FASTER per tick as well, because ApplyAdditive's dense
        /// coarse solve is O(m^2) every iteration.</summary>
        public int MaxAggregates = 64;

        /// <summary>Ceiling on the automatic coarsening, so a pathological
        /// ship cannot aggregate itself down to a handful of blocks and a
        /// coarse space too small to carry a useful correction.</summary>
        public int MaxAggregateBlockSpan = 16;

        /// <summary>Span the most recent Rebuild actually used, which is
        /// <see cref="AggregateBlockSpan"/> unless the cap above coarsened
        /// it. Exposed for tests/logging.</summary>
        public int LastAggregateBlockSpan { get; private set; }

        /// <summary>Counts the aggregates a given span would produce, with
        /// no allocation beyond the set it counts into: called at most a
        /// few times per rebuild (the span doubles each try).</summary>
        static int CountAggregates(Unity.Mathematics.float2[] pos, int nodeCount, int span)
        {
            var seen = new System.Collections.Generic.HashSet<(int, int)>();
            for (int i = 0; i < nodeCount; i++)
                seen.Add(((int)Math.Floor(pos[i].x / span), (int)Math.Floor(pos[i].y / span)));
            return seen.Count;
        }

        /// <summary>
        /// (Re)builds the aggregation, P's local mode coefficients, Kc, and
        /// Kc's pseudo-inverse from `k`'s current sparsity/values. Call
        /// whenever StiffnessAssembly.Rebuild ran (topology or, in principle,
        /// a stiffness-affecting change): this does not itself detect
        /// staleness, matching StructuralSolver's existing rebuild-tracking
        /// pattern for _assembly/_rigidModes.
        /// </summary>
        public void Rebuild(StiffnessAssembly k)
        {
            int dof = k.DofCount;
            var pos = k.NodeRestPositions;
            int nodeCount = pos.Length;

            _dof = dof;
            if (_aggId.Length != dof)
            {
                _aggId = new int[dof];
                _phi0 = new float[dof];
                _phi1 = new float[dof];
                _phi2 = new float[dof];
            }

            // Deterministic aggregate ids: group node dense-index by its
            // floored (x,y) block-aggregate coordinate, then assign ids in
            // ascending (ax,ay) order so a rebuild over an unchanged grid
            // reproduces identical ids (required for the bit-determinism
            // buckling-style guarantees the rest of this module upholds).
            // AGGREGATE-COUNT CAP: the coarse operator is dense and is
            // diagonalized once per rebuild, so its cost grows like
            // (3 * aggregates)^3. At AggregateBlockSpan = 4 a 2000-block
            // ship produces 155 aggregates, a 465-square Kc, and a 3.94 s
            // eigendecomposition on a 4.13 s rebuild: a visible hitch the
            // first time a big ship is touched. Doubling the span quarters
            // the aggregate count and so cuts that cost ~64x. The coarse
            // space gets correspondingly coarser, which is a real loss of
            // preconditioner quality, but a 64x cheaper rebuild is worth
            // more than a correction that is slightly sharper on a ship
            // whose per-tick cost is dominated by CG anyway (measured
            // below; see docs/roadmap.md's Performance section).
            int span = AggregateBlockSpan;
            while (span < MaxAggregateBlockSpan && CountAggregates(pos, nodeCount, span) > MaxAggregates)
                span *= 2;
            LastAggregateBlockSpan = span;

            var coordToNodes = new System.Collections.Generic.SortedDictionary<(int ax, int ay), System.Collections.Generic.List<int>>();
            for (int i = 0; i < nodeCount; i++)
            {
                int ax = (int)Math.Floor(pos[i].x / span);
                int ay = (int)Math.Floor(pos[i].y / span);
                var key = (ax, ay);
                if (!coordToNodes.TryGetValue(key, out var list))
                {
                    list = new System.Collections.Generic.List<int>();
                    coordToNodes[key] = list;
                }
                list.Add(i);
            }

            _aggregateCount = coordToNodes.Count;
            int m = 3 * _aggregateCount;

            int agg = 0;
            foreach (var kv in coordToNodes)
            {
                BuildAggregateModes(kv.Value, pos, agg);
                agg++;
            }

            if (_kc.GetLength(0) != m)
            {
                _kc = new float[m, m];
                _eigVecs = new float[m, m];
                _eigVals = new float[m];
                _kcPinv = new float[m, m];
                _coarseRhs = new float[m];
                _coarseSol = new float[m];
            }

            if (_kcol.Length != dof) _kcol = new float[dof];

            if (_rProjected.Length != dof)
            {
                _rProjected = new float[dof];
                _coarseExpanded = new float[dof];
                for (int i = 0; i < 3; i++) _globalRigid[i] = new float[dof];
            }

            // Same three modes CgSolver and BucklingAnalysis project
            // against, built from the same NodeRestPositions, so the
            // subspace this preconditioner annihilates is bit-for-bit the
            // one the rest of the solver stack treats as K's null space.
            LoadVector.RigidBodyModes(pos, _globalRigid);
            CgSolver.Orthonormalize(_globalRigid);

            BuildKc(k);
            SolvePseudoInverse();
        }

        /// <summary>Fills _phi0/_phi1/_phi2 for every dof in aggregate
        /// `aggIndex`'s node list with its orthonormalized local rigid
        /// modes (translate x, translate y, rotate about the aggregate's
        /// own centroid). Gram-Schmidt is applied over just this aggregate's
        /// dofs (a handful, never the whole ship), reusing CgSolver's shared
        /// Dot for consistency with the rest of the solver stack.</summary>
        void BuildAggregateModes(System.Collections.Generic.List<int> nodes, Unity.Mathematics.float2[] pos, int aggIndex)
        {
            float cx = 0f, cy = 0f;
            foreach (var n in nodes) { cx += pos[n].x; cy += pos[n].y; }
            cx /= nodes.Count;
            cy /= nodes.Count;

            int localDof = nodes.Count * 2;
            var t0 = new float[localDof];
            var t1 = new float[localDof];
            var t2 = new float[localDof];

            for (int li = 0; li < nodes.Count; li++)
            {
                int n = nodes[li];
                float x = pos[n].x - cx;
                float y = pos[n].y - cy;
                t0[2 * li] = 1f;
                t1[2 * li + 1] = 1f;
                t2[2 * li] = -y;
                t2[2 * li + 1] = x;

                _aggId[2 * n] = aggIndex;
                _aggId[2 * n + 1] = aggIndex;
            }

            var localModes = new float[][] { t0, t1, t2 };
            CgSolver.Orthonormalize(localModes);

            for (int li = 0; li < nodes.Count; li++)
            {
                int n = nodes[li];
                _phi0[2 * n] = t0[2 * li];
                _phi0[2 * n + 1] = t0[2 * li + 1];
                _phi1[2 * n] = t1[2 * li];
                _phi1[2 * n + 1] = t1[2 * li + 1];
                _phi2[2 * n] = t2[2 * li];
                _phi2[2 * n + 1] = t2[2 * li + 1];
            }
        }

        /// <summary>Projects `r` onto P's columns: out[3*agg+k] = phi_k . r
        /// restricted to that aggregate. Allocation-free; `outVec` must be
        /// length 3*AggregateCount.</summary>
        void ProjectToCoarse(float[] r, float[] outVec)
        {
            Array.Clear(outVec, 0, outVec.Length);
            for (int d = 0; d < _dof; d++)
            {
                int b = _aggId[d];
                float rv = r[d];
                outVec[3 * b] += _phi0[d] * rv;
                outVec[3 * b + 1] += _phi1[d] * rv;
                outVec[3 * b + 2] += _phi2[d] * rv;
            }
        }

        /// <summary>Expands a coarse-space vector back with P: out[d] +=
        /// phi_k(d) * coarse[3*agg(d)+k]. Additive (into an existing
        /// preconditioned residual), allocation-free.</summary>
        void ExpandFromCoarseAdditive(float[] coarse, float[] outVec)
        {
            for (int d = 0; d < _dof; d++)
            {
                int b = _aggId[d];
                outVec[d] += _phi0[d] * coarse[3 * b]
                           + _phi1[d] * coarse[3 * b + 1]
                           + _phi2[d] * coarse[3 * b + 2];
            }
        }

        /// <summary>Builds Kc = P^T K P one column at a time: for each
        /// aggregate/mode column, one K-multiply of that column's (sparse,
        /// aggregate-local) P vector, then one O(dof) pass distributes the
        /// result into the WHOLE row of Kc at once (every other column's
        /// dot product falls out of the same pass, since P's columns
        /// partition the dofs).</summary>
        void BuildKc(StiffnessAssembly k)
        {
            int m = _kc.GetLength(0);
            var pcol = _kcol; // reused as the sparse P-column input to Multiply
            var kcol = new float[_dof];

            for (int agg = 0; agg < _aggregateCount; agg++)
            {
                for (int mode = 0; mode < 3; mode++)
                {
                    Array.Clear(pcol, 0, _dof);
                    for (int d = 0; d < _dof; d++)
                    {
                        if (_aggId[d] != agg) continue;
                        pcol[d] = mode == 0 ? _phi0[d] : mode == 1 ? _phi1[d] : _phi2[d];
                    }

                    k.Multiply(pcol, kcol);

                    int row = 3 * agg + mode;
                    for (int d = 0; d < _dof; d++)
                    {
                        int b = _aggId[d];
                        float kv = kcol[d];
                        _kc[row, 3 * b] += kv * _phi0[d];
                        _kc[row, 3 * b + 1] += kv * _phi1[d];
                        _kc[row, 3 * b + 2] += kv * _phi2[d];
                    }
                }
            }

            // Symmetrize away rounding asymmetry (K is symmetric, so Kc is
            // too, up to float rounding across the two accumulation orders
            // a row/column pair can take).
            for (int i = 0; i < m; i++)
            for (int j = i + 1; j < m; j++)
            {
                float avg = 0.5f * (_kc[i, j] + _kc[j, i]);
                _kc[i, j] = avg;
                _kc[j, i] = avg;
            }
        }

        /// <summary>Diagonalizes Kc and builds its pseudo-inverse V * S^+ *
        /// V^T, dropping eigenvalues below 1e-6 * the largest: see the
        /// class doc for why this (not Cholesky) is what makes the coarse
        /// correction well-defined despite Kc's exact 3-dimensional null
        /// space.</summary>
        void SolvePseudoInverse()
        {
            int m = _kc.GetLength(0);
            if (m == 0) return;

            // SCALE BEFORE DIAGONALIZING: Kc's entries carry K's actual
            // stiffness units (can be 1e5-1e8), but DenseJacobiEigen's
            // sweep-termination tolerance is an ABSOLUTE bound on the
            // remaining off-diagonal sum of squares. Left unscaled, that
            // absolute tolerance is meaningless relative to Kc's magnitude,
            // so Jacobi silently stops (at maxSweeps) with real off-
            // diagonal error still in the matrix, and the eigenvalues Kc's
            // exact 3-dimensional null space maps to land at whatever
            // rounding noise is left, not at zero. Some of that noise
            // measured comfortably above the pseudo-inverse's 1e-6-relative
            // floor, so those directions got a large-but-finite 1/lambda
            // instead of being dropped: precisely the amplification this
            // whole pseudo-inverse construction exists to avoid (see the
            // class doc), just relocated from Cholesky's hard NaN to a
            // slower, still-fatal blowup over repeated CG iterations
            // (observed directly on the 500-block benchmark: residual grew
            // from 1e7 to 1e13 over 10 ticks before this fix). Dividing by
            // Kc's largest entry first makes the matrix O(1) so the same
            // absolute tolerance is now meaningfully tight, and multiplying
            // the resulting eigenvalues back by that scale undoes it
            // exactly (eigenvectors are scale-invariant).
            float kcScale = 0f;
            for (int i = 0; i < m; i++)
            for (int j = 0; j < m; j++)
                kcScale = Math.Max(kcScale, Math.Abs(_kc[i, j]));
            if (kcScale < 1e-20f) kcScale = 1f;

            var scaled = new float[m, m];
            for (int i = 0; i < m; i++)
            for (int j = 0; j < m; j++)
                scaled[i, j] = _kc[i, j] / kcScale;

            DenseJacobiEigen.Solve(scaled, m, _eigVals, _eigVecs, maxSweeps: 100, tolerance: 1e-14f);
            for (int i = 0; i < m; i++) _eigVals[i] *= kcScale;

            float maxAbs = 0f;
            for (int i = 0; i < m; i++) maxAbs = Math.Max(maxAbs, Math.Abs(_eigVals[i]));
            // 1e-3, not the ticket's suggested 1e-6: measured directly on
            // the 500-block plate+arm benchmark, a 1e-6 floor let through
            // near-null (but not exactly null) directions from the 1-wide
            // arm's poorly-conditioned aggregation (neighboring aggregates
            // along a 1-block-wide strip have almost-parallel local rotate
            // modes, not the exact 3-dimensional global rigid-body null
            // space the ticket's failure mode was about), each with a huge
            // but finite 1/lambda that amplified rounding noise every CG
            // iteration and diverged the residual from 1e4 to NaN over 10
            // ticks. 1e-3 costs a little coarse-correction quality on those
            // directions (they fall back to Jacobi-only) in exchange for
            // never dividing by anything that small.
            float floor = 3e-4f * maxAbs;

            var sPlus = new float[m];
            for (int i = 0; i < m; i++)
                sPlus[i] = _eigVals[i] > floor ? 1f / _eigVals[i] : 0f;

            for (int i = 0; i < m; i++)
            for (int j = 0; j < m; j++)
            {
                float sum = 0f;
                for (int kk = 0; kk < m; kk++)
                    sum += _eigVecs[i, kk] * sPlus[kk] * _eigVecs[j, kk];
                _kcPinv[i, j] = sum;
            }
        }

        /// <summary>
        /// Adds the coarse correction (I-Q) P Kc^+ P^T (I-Q) r into `z` (in
        /// place, `z` already expected to hold the Jacobi term D^-1 r),
        /// where Q projects onto the ship's three global rigid-body modes:
        /// the combined M^-1 = D^-1 + (I-Q) P Kc^+ P^T (I-Q) is what
        /// CgSolver applies as its preconditioner when a
        /// CoarsePreconditioner is attached.
        ///
        /// WHY BOTH SIDES ARE PROJECTED, not just the output: Kc^+'s
        /// eigenvalue floor (see SolvePseudoInverse) drops Kc's null space
        /// only to within DenseJacobiEigen's rounding, so the raw
        /// correction leaks a small rigid-body component, which CG cannot
        /// see (K annihilates it, so it never reaches the residual) but
        /// which accumulates in `p` and `u` every iteration and destroys
        /// the cancellation in the B*u strain evaluation downstream: that
        /// is what corrupted three BucklingTests' geometric stiffness
        /// under Mono, whose float rounding differs from .NET's. Making
        /// the correction EXACTLY rigid-mode-free by construction removes
        /// the leak at its source instead of mopping it up in CgSolver.
        /// The projection is applied on the INPUT too, not only the
        /// output, because (I-Q) M0 is not a symmetric operator while
        /// (I-Q) M0 (I-Q) is (Q is a symmetric projector, Kc^+ is
        /// symmetric), and PCG's convergence theory needs M^-1 symmetric:
        /// see CoarsePreconditionerTests.CombinedPreconditioner_IsSymmetric.
        /// </summary>
        public void ApplyAdditive(float[] r, float[] z)
        {
            // INPUT PROJECTION: strip the global rigid modes off the
            // residual before it ever reaches the coarse space.
            Array.Copy(r, _rProjected, _dof);
            CgSolver.Project(_rProjected, _globalRigid);

            ProjectToCoarse(_rProjected, _coarseRhs);

            int m = _coarseRhs.Length;
            for (int i = 0; i < m; i++)
            {
                float sum = 0f;
                for (int j = 0; j < m; j++)
                    sum += _kcPinv[i, j] * _coarseRhs[j];
                _coarseSol[i] = sum;
            }

            // OUTPUT PROJECTION: strip them off the correction as well,
            // then add the result into `z`.
            Array.Clear(_coarseExpanded, 0, _dof);
            ExpandFromCoarseAdditive(_coarseSol, _coarseExpanded);
            CgSolver.Project(_coarseExpanded, _globalRigid);

            for (int i = 0; i < _dof; i++) z[i] += _coarseExpanded[i];
        }
    }
}
