using System;
using System.Collections.Generic;

namespace Hullbreach.Core
{
    // Cut vertices of the block adjacency graph: blocks whose removal would
    // disconnect the ship. See docs/reference/hullbreach-core.md#articulation.
    // frob:doc docs/reference/hullbreach-core.md#articulation
    public static class Articulation
    {
        // Kept as a class so the `into` neighbor buffer and child index can
        // be mutated in place while the frame sits in the stack.
        sealed class Frame
        {
            public int Node;
            public int Parent;
            public int NeighborIndex;
            public int ChildCount;
            public readonly int[] Neighbors = new int[4];
        }

        // Tarjan's algorithm, O(V + E); ITERATIVE DFS so a large ship
        // cannot blow the stack. See docs/reference/hullbreach-core.md#articulation.
        // frob:doc docs/reference/hullbreach-core.md#articulation
        public static void Compute(BlockGrid grid, HashSet<int> articulationPoints)
        {
            articulationPoints.Clear();
            if (grid.Count == 0) return;

            var disc = new Dictionary<int, int>();
            var low = new Dictionary<int, int>();
            int timer = 0;

            var rootKeys = grid.SortedKeys;
            for (int rootIndex = 0; rootIndex < rootKeys.Length; rootIndex++)
            {
                int root = rootKeys[rootIndex];
                if (disc.ContainsKey(root)) continue;

                // -1 is a safe "no parent" sentinel: packed keys are always
                // in [0, 65535] (see BlockKey.Pack), so no real key is negative.
                const int NoParent = -1;

                var stack = new Stack<Frame>();
                var rootFrame = new Frame { Node = root, Parent = NoParent };
                disc[root] = low[root] = timer++;
                stack.Push(rootFrame);

                while (stack.Count > 0)
                {
                    var frame = stack.Peek();

                    if (frame.NeighborIndex == 0)
                        BlockKey.Neighbors(frame.Node, frame.Neighbors);

                    if (frame.NeighborIndex < 4)
                    {
                        int child = frame.Neighbors[frame.NeighborIndex];
                        frame.NeighborIndex++;

                        if (!grid.Contains(child)) continue;
                        if (child == frame.Parent)
                        {
                            // Skip only the one edge back to the immediate
                            // tree parent (4-connected adjacency has at most one).
                            continue;
                        }

                        if (!disc.ContainsKey(child))
                        {
                            frame.ChildCount++;
                            disc[child] = low[child] = timer++;
                            stack.Push(new Frame { Node = child, Parent = frame.Node });
                        }
                        else
                        {
                            low[frame.Node] = Math.Min(low[frame.Node], disc[child]);
                        }
                    }
                    else
                    {
                        stack.Pop();

                        if (stack.Count > 0)
                        {
                            var parentFrame = stack.Peek();
                            low[parentFrame.Node] = Math.Min(low[parentFrame.Node], low[frame.Node]);

                            bool parentIsDfsRoot = parentFrame.Parent == NoParent;
                            if (parentIsDfsRoot)
                            {
                                if (parentFrame.ChildCount > 1)
                                    articulationPoints.Add(parentFrame.Node);
                            }
                            else if (low[frame.Node] >= disc[parentFrame.Node])
                            {
                                articulationPoints.Add(parentFrame.Node);
                            }
                        }
                    }
                }
            }
        }
    }
}
