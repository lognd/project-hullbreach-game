using System;
using System.Collections.Generic;
using System.Linq;

namespace Hullbreach.Structure
{
    /// <summary>
    /// Global node addressing for the Q8 mesh, on a lattice at TWICE the block
    /// resolution.
    ///
    /// Block (i,j) owns the 8 doubled-lattice points around it, excluding the
    /// center (Q8 is the serendipity element; Q9 would use the center too):
    ///
    ///     (2i,2j+2)---(2i+1,2j+2)---(2i+2,2j+2)
    ///         |                          |
    ///     (2i,2j+1)                 (2i+2,2j+1)      center (2i+1,2j+1)
    ///         |                          |           is NOT a node
    ///      (2i,2j)----(2i+1,2j)-----(2i+2,2j)
    ///
    /// The point of this scheme: node SHARING between adjacent blocks falls out
    /// by construction. Block (i+1,j) independently computes (2i+2, 2j),
    /// (2i+2, 2j+1) and (2i+2, 2j+2) and gets byte-identical ids. There is no
    /// dedup pass and no tolerance-based point merging to get wrong.
    ///
    /// Node ordering is the standard Q8 convention: corners 1-4 counterclockwise
    /// from the lower left, then midsides 5-8 starting between corners 1 and 2.
    /// </summary>
    public static class NodeLattice
    {
        public const int NodesPerElement = 8;

        /// <summary>
        /// Bias applied to each doubled-lattice axis before packing, so that
        /// the biased value is always non-negative over the supported range.
        /// BlockKey covers x,y in [-128,127], so the doubled lattice covers
        /// roughly [-256,256]; 1024 leaves comfortable headroom (matches the
        /// "injective at least over [-300,300]^2" requirement with margin).
        /// </summary>
        const int Bias = 1024;

        /// <summary>
        /// Bits to shift the biased x coordinate up by, before OR-ing in y.
        /// Biased coordinates fit in 16 bits (max ~2048), so 16 keeps the two
        /// axes from ever overlapping.
        /// </summary>
        const int Shift = 16;

        /// <summary>
        /// Doubled-lattice offsets of the 8 nodes, relative to (2i, 2j),
        /// in standard Q8 order.
        /// </summary>
        public static readonly int[,] Offsets =
        {
            { 0, 0 }, { 2, 0 }, { 2, 2 }, { 0, 2 },   // corners  1..4
            { 1, 0 }, { 2, 1 }, { 1, 2 }, { 0, 1 },   // midsides 5..8
        };

        /// <summary>
        /// Packs a doubled-lattice coordinate (dx, dy) into a single injective
        /// int id, by biasing both axes into non-negative range and packing
        /// x into the high bits, y into the low bits.
        /// </summary>
        public static int PackNode(int dx, int dy)
        {
            int bx = dx + Bias;
            int by = dy + Bias;
            return (bx << Shift) | by;
        }

        /// <summary>Exact inverse of <see cref="PackNode"/>.</summary>
        public static void UnpackNode(int node, out int dx, out int dy)
        {
            int bx = node >> Shift;
            int by = node & 0xFFFF;
            dx = bx - Bias;
            dy = by - Bias;
        }

        /// <summary>
        /// Writes the 8 global node ids of block (x,y) into `into` (length 8),
        /// in the standard order given by <see cref="Offsets"/>.
        /// </summary>
        public static void NodesOf(int x, int y, int[] into)
        {
            int bx = 2 * x;
            int by = 2 * y;
            for (int i = 0; i < NodesPerElement; i++)
            {
                into[i] = PackNode(bx + Offsets[i, 0], by + Offsets[i, 1]);
            }
        }

        /// <summary>
        /// Builds the dense node map for a whole grid: assigns each distinct
        /// node id a contiguous index 0..n-1. Blocks are visited in ascending
        /// key order (and nodes within a block in standard Q8 order) so that
        /// two independent builds over the same grid produce byte-identical
        /// maps: required for client/server agreement without shipping the
        /// map itself.
        /// </summary>
        public static void BuildNodeMap(Hullbreach.Core.BlockGrid grid,
                                        Dictionary<int, int> nodeToDense)
        {
            nodeToDense.Clear();

            var keys = grid.All.Select(kvp => kvp.Key).ToList();
            keys.Sort();

            var buf = new int[NodesPerElement];
            foreach (var key in keys)
            {
                Hullbreach.Core.BlockKey.Unpack(key, out int x, out int y);
                NodesOf(x, y, buf);
                for (int i = 0; i < NodesPerElement; i++)
                {
                    int node = buf[i];
                    if (!nodeToDense.ContainsKey(node))
                        nodeToDense[node] = nodeToDense.Count;
                }
            }
        }
    }
}
