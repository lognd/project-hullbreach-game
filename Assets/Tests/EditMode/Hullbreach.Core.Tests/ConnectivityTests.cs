using System.Collections.Generic;
using NUnit.Framework;
using Hullbreach.Core;

namespace Hullbreach.Core.Tests
{
    public class ConnectivityTests
    {
        static BlockGrid Line(int length)
        {
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            for (int x = 1; x < length; x++)
                g.TryAdd(BlockKey.Pack(x, 0), new Block(BlockTypes.Hull));
            return g;
        }

        [Test]
        public void IntactLine_IsEntirelyReachable()
        {
            var g = Line(3);
            var reachable = new HashSet<int>();
            Connectivity.ReachableFromCore(g, reachable);
            Assert.AreEqual(3, reachable.Count);
        }

        [Test]
        public void RemovingTheMiddle_DetachesTheFarEnd()
        {
            var g = Line(3);
            g.TryRemove(BlockKey.Pack(1, 0));

            var detached = new List<int>();
            Connectivity.FindDetached(g, detached);

            CollectionAssert.AreEquivalent(new[] { BlockKey.Pack(2, 0) }, detached);
        }

        [Test]
        public void RemovingAnEnd_DetachesNothing()
        {
            var g = Line(3);
            g.TryRemove(BlockKey.Pack(2, 0));

            var detached = new List<int>();
            Connectivity.FindDetached(g, detached);

            Assert.IsEmpty(detached);
        }

        [Test]
        public void RingSurvivesAnySingleRemoval()
        {
            // A 2x2 block of cells is a 4-cycle in the adjacency graph, so no
            // single removal can disconnect it.
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            g.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Hull));
            g.TryAdd(BlockKey.Pack(0, 1), new Block(BlockTypes.Hull));
            g.TryAdd(BlockKey.Pack(1, 1), new Block(BlockTypes.Hull));

            g.TryRemove(BlockKey.Pack(1, 1));

            var detached = new List<int>();
            Connectivity.FindDetached(g, detached);
            Assert.IsEmpty(detached);
        }

        [Test]
        public void GridWithoutCore_ReachesNothing()
        {
            // Debris has no core, so there is no root to flood from.
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Hull));

            var reachable = new HashSet<int>();
            Connectivity.ReachableFromCore(g, reachable);
            Assert.IsEmpty(reachable);
        }

        [Test]
        public void BatchedDestruction_YieldsTwoComponents()
        {
            //  core  H  H  X  H  H        removing X leaves two pieces,
            //  but only one of them is still attached to the core.
            var g = Line(6);
            g.TryRemove(BlockKey.Pack(3, 0));

            var detached = new List<int>();
            Connectivity.FindDetached(g, detached);

            CollectionAssert.AreEquivalent(
                new[] { BlockKey.Pack(4, 0), BlockKey.Pack(5, 0) }, detached);
        }
    }
}
