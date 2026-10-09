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
        public void TryRemove_ActuallyRemovesTheBlock()
        {
            var g = GridWithCore();
            var k = BlockKey.Pack(1, 0);
            g.TryAdd(k, new Block(BlockTypes.Hull));

            Assert.IsTrue(g.TryRemove(k));
            Assert.IsFalse(g.Contains(k), "the block must actually leave the dictionary");
            Assert.AreEqual(1, g.Count);

            // Re-adding at the same key must succeed: it would be refused
            // if TryRemove left a stale entry behind.
            Assert.IsTrue(g.TryAdd(k, new Block(BlockTypes.Hull)));
        }

        [Test]
        public void TrySet_WritesTheNewBlockAndUpdatesMass()
        {
            var g = GridWithCore();
            var k = BlockKey.Pack(1, 0);
            g.TryAdd(k, new Block(BlockTypes.Hull));

            var massBefore = g.Mass.Total;
            Assert.IsTrue(g.TrySet(k, new Block(BlockTypes.Armor)));

            g.TryGet(k, out var stored);
            Assert.AreEqual(BlockTypes.Armor, stored.TypeId, "TrySet must write the new block into the grid");

            var expectedDelta = BlockTypes.Get(BlockTypes.Armor).Mass - BlockTypes.Get(BlockTypes.Hull).Mass;
            Assert.AreEqual(massBefore + expectedDelta, g.Mass.Total, Tol);
        }

        [Test]
        public void CenterOf_FollowsTheGridConvention()
        {
            // Block (x,y) spans [x,x+1] x [y,y+1], so its center is offset by 0.5.
            var c = BlockGrid.CenterOf(BlockKey.Pack(3, -2));
            Assert.AreEqual(3.5f, c.x, Tol);
            Assert.AreEqual(-1.5f, c.y, Tol);
        }

        [Test]
        public void SortedKeys_StaySortedAfterAdds()
        {
            var g = GridWithCore(); // key 0
            g.TryAdd(BlockKey.Pack(5, 0), new Block(BlockTypes.Hull));
            g.TryAdd(BlockKey.Pack(-3, 0), new Block(BlockTypes.Hull));
            g.TryAdd(BlockKey.Pack(2, 0), new Block(BlockTypes.Hull));

            Assert.AreEqual(g.Count, g.KeyCount);
            var keys = g.SortedKeys.ToArray();
            for (int i = 1; i < keys.Length; i++)
            {
                Assert.Less(keys[i - 1], keys[i], "SortedKeys must be strictly ascending");
            }
            CollectionAssert.AreEquivalent(new[]
            {
                BlockKey.Pack(0, 0), BlockKey.Pack(5, 0), BlockKey.Pack(-3, 0), BlockKey.Pack(2, 0)
            }, keys);
        }

        [Test]
        public void SortedKeys_StaySortedAndInSyncAfterRemoves()
        {
            var g = GridWithCore();
            var a = BlockKey.Pack(1, 0);
            var b = BlockKey.Pack(2, 0);
            var c = BlockKey.Pack(3, 0);
            g.TryAdd(a, new Block(BlockTypes.Hull));
            g.TryAdd(b, new Block(BlockTypes.Hull));
            g.TryAdd(c, new Block(BlockTypes.Hull));

            Assert.IsTrue(g.TryRemove(b));

            Assert.AreEqual(g.Count, g.KeyCount);
            var keys = g.SortedKeys.ToArray();
            CollectionAssert.DoesNotContain(keys, b);
            for (int i = 1; i < keys.Length; i++)
            {
                Assert.Less(keys[i - 1], keys[i]);
            }
        }

        [Test]
        public void SortedKeys_UnaffectedByTrySet()
        {
            var g = GridWithCore();
            var k = BlockKey.Pack(1, 0);
            g.TryAdd(k, new Block(BlockTypes.Hull));

            var before = g.SortedKeys.ToArray();
            Assert.IsTrue(g.TrySet(k, new Block(BlockTypes.Armor)));
            var after = g.SortedKeys.ToArray();

            CollectionAssert.AreEqual(before, after, "TrySet must not change the key set");
        }

        [Test]
        public void TryAdd_UnknownTypeId_ReturnsFalseAndLeavesGridUnchanged()
        {
            var g = new BlockGrid();
            Assert.IsFalse(g.TryAdd(BlockKey.Pack(0, 0), new Block(255)));
            Assert.AreEqual(0, g.Count);
            Assert.IsNull(g.CoreKey);
            Assert.AreEqual(0f, g.Mass.Total);
            Assert.IsFalse(g.TopologyDirty);

            Assert.IsFalse(g.TryAdd(BlockKey.Pack(0, 0), new Block((byte)BlockTypes.Count)));
            Assert.IsTrue(g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core)), "grid still usable");
        }

        [Test]
        public void TrySet_UnknownTypeId_ReturnsFalseAndKeepsBlock()
        {
            var g = GridWithCore();
            var k = BlockKey.Pack(1, 0);
            g.TryAdd(k, new Block(BlockTypes.Hull));
            float mass = g.Mass.Total;

            Assert.IsFalse(g.TrySet(k, new Block(200)));
            g.TryGet(k, out var b);
            Assert.AreEqual(BlockTypes.Hull, b.TypeId);
            Assert.AreEqual(mass, g.Mass.Total, Tol);
        }

        [Test]
        public void TrySet_CannotCreateOrRemoveCoreness()
        {
            var g = GridWithCore();
            var k = BlockKey.Pack(1, 0);
            g.TryAdd(k, new Block(BlockTypes.Hull));

            Assert.IsFalse(g.TrySet(k, new Block(BlockTypes.Core)), "a second core via TrySet");
            Assert.IsFalse(g.TrySet(g.CoreKey.Value, new Block(BlockTypes.Hull)), "demoting the core via TrySet");
            Assert.IsTrue(g.TrySet(k, new Block(BlockTypes.Armor)), "non-core retype is fine");
        }

        [Test]
        public void KeyAt_PastKeyCount_ThrowsInsteadOfReturningStaleKey()
        {
            var g = GridWithCore();
            var k = BlockKey.Pack(1, 0);
            g.TryAdd(k, new Block(BlockTypes.Hull));
            Assert.AreEqual(2, g.KeyCount);
            g.TryRemove(k);

            Assert.AreEqual(1, g.KeyCount);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => g.KeyAt(1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => g.KeyAt(-1));
        }
    }
}
