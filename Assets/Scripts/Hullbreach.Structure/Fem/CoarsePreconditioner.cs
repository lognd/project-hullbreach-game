using System;

namespace Hullbreach.Structure
{
    // Deflated two-level additive preconditioner; see
    // docs/reference/hullbreach-structure.md#coarsepreconditioner for why.
    // frob:doc docs/reference/hullbreach-structure.md#coarsepreconditioner
    public sealed class CoarsePreconditioner
    {
        // 4 resolves per-aggregate rigid motion on ships small enough to
        // never hit MaxAggregates.
        // frob:doc docs/reference/hullbreach-structure.md#coarsepreconditioner
        public int AggregateBlockSpan = 4;

        int _dof = -1;
        int _aggregateCount;

        // Per-dof aggregate assignment and local rigid-mode coefficients:
        // these three arrays ARE P (never formed densely).
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

        // The ship's three GLOBAL rigid-body modes, plus scratch
        // ApplyAdditive uses to sandwich the coarse solve.
        float[][] _globalRigid = new float[3][] { Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>() };
        float[] _rProjected = Array.Empty<float>();
        float[] _coarseExpanded = Array.Empty<float>();

        // Kc is 3x this. Exposed for tests/logging.
        // frob:doc docs/reference/hullbreach-structure.md#coarsepreconditioner
        public int AggregateCount => _aggregateCount;

        // Above this, Rebuild coarsens the aggregation rather than pay a
        // cubic eigendecomposition; see the reference page for why 64.
        // frob:doc docs/reference/hullbreach-structure.md#coarsepreconditioner
        public int MaxAggregates = 64;

        // Ceiling on automatic coarsening, so a pathological ship cannot
        // aggregate down to a coarse space too small to help.
        // frob:doc docs/reference/hullbreach-structure.md#coarsepreconditioner
        public int MaxAggregateBlockSpan = 16;

        // Span the most recent Rebuild actually used (may exceed
        // AggregateBlockSpan if the cap coarsened it).
        // frob:doc docs/reference/hullbreach-structure.md#coarsepreconditioner
        public int LastAggregateBlockSpan { get; private set; }

        // Counts the aggregates a given span would produce; called a few
        // times per rebuild while probing for a span under MaxAggregates.
        static int CountAggregates(Unity.Mathematics.float2[] pos, int nodeCount, int span)
        {
            var seen = new System.Collections.Generic.HashSet<(int, int)>();
            for (int i = 0; i < nodeCount; i++)
                seen.Add(((int)Math.Floor(pos[i].x / span), (int)Math.Floor(pos[i].y / span)));
            return seen.Count;
        }

        // (Re)builds the aggregation, P's local mode coefficients, Kc, and
        // Kc's pseudo-inverse. Call whenever StiffnessAssembly.Rebuild ran.
        // frob:doc docs/reference/hullbreach-structure.md#coarsepreconditioner
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

            // Deterministic aggregate ids: group by floored (x,y)
            // block-aggregate coordinate; see the reference page for the cap.
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
            // against, so this preconditioner annihilates the same subspace.
            LoadVector.RigidBodyModes(pos, _globalRigid);
            CgSolver.Orthonormalize(_globalRigid);

            BuildKc(k);
            SolvePseudoInverse();
        }

        // Fills _phi0/_phi1/_phi2 for aggregate `aggIndex`'s dofs with its
        // orthonormalized local rigid modes about the aggregate's centroid.
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

        // Projects `r` onto P's columns: outVec[3*agg+k] = phi_k . r
        // restricted to that aggregate.
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

        // Expands a coarse-space vector back with P, additively into outVec.
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

        // Builds Kc = P^T K P one column at a time: one K-multiply per
        // aggregate/mode column distributes a whole row of Kc.
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
            // too, up to float rounding across accumulation orders).
            for (int i = 0; i < m; i++)
            for (int j = i + 1; j < m; j++)
            {
                float avg = 0.5f * (_kc[i, j] + _kc[j, i]);
                _kc[i, j] = avg;
                _kc[j, i] = avg;
            }
        }

        // Diagonalizes Kc and builds its pseudo-inverse V * S^+ * V^T; see
        // the reference page for why (not Cholesky).
        void SolvePseudoInverse()
        {
            int m = _kc.GetLength(0);
            if (m == 0) return;

            // Scale before diagonalizing: Jacobi's tolerance is an
            // ABSOLUTE bound; see the reference page for why this matters.
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
            // 3e-4, not a naive 1e-6: see the reference page for the
            // near-null directions a looser floor let through.
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

        // Adds the coarse correction (I-Q) P Kc^+ P^T (I-Q) r into `z`;
        // see the reference page for why both sides must be projected.
        // frob:doc docs/reference/hullbreach-structure.md#coarsepreconditioner
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
