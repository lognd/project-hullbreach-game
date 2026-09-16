using System;
using System.Collections.Generic;

namespace Hullbreach.Core
{
    /// <summary>
    /// Cut vertices of the block adjacency graph: blocks whose removal would
    /// disconnect the ship.
    ///
    /// This is the optimization that makes removal cheap. Removing a block that
    /// is NOT an articulation point cannot split anything, so the flood fill is
    /// skipped outright. Ships are mostly 2-connected blobs, so most removals
    /// take the fast path.
    ///
    /// It doubles as UI: these are exactly the load-bearing blocks, which pairs
    /// naturally with the S37 stress tint.
    /// </summary>
    public static class Articulation
    {
        // TODO [B6]: Tarjan's algorithm, O(V + E), once per topology change.
        //            Use an ITERATIVE DFS -- a recursive one will blow the stack
        //            on a large ship.
        public static void Compute(BlockGrid grid, HashSet<int> articulationPoints)
            => throw new NotImplementedException();
    }
}
