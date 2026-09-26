using System;
using System.Collections.Generic;

namespace Hullbreach.Core
{
    // Which blocks are still attached to the core (S38); plain flood fill.
    // See docs/reference/hullbreach-core.md#connectivity.
    // frob:doc docs/reference/hullbreach-core.md#connectivity
    public static class Connectivity
    {
        // Left empty when the grid has no core (a fragment is debris).
        // frob:doc docs/reference/hullbreach-core.md#connectivity
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

        // Apply every destruction for the tick FIRST, then call once.
        // frob:doc docs/reference/hullbreach-core.md#connectivity
        public static void FindDetached(BlockGrid grid, List<int> detached)
        {
            detached.Clear();
            var reachable = new HashSet<int>();
            ReachableFromCore(grid, reachable);

            var keys = grid.SortedKeys;
            for (int i = 0; i < keys.Length; i++)
            {
                int key = keys[i];
                if (!reachable.Contains(key)) detached.Add(key);
            }
        }

        // BFS restricted to `keys` only, so N debris chunks stay N components.
        // frob:doc docs/reference/hullbreach-core.md#connectivity
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
