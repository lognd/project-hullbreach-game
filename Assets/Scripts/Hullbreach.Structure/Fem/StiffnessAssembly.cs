using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Hullbreach.Structure
{
    /// <summary>
    /// Scatters each element's 16x16 into the sparse global K.
    ///
    /// The accumulation at shared nodes IS the structural connection: two
    /// blocks are joined precisely because they write into the same rows.
    ///
    /// K comes out symmetric positive SEMI-definite. It is singular, by three,
    /// because a free-floating ship has three rigid-body modes -- see
    /// LoadVector for how that is handled rather than papered over.
    /// </summary>
    public sealed class StiffnessAssembly
    {
        /// <summary>Dense node id -> contiguous index, index*2 is the DOF
        /// offset. Rebuilt whenever <see cref="Rebuild"/> runs.</summary>
        public Dictionary<int, int> NodeMap { get; } = new Dictionary<int, int>();

        /// <summary>Rest position of each dense node, in ship-local units
        /// (lattice coordinate / 2). Needed by LoadVector to locate the
        /// element containing a point and by rigid-body mode construction.</summary>
        public float2[] NodeRestPositions { get; private set; } = Array.Empty<float2>();

        // CSR storage: rowPtr has DofCount+1 entries, colIndex/values are
        // parallel arrays of the nonzero entries per row.
        int[] _rowPtr = Array.Empty<int>();
        int[] _colIndex = Array.Empty<int>();
        float[] _values = Array.Empty<float>();

        /// <summary>Number of degrees of freedom (2 per node).</summary>
        public int DofCount { get; private set; }

        /// <summary>
        /// Build sparse K for the whole grid, from scratch. Rebuild only when
        /// topology is dirty (or after a stiffness-affecting damage change,
        /// since this recomputes everything rather than rescaling in place --
        /// simplicity over the incremental-rescale optimization the TODO
        /// mentions, since assembly at this scale is cheap).
        /// </summary>
        public void Rebuild(Hullbreach.Core.BlockGrid grid)
        {
            NodeLattice.BuildNodeMap(grid, NodeMap);
            int nodeCount = NodeMap.Count;
            DofCount = nodeCount * 2;

            NodeRestPositions = new float2[nodeCount];
            foreach (var kvp in NodeMap)
            {
                NodeLattice.UnpackNode(kvp.Key, out int dx, out int dy);
                NodeRestPositions[kvp.Value] = new float2(dx / 2f, dy / 2f);
            }

            // Triplet accumulation keyed by (row * DofCount + col). Simple and
            // correct; CSR is built from this once at the end.
            var entries = new Dictionary<long, float> ();
            var nodeIds = new int[NodeLattice.NodesPerElement];
            var dofs = new int[Q8Element.DofCount];

            foreach (var kvp in grid.All)
            {
                Hullbreach.Core.BlockKey.Unpack(kvp.Key, out int x, out int y);
                NodeLattice.NodesOf(x, y, nodeIds);

                for (int i = 0; i < NodeLattice.NodesPerElement; i++)
                {
                    int dense = NodeMap[nodeIds[i]];
                    dofs[2 * i] = 2 * dense;
                    dofs[2 * i + 1] = 2 * dense + 1;
                }

                float e = Hullbreach.Core.BlockTypes.EffectiveStiffness(kvp.Value);
                byte poissonClass = Hullbreach.Core.BlockTypes.Get(kvp.Value.TypeId).PoissonClass;
                var kHat = Q8Element.KHatFor(poissonClass, Hullbreach.Core.BlockType.Width);

                for (int i = 0; i < Q8Element.DofCount; i++)
                {
                    int row = dofs[i];
                    for (int j = 0; j < Q8Element.DofCount; j++)
                    {
                        int col = dofs[j];
                        float v = e * kHat[i, j];
                        if (v == 0f) continue;

                        long key = (long)row * DofCount + col;
                        entries.TryGetValue(key, out float prev);
                        entries[key] = prev + v;
                    }
                }
            }

            BuildCsr(entries);
        }

        /// <summary>Converts the accumulated triplets into CSR arrays.</summary>
        void BuildCsr(Dictionary<long, float> entries)
        {
            var perRow = new List<(int col, float val)>[DofCount];
            for (int i = 0; i < DofCount; i++) perRow[i] = new List<(int, float)>();

            foreach (var kv in entries)
            {
                int row = (int)(kv.Key / DofCount);
                int col = (int)(kv.Key % DofCount);
                perRow[row].Add((col, kv.Value));
            }

            _rowPtr = new int[DofCount + 1];
            int nnz = 0;
            for (int i = 0; i < DofCount; i++)
            {
                perRow[i].Sort((a, b) => a.col.CompareTo(b.col));
                _rowPtr[i] = nnz;
                nnz += perRow[i].Count;
            }
            _rowPtr[DofCount] = nnz;

            _colIndex = new int[nnz];
            _values = new float[nnz];
            int idx = 0;
            for (int i = 0; i < DofCount; i++)
            {
                foreach (var (col, val) in perRow[i])
                {
                    _colIndex[idx] = col;
                    _values[idx] = val;
                    idx++;
                }
            }
        }

        /// <summary>y = K * x. The only operation CG needs.</summary>
        public void Multiply(float[] x, float[] y)
        {
            for (int row = 0; row < DofCount; row++)
            {
                float sum = 0f;
                int start = _rowPtr[row];
                int end = _rowPtr[row + 1];
                for (int k = start; k < end; k++)
                    sum += _values[k] * x[_colIndex[k]];
                y[row] = sum;
            }
        }

        /// <summary>Writes the diagonal of K into `into`, for the Jacobi
        /// preconditioner.</summary>
        public void Diagonal(float[] into)
        {
            for (int row = 0; row < DofCount; row++)
            {
                float d = 0f;
                int start = _rowPtr[row];
                int end = _rowPtr[row + 1];
                for (int k = start; k < end; k++)
                {
                    if (_colIndex[k] == row) { d = _values[k]; break; }
                }
                into[row] = d;
            }
        }
    }
}
