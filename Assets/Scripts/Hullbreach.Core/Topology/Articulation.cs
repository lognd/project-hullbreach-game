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
        /// <summary>
        /// One DFS-stack frame. Kept as a class (not a struct) so the `into`
        /// neighbor buffer and the running child index can be mutated in
        /// place while the frame sits in the stack.
        /// </summary>
        sealed class Frame
        {
            public int Node;
            public int Parent;
            public int NeighborIndex;
            public int ChildCount;
            public readonly int[] Neighbors = new int[4];
        }

        /// <summary>
        /// Tarjan's algorithm, O(V + E), once per topology change. Uses an
        /// ITERATIVE DFS -- a recursive one would blow the stack on a large
        /// ship. Works with no core (any block can serve as the DFS root,
        /// since articulation points are a property of the adjacency graph
        /// alone) and correctly reports no articulation points for a single
        /// block (a root is only a cut vertex when it has 2+ DFS children).
        /// </summary>
        public static void Compute(BlockGrid grid, HashSet<int> articulationPoints)
        {
            articulationPoints.Clear();
            if (grid.Count == 0) return;

            var disc = new Dictionary<int, int>();
            var low = new Dictionary<int, int>();
            int timer = 0;

            foreach (var kvp in grid.All)
            {
                int root = kvp.Key;
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
                            // Skip exactly the one edge back to the immediate
                            // tree parent; 4-connected grid adjacency has at
                            // most one such edge, so this cannot also eat a
                            // legitimate back-edge to an ancestor further up.
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
