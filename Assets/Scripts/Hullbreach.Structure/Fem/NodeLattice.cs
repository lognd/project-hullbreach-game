using System;

namespace Hullbreach.Structure
{
    /// <summary>
    /// Global node addressing for the Q8 mesh, on a lattice at TWICE the block
    /// resolution.
    ///
    /// Block (i,j) owns the 8 doubled-lattice points around it, excluding the
    /// center (Q8 is the serendipity element -- Q9 would use the center too):
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
        /// Doubled-lattice offsets of the 8 nodes, relative to (2i, 2j),
        /// in standard Q8 order.
        /// </summary>
        public static readonly int[,] Offsets =
        {
            { 0, 0 }, { 2, 0 }, { 2, 2 }, { 0, 2 },   // corners  1..4
            { 1, 0 }, { 2, 1 }, { 1, 2 }, { 0, 1 },   // midsides 5..8
        };

        // TODO [C1]: Pack a doubled-lattice coordinate into a node id. Must be
        //            injective over the doubled range (twice BlockKey's range,
        //            so BlockKey.Pack will NOT fit -- use a wider packing).
        public static int PackNode(int dx, int dy)
            => throw new NotImplementedException();

        // TODO [C1]: Write the 8 global node ids of block (x,y) into `into`
        //            (length 8), in the standard order above.
        public static void NodesOf(int x, int y, int[] into)
            => throw new NotImplementedException();

        // TODO [C1]: Build the dense node map for a whole grid: assign each
        //            distinct node id a contiguous index 0..n-1, because that
        //            index times 2 is its DOF offset in the global system.
        //            Rebuild only when topology is dirty.
        public static void BuildNodeMap(Hullbreach.Core.BlockGrid grid,
                                        System.Collections.Generic.Dictionary<int, int> nodeToDense)
            => throw new NotImplementedException();
    }
}
