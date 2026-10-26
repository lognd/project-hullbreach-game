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

        [Test]
        public void SplitIntoComponents_SeparatesTwoDisjointChunks()
        {
            // Two separate 1x2 debris chunks, far enough apart to share no
            // adjacency, must come back as two components.
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            g.TryAdd(BlockKey.Pack(10, 0), new Block(BlockTypes.Hull));
            g.TryAdd(BlockKey.Pack(10, 1), new Block(BlockTypes.Hull));
            g.TryAdd(BlockKey.Pack(20, 0), new Block(BlockTypes.Hull));
            g.TryAdd(BlockKey.Pack(20, 1), new Block(BlockTypes.Hull));

            var keys = new List<int>
            {
                BlockKey.Pack(10, 0), BlockKey.Pack(10, 1),
                BlockKey.Pack(20, 0), BlockKey.Pack(20, 1),
            };

            var components = new List<List<int>>();
            Connectivity.SplitIntoComponents(g, keys, components);

            Assert.AreEqual(2, components.Count);
            foreach (var component in components)
                Assert.AreEqual(2, component.Count);
        }

        [Test]
        public void SplitIntoComponents_KeepsOneChunkTogether()
        {
            var g = Line(3);
            var keys = new List<int> { BlockKey.Pack(1, 0), BlockKey.Pack(2, 0) };

            var components = new List<List<int>>();
            Connectivity.SplitIntoComponents(g, keys, components);

            Assert.AreEqual(1, components.Count);
            CollectionAssert.AreEquivalent(keys, components[0]);
        }

        [Test]
        public void FarEdgeCells_AreNotFalselyAdjacent()
        {
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, BlockKey.Max), new Block(BlockTypes.Core));
            g.TryAdd(BlockKey.Pack(1, BlockKey.Min), new Block(BlockTypes.Hull));

            var detached = new List<int>();
            Connectivity.FindDetached(g, detached);

            CollectionAssert.AreEquivalent(new[] { BlockKey.Pack(1, BlockKey.Min) }, detached);
        }

        // frob:ticket 01M3DG5Y1Z912W21FF70NBN22Q
        [Test]
        public void ClassifyCore_IntactLine_IsIntact()
        {
            Assert.AreEqual(CoreState.Intact, Connectivity.ClassifyCore(Line(4)));
        }

        // frob:ticket 01M3DG5Y1Z912W21FF70NBN22Q
        [Test]
        public void ClassifyCore_LoneCore_IsIntact()
        {
            // A bare core has nothing to be cut off from.
            Assert.AreEqual(CoreState.Intact, Connectivity.ClassifyCore(Line(1)));
        }

        // frob:ticket 01M3DG5Y1Z912W21FF70NBN22Q
        [Test]
        public void ClassifyCore_NoCore_IsDestroyed()
        {
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Hull));
            Assert.AreEqual(CoreState.Destroyed, Connectivity.ClassifyCore(g));
        }

        // frob:ticket 01M3DG5Y1Z912W21FF70NBN22Q
        [Test]
        public void ClassifyCore_FullyDamagedCore_IsDestroyed()
        {
            var g = Line(3);
            int core = g.CoreKey.Value;
            g.TrySet(core, new Block(BlockTypes.Core).WithDamage(Connectivity.DestroyedDamage));
            Assert.AreEqual(CoreState.Destroyed, Connectivity.ClassifyCore(g));
        }

        // frob:ticket 01M3DG5Y1Z912W21FF70NBN22Q
        [Test]
        public void ClassifyCore_DamagedButStanding_IsIntact()
        {
            var g = Line(3);
            g.TrySet(g.CoreKey.Value,
                new Block(BlockTypes.Core).WithDamage(Connectivity.DestroyedDamage - 1));
            Assert.AreEqual(CoreState.Intact, Connectivity.ClassifyCore(g));
        }

        // frob:ticket 01M3DG5Y1Z912W21FF70NBN22Q
        [Test]
        public void ClassifyCore_CoreWithNoNeighbourButOtherBlocks_IsIsolated()
        {
            //  core  .  H   : the hull is somewhere, but nothing touches the core.
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            g.TryAdd(BlockKey.Pack(2, 0), new Block(BlockTypes.Hull));
            Assert.AreEqual(CoreState.Isolated, Connectivity.ClassifyCore(g));
        }

        // frob:ticket 01M3DG5Y1Z912W21FF70NBN22Q
        [Test]
        public void ClassifyCore_NeighbourPresent_IsNotIsolated()
        {
            // Debris elsewhere does not matter once the core has an attached neighbour.
            var g = Line(2);
            g.TryAdd(BlockKey.Pack(5, 5), new Block(BlockTypes.Hull));
            Assert.AreEqual(CoreState.Intact, Connectivity.ClassifyCore(g));
        }
    }
}
