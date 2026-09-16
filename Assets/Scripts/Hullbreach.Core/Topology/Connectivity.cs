using System;
using System.Collections.Generic;

namespace Hullbreach.Core
{
    /// <summary>
    /// Which blocks are still attached to the core (S38).
    ///
    /// This is a plain flood fill and it should stay one. At a few hundred
    /// blocks an O(n) pass is microseconds. Union-Find handles unions but not
    /// deletions, and deletion-capable structures (Euler tour trees, link-cut
    /// trees) are wildly out of proportion to the problem.
    ///
    /// DETERMINISM: this is all integer work, so it reproduces bit-exactly on
    /// every platform -- unlike the float FE solve, which does not. That
    /// asymmetry is what lets the server send only "block (x,y) died" and have
    /// both sides independently derive the same detached components, instead of
    /// ever putting a block list on the wire.
    /// </summary>
    public static class Connectivity
    {
        // TODO [B5]: Flood fill from the core, writing every reachable key into
        //            `reachable`. Return an empty set when the grid has no core
        //            -- a fragment is debris and has nothing to stay attached to.
        public static void ReachableFromCore(BlockGrid grid, HashSet<int> reachable)
            => throw new NotImplementedException();

        // TODO [B5]: Everything NOT reachable from the core. In combat, apply
        //            every destruction for the tick FIRST and then call this
        //            once -- one fill for the whole batch, never one per block.
        public static void FindDetached(BlockGrid grid, List<int> detached)
            => throw new NotImplementedException();

        // TODO [D5]: Split a detached set into individual connected components,
        //            so a hit that shears off two separate chunks yields two
        //            debris bodies rather than one.
        public static void SplitIntoComponents(BlockGrid grid, List<int> keys,
                                               List<List<int>> components)
            => throw new NotImplementedException();
    }
}
