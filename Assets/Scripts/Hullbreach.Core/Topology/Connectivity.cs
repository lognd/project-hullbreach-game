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
        /// <summary>
        /// Flood fill from the core, writing every reachable key into
        /// `reachable`. Left empty when the grid has no core -- a fragment is
        /// debris and has nothing to stay attached to.
        /// </summary>
        public static void ReachableFromCore(BlockGrid grid, HashSet<int> reachable)
        {
            reachable.Clear();
            if (!grid.CoreKey.HasValue) return;

            var stack = new Stack<int>();
            var neighbors = new int[4];

            stack.Push(grid.CoreKey.Value);
            reachable.Add(grid.CoreKey.Value);

            while (stack.Count > 0)
            {
                int current = stack.Pop();
                BlockKey.Neighbors(current, neighbors);
                for (int i = 0; i < 4; i++)
                {
                    int n = neighbors[i];
                    if (!grid.Contains(n)) continue;
                    if (reachable.Add(n)) stack.Push(n);
                }
            }
        }

        /// <summary>
        /// Everything NOT reachable from the core. In combat, apply every
        /// destruction for the tick FIRST and then call this once -- one fill
        /// for the whole batch, never one per block.
        /// </summary>
        public static void FindDetached(BlockGrid grid, List<int> detached)
        {
            detached.Clear();
            var reachable = new HashSet<int>();
            ReachableFromCore(grid, reachable);

            foreach (var kvp in grid.All)
            {
                if (!reachable.Contains(kvp.Key)) detached.Add(kvp.Key);
            }
        }

        /// <summary>
        /// Split a detached set into individual connected components, so a
        /// hit that shears off two separate chunks yields two debris bodies
        /// rather than one. BFS restricted to the given key set only -- same
        /// shape as ReachableFromCore, but bounded to `keys` instead of the
        /// whole grid.
        /// </summary>
        public static void SplitIntoComponents(BlockGrid grid, List<int> keys,
                                               List<List<int>> components)
        {
            components.Clear();
            var remaining = new HashSet<int>(keys);
            var neighbors = new int[4];

            while (remaining.Count > 0)
            {
                int seed = -1;
                foreach (var k in remaining) { seed = k; break; }

                var component = new List<int>();
                var stack = new Stack<int>();
                stack.Push(seed);
                remaining.Remove(seed);

                while (stack.Count > 0)
                {
                    int current = stack.Pop();
                    component.Add(current);

                    BlockKey.Neighbors(current, neighbors);
                    for (int i = 0; i < 4; i++)
                    {
                        int n = neighbors[i];
                        if (remaining.Remove(n)) stack.Push(n);
                    }
                }

                components.Add(component);
            }
        }
    }
}
