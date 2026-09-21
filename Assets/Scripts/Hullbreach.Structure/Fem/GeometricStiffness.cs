using System;
using System.Collections.Generic;
using System.Linq;

namespace Hullbreach.Structure
{
    /// <summary>
    /// The geometric (initial-stress) stiffness K_G: the part of the tangent
    /// stiffness that comes from stress already in the structure rather than
    /// from material response. It is what makes a compressed member go
    /// unstable: K_G is (locally) negative-definite along a compressive
    /// principal direction, so K + lambda*K_G loses positive-definiteness as
    /// the load factor lambda grows. That crossing point is buckling.
    ///
    /// PER-ELEMENT G AND S: at a Gauss point, G is the 4x16 matrix of
    /// displacement-gradient rows (not strain, gradients, so shear does not
    /// get the usual engineering factor of 2):
    ///     row 0: dN/dx for u   row 1: dN/dy for u
    ///     row 2: dN/dx for v   row 3: dN/dy for v
    /// and S = blockdiag(sigma, sigma) with sigma = [[Sxx,Txy],[Txy,Syy]], so
    /// K_G_e = integral G^T S G dV, by the same 3x3 Gauss rule as
    /// <see cref="Q8Element.UnitStiffness"/>.
    ///
    /// CONSTANT-STRESS SIMPLIFICATION: this uses ONE stress tensor per
    /// element (the element-CENTER stress StructuralSolver already computes
    /// for BlockStress), not the true Gauss-point stress, which varies over
    /// the element. That is an approximation: a per-Gauss-point stress,
    /// recovered the same way BlockStress recovers the center stress but at
    /// each of the 9 points, would be more accurate. It is deliberately not
    /// done here: it would 9x the per-tick stress recovery cost for a
    /// correction that only matters right at a stress concentration, and the
    /// buckling analysis already only runs every BucklingEveryNTicks ticks.
    /// Left as a documented later refinement, not a TODO marker (no ticket
    /// system in this repo; see TODO.md).
    ///
    /// SPARSITY: shares K's exact CSR row/column pattern via
    /// <see cref="AttachSparsity"/> (see StiffnessAssembly.RowPointers for
    /// why sharing the pattern beats re-deriving it). Only Rebuild's VALUE
    /// scatter runs per buckling-eligible tick; <see cref="Multiply"/> is
    /// the same sparse mat-vec as StiffnessAssembly, over K_G's own values.
    /// </summary>
    public sealed class GeometricStiffness
    {
        int[] _rowPtr = Array.Empty<int>();
        int[] _colIndex = Array.Empty<int>();
        float[] _values = Array.Empty<float>();

        /// <summary>Degrees of freedom, mirrored from the attached
        /// StiffnessAssembly so callers can size DOF-length work vectors.</summary>
        public int DofCount { get; private set; }

        // Fixed-size scratch reused across every element in Rebuild: sizes
        // are compile-time constants (8 nodes, 16 dofs), so these allocate
        // exactly once per GeometricStiffness instance, never per tick.
        readonly float[] _dNdXi = new float[Q8Element.NodeCount];
        readonly float[] _dNdEta = new float[Q8Element.NodeCount];
        readonly float[,] _g = new float[4, Q8Element.DofCount];
        readonly float[,] _sg = new float[4, Q8Element.DofCount];
        readonly float[,] _kgLocal = new float[Q8Element.DofCount, Q8Element.DofCount];
        readonly int[] _nodeIds = new int[NodeLattice.NodesPerElement];
        readonly int[] _dofs = new int[Q8Element.DofCount];

        static readonly float[] GaussPts = { -(float)Math.Sqrt(3.0 / 5.0), 0f, (float)Math.Sqrt(3.0 / 5.0) };
        static readonly float[] GaussWts = { 5f / 9f, 8f / 9f, 5f / 9f };

        /// <summary>
        /// Adopts `assembly`'s current CSR row/column pattern by reference
        /// (StiffnessAssembly does not mutate those arrays in place; Rebuild
        /// always allocates fresh ones), so holding a reference across ticks
        /// is safe until the next topology Rebuild, at which point the caller
        /// must call AttachSparsity again). Resizes the value array only when
        /// the pattern's nnz actually changed.
        /// </summary>
        public void AttachSparsity(StiffnessAssembly assembly)
        {
            _rowPtr = assembly.RowPointers;
            _colIndex = assembly.ColumnIndices;
            DofCount = assembly.DofCount;
            if (_values.Length != _colIndex.Length)
                _values = new float[_colIndex.Length];
        }

        /// <summary>
        /// Re-scatters K_G's values for the current stress state, in place.
        /// Allocation-free on the hot path: the only per-tick work is
        /// clearing and re-filling the preallocated `_values` array plus the
        /// fixed-size scratch above. Blocks are visited in ascending key
        /// order (matching NodeLattice.BuildNodeMap) so float accumulation at
        /// shared nodes happens in the same order every time: required for
        /// the determinism the buckling analysis promises.
        /// </summary>
        public void Rebuild(Hullbreach.Core.BlockGrid grid, StiffnessAssembly assembly,
                            IReadOnlyDictionary<int, BlockStress> stresses)
        {
            Array.Clear(_values, 0, _values.Length);

            var keys = grid.All.Select(kvp => kvp.Key).ToList();
            keys.Sort();

            float h = Hullbreach.Core.BlockType.Width;

            foreach (var key in keys)
            {
                Hullbreach.Core.BlockKey.Unpack(key, out int x, out int y);
                NodeLattice.NodesOf(x, y, _nodeIds);
                for (int i = 0; i < NodeLattice.NodesPerElement; i++)
                {
                    int dense = assembly.NodeMap[_nodeIds[i]];
                    _dofs[2 * i] = 2 * dense;
                    _dofs[2 * i + 1] = 2 * dense + 1;
                }

                if (!stresses.TryGetValue(key, out var stress))
                    continue; // no stress recorded (should not happen); leaves this element's contribution at zero.

                ElementMatrix(stress.Sxx, stress.Syy, stress.Txy, h, _kgLocal);

                for (int i = 0; i < Q8Element.DofCount; i++)
                {
                    int row = _dofs[i];
                    int rowStart = _rowPtr[row];
                    int rowEnd = _rowPtr[row + 1];
                    for (int j = 0; j < Q8Element.DofCount; j++)
                    {
                        float v = _kgLocal[i, j];
                        if (v == 0f) continue;
                        int col = _dofs[j];
                        int idx = FindColumn(rowStart, rowEnd, col);
                        _values[idx] += v;
                    }
                }
            }
        }

        /// <summary>Binary search for `col` within colIndex[start,end): the
        /// row's slice is sorted ascending by StiffnessAssembly.BuildCsr.
        /// Throws (via the unhandled -1) if the pattern does not contain the
        /// entry, which would mean K and K_G disagree on sparsity: a
        /// programmer bug, not a runtime condition to handle gracefully.</summary>
        int FindColumn(int start, int end, int col)
        {
            int lo = start, hi = end - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int c = _colIndex[mid];
                if (c == col) return mid;
                if (c < col) lo = mid + 1; else hi = mid - 1;
            }
            throw new InvalidOperationException("K_G sparsity does not match K's pattern");
        }

        /// <summary>y = K_G * x, identical CSR mat-vec pattern to
        /// StiffnessAssembly.Multiply.</summary>
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

        /// <summary>
        /// The 16x16 element geometric stiffness for a constant element
        /// stress (Sxx,Syy,Txy) on a side-`h` block: integral of G^T S G by
        /// 3x3 Gauss, mirroring Q8Element.UnitStiffness's structure exactly
        /// so the two are easy to compare line for line.
        /// </summary>
        public void ElementMatrix(float sxx, float syy, float txy, float h, float[,] kg)
        {
            for (int i = 0; i < Q8Element.DofCount; i++)
            for (int j = 0; j < Q8Element.DofCount; j++)
                kg[i, j] = 0f;

            float scale = 2f / h;
            float detJ = (h / 2f) * (h / 2f);

            for (int gi = 0; gi < 3; gi++)
            for (int gj = 0; gj < 3; gj++)
            {
                float xi = GaussPts[gi];
                float eta = GaussPts[gj];
                float w = GaussWts[gi] * GaussWts[gj] * detJ;

                Q8Element.ShapeDerivatives(xi, eta, _dNdXi, _dNdEta);

                for (int i = 0; i < 4; i++)
                for (int c = 0; c < Q8Element.DofCount; c++)
                    _g[i, c] = 0f;

                for (int i = 0; i < Q8Element.NodeCount; i++)
                {
                    float dNdx = scale * _dNdXi[i];
                    float dNdy = scale * _dNdEta[i];
                    int cu = 2 * i;
                    int cv = 2 * i + 1;

                    _g[0, cu] = dNdx;
                    _g[1, cu] = dNdy;
                    _g[2, cv] = dNdx;
                    _g[3, cv] = dNdy;
                }

                // S*G, using S = blockdiag(sigma, sigma) explicitly rather
                // than materializing the 4x4 (it is almost entirely zero).
                for (int c = 0; c < Q8Element.DofCount; c++)
                {
                    float g0 = _g[0, c], g1 = _g[1, c], g2 = _g[2, c], g3 = _g[3, c];
                    _sg[0, c] = sxx * g0 + txy * g1;
                    _sg[1, c] = txy * g0 + syy * g1;
                    _sg[2, c] = sxx * g2 + txy * g3;
                    _sg[3, c] = txy * g2 + syy * g3;
                }

                for (int i = 0; i < Q8Element.DofCount; i++)
                for (int j = 0; j < Q8Element.DofCount; j++)
                {
                    float sum = 0f;
                    for (int k = 0; k < 4; k++)
                        sum += _g[k, i] * _sg[k, j];
                    kg[i, j] += sum * w;
                }
            }
        }
    }
}
