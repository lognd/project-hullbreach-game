using System.Collections.Generic;
using NUnit.Framework;
using Hullbreach.Core;
using Hullbreach.Builder;

namespace Hullbreach.Builder.Tests
{
    public class PlacementRulesTests
    {
        [Test]
        public void EmptyGrid_OnlyAcceptsCore()
        {
            var g = new BlockGrid();
            Assert.IsFalse(PlacementRules.CanPlace(g, BlockKey.Pack(0, 0), BlockTypes.Hull));
            Assert.IsTrue(PlacementRules.CanPlace(g, BlockKey.Pack(0, 0), BlockTypes.Core));
        }

        [Test]
        public void NonEmptyGrid_RefusesSecondCore()
        {
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            Assert.IsFalse(PlacementRules.CanPlace(g, BlockKey.Pack(1, 0), BlockTypes.Core));
        }

        [Test]
        public void CanPlace_RequiresAdjacency()
        {
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));

            Assert.IsTrue(PlacementRules.CanPlace(g, BlockKey.Pack(1, 0)));
            Assert.IsFalse(PlacementRules.CanPlace(g, BlockKey.Pack(5, 5)), "not adjacent to anything");
        }

        [Test]
        public void CanPlace_RefusesOccupiedCell()
        {
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            Assert.IsFalse(PlacementRules.CanPlace(g, BlockKey.Pack(0, 0)));
        }

        [Test]
        public void CanPlace_RefusesOutOfRange()
        {
            // A cell just past BlockKey.Max is adjacent to a real block but
            // out of the packable range, and must be refused.
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(BlockKey.Max, 0), new Block(BlockTypes.Core));
            Assert.IsFalse(BlockKey.InRange(BlockKey.Max + 1, 0));
        }

        [Test]
        public void CanRemove_RefusesCore()
        {
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            Assert.IsFalse(PlacementRules.CanRemove(g, BlockKey.Pack(0, 0)));
        }

        [Test]
        public void CanRemove_RefusesMissingBlock()
        {
            var g = new BlockGrid();
            Assert.IsFalse(PlacementRules.CanRemove(g, BlockKey.Pack(0, 0)));
        }

        [Test]
        public void CanRemove_AllowsOrdinaryBlock()
        {
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            g.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Hull));
            Assert.IsTrue(PlacementRules.CanRemove(g, BlockKey.Pack(1, 0)));
        }

        [Test]
        public void Detach_OfNonArticulationPoint_RemovesOnlyThatBlock()
        {
            // A 2x2 loop: no block here is a cut vertex.
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            g.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Hull));
            g.TryAdd(BlockKey.Pack(0, 1), new Block(BlockTypes.Hull));
            g.TryAdd(BlockKey.Pack(1, 1), new Block(BlockTypes.Hull));

            var removed = new List<int>();
            Assert.IsTrue(PlacementRules.Detach(g, BlockKey.Pack(1, 1), removed));

            Assert.AreEqual(1, removed.Count);
            Assert.AreEqual(3, g.Count);
        }

        [Test]
        public void Detach_OfArticulationPoint_StrandsAndRemovesFarSide()
        {
            // Core - A - B - C, a straight line. Removing A strands B and C.
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            g.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Hull)); // A
            g.TryAdd(BlockKey.Pack(2, 0), new Block(BlockTypes.Hull)); // B
            g.TryAdd(BlockKey.Pack(3, 0), new Block(BlockTypes.Hull)); // C

            var removed = new List<int>();
            Assert.IsTrue(PlacementRules.Detach(g, BlockKey.Pack(1, 0), removed));

            CollectionAssert.AreEquivalent(
                new[] { BlockKey.Pack(1, 0), BlockKey.Pack(2, 0), BlockKey.Pack(3, 0) },
                removed);
            Assert.AreEqual(1, g.Count, "only the core should remain");
            Assert.IsTrue(g.Contains(BlockKey.Pack(0, 0)));
        }

        [Test]
        public void Detach_RefusesCore()
        {
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));

            var removed = new List<int>();
            Assert.IsFalse(PlacementRules.Detach(g, BlockKey.Pack(0, 0), removed));
            Assert.AreEqual(1, g.Count);
        }
    }
}
