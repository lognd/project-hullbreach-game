using NUnit.Framework;
using Hullbreach.Core;

namespace Hullbreach.Core.Tests
{
    public class BlockGridTests
    {
        const float Tol = 1e-4f;

        static BlockGrid GridWithCore()
        {
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            return g;
        }

        [Test]
        public void NewGrid_IsEmptyAndHasNoCore()
        {
            var g = new BlockGrid();
            Assert.AreEqual(0, g.Count);
            Assert.IsNull(g.CoreKey, "a fresh grid has no core until one is placed");
        }

        [Test]
        public void AddingCore_RecordsCoreKey()
        {
            var g = GridWithCore();
            Assert.AreEqual(BlockKey.Pack(0, 0), g.CoreKey);
        }

        [Test]
        public void TryAdd_RefusesAnOccupiedCell()
        {
            var g = GridWithCore();
            Assert.IsFalse(g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Hull)));
            Assert.AreEqual(1, g.Count);
        }

        [Test]
        public void TryRemove_RefusesTheCore()
        {
            // S32: a new ship is exactly one core block and it cannot be removed.
            var g = GridWithCore();
            Assert.IsFalse(g.TryRemove(BlockKey.Pack(0, 0)));
            Assert.AreEqual(1, g.Count);
        }

        [Test]
        public void AddThenRemove_RestoresMassExactly()
        {
            var g = GridWithCore();
            var before = g.Mass.Total;

            var k = BlockKey.Pack(1, 0);
            g.TryAdd(k, new Block(BlockTypes.Hull));
            Assert.Greater(g.Mass.Total, before);

            g.TryRemove(k);
            Assert.AreEqual(before, g.Mass.Total, Tol);
        }

        [Test]
        public void TryAdd_MarksTopologyDirty()
        {
            var g = GridWithCore();
            g.ClearDirty();
            g.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Hull));
            Assert.IsTrue(g.TopologyDirty);
        }

        [Test]
        public void TrySet_DoesNotMarkTopologyDirty()
        {
            // Damage changes stiffness, not connectivity. Marking dirty here
            // would throw away cached factorisations every damage tick.
            var g = GridWithCore();
            var k = BlockKey.Pack(1, 0);
            g.TryAdd(k, new Block(BlockTypes.Hull));
            g.ClearDirty();

            g.TryGet(k, out var b);
            Assert.IsTrue(g.TrySet(k, b.WithDamage(128)));
            Assert.IsFalse(g.TopologyDirty);
        }

        [Test]
        public void CenterOf_FollowsTheGridConvention()
        {
            // Block (x,y) spans [x,x+1] x [y,y+1], so its center is offset by 0.5.
            var c = BlockGrid.CenterOf(BlockKey.Pack(3, -2));
            Assert.AreEqual(3.5f, c.x, Tol);
            Assert.AreEqual(-1.5f, c.y, Tol);
        }
    }
}
