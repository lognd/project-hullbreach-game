using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Hullbreach.Structure
{
    // Scatters each element's 16x16 into the sparse global K; see
    // docs/reference/hullbreach-structure.md#stiffnessassembly.
    // frob:doc docs/reference/hullbreach-structure.md#stiffnessassembly
    public sealed class StiffnessAssembly
    {
        // Rebuilt whenever Rebuild runs. index*2 is the DOF offset.
        // frob:doc docs/reference/hullbreach-structure.md#stiffnessassembly
        public Dictionary<int, int> NodeMap { get; } = new Dictionary<int, int>();

        // Rest position of each dense node (lattice coordinate / 2).
        // Needed by LoadVector and rigid-body mode construction.
        // frob:doc docs/reference/hullbreach-structure.md#stiffnessassembly
        public float2[] NodeRestPositions { get; private set; } = Array.Empty<float2>();

        // CSR storage: rowPtr has DofCount+1 entries, colIndex/values are
        // parallel arrays of the nonzero entries per row.
        int[] _rowPtr = Array.Empty<int>();
        int[] _colIndex = Array.Empty<int>();
        float[] _values = Array.Empty<float>();

        // frob:doc docs/reference/hullbreach-structure.md#stiffnessassembly
        public int DofCount { get; private set; }

        // Exposed read-only so GeometricStiffness can share this sparsity
        // pattern instead of re-deriving it. Callers must not mutate.
        // frob:doc docs/reference/hullbreach-structure.md#stiffnessassembly
        public int[] RowPointers => _rowPtr;

        // Sorted ascending within each row: callers may binary-search a
        // row's range. See RowPointers for why this is shared.
        // frob:doc docs/reference/hullbreach-structure.md#stiffnessassembly
        public int[] ColumnIndices => _colIndex;

        // Rebuild only when topology is dirty or damage changed stiffness;
        // recomputes everything (simplicity, assembly is cheap).
        // frob:doc docs/reference/hullbreach-structure.md#stiffnessassembly
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

                        // Keep every (row,col) pair even when v == 0f: the
                        // pattern must also fit GeometricStiffness's nonzeros.
                        long key = (long)row * DofCount + col;
                        entries.TryGetValue(key, out float prev);
                        entries[key] = prev + v;
                    }
                }
            }

            BuildCsr(entries);
        }

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

        // The only operation CG needs.
        // frob:doc docs/reference/hullbreach-structure.md#stiffnessassembly
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

        // For the Jacobi preconditioner.
        // frob:doc docs/reference/hullbreach-structure.md#stiffnessassembly
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
