using System;
using System.Collections.Generic;
using System.Linq;

namespace Hullbreach.Structure
{
    // Global node addressing for the Q8 mesh, on a lattice at TWICE the
    // block resolution; see docs/reference/hullbreach-structure.md#nodelattice.
    // frob:doc docs/reference/hullbreach-structure.md#nodelattice
    public static class NodeLattice
    {
        // frob:doc docs/reference/hullbreach-structure.md#nodelattice
        public const int NodesPerElement = 8;

        // BlockKey covers [-128,127], so the doubled lattice covers
        // roughly [-256,256]; 1024 leaves comfortable headroom.
        const int Bias = 1024;

        // Biased coordinates fit in 16 bits (max ~2048), so 16 keeps the
        // two axes from ever overlapping.
        const int Shift = 16;

        // Doubled-lattice offsets of the 8 nodes, relative to (2i, 2j), in
        // standard Q8 order.
        // frob:doc docs/reference/hullbreach-structure.md#nodelattice
        public static readonly int[,] Offsets =
        {
            { 0, 0 }, { 2, 0 }, { 2, 2 }, { 0, 2 },   // corners  1..4
            { 1, 0 }, { 2, 1 }, { 1, 2 }, { 0, 1 },   // midsides 5..8
        };

        // Packs (dx, dy) into a single injective int id: bias both axes
        // non-negative, x into the high bits, y into the low bits.
        // frob:doc docs/reference/hullbreach-structure.md#nodelattice
        public static int PackNode(int dx, int dy)
        {
            int bx = dx + Bias;
            int by = dy + Bias;
            return (bx << Shift) | by;
        }

        // Exact inverse of PackNode.
        // frob:doc docs/reference/hullbreach-structure.md#nodelattice
        public static void UnpackNode(int node, out int dx, out int dy)
        {
            int bx = node >> Shift;
            int by = node & 0xFFFF;
            dx = bx - Bias;
            dy = by - Bias;
        }

        // Writes the 8 global node ids of block (x,y) into `into` (length
        // 8), in the standard order given by Offsets.
        // frob:doc docs/reference/hullbreach-structure.md#nodelattice
        public static void NodesOf(int x, int y, int[] into)
        {
            int bx = 2 * x;
            int by = 2 * y;
            for (int i = 0; i < NodesPerElement; i++)
            {
                into[i] = PackNode(bx + Offsets[i, 0], by + Offsets[i, 1]);
            }
        }

        // Visits blocks in ascending key order so two independent builds
        // over the same grid produce byte-identical maps.
        // frob:doc docs/reference/hullbreach-structure.md#nodelattice
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
