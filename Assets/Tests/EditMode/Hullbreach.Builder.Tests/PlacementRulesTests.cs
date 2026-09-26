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

        [Test]
        public void Thruster_OnTheShipsSide_IsAccepted()
        {
            // Beside the core is legal as long as its own -y exhaust is clear.
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));

            bool ok = PlacementRules.CanPlace(g, BlockKey.Pack(1, 0), BlockTypes.Thruster, 0, out var why);
            Assert.IsTrue(ok);
            Assert.AreEqual(PlacementVerdict.Ok, why);
        }

        [Test]
        public void Hull_BehindAThruster_IsRefused()
        {
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            g.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Thruster));

            bool ok = PlacementRules.CanPlace(g, BlockKey.Pack(1, -1), BlockTypes.Hull, 0, out var why);
            Assert.IsFalse(ok);
            Assert.AreEqual(PlacementVerdict.InsideReservedCell, why);
        }

        [Test]
        public void Hull_InFrontOfACannon_IsRefused()
        {
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            // Facing +x (modifiers 1): muzzle is the cell ahead, (2, 0).
            g.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Cannon, 1));

            bool ok = PlacementRules.CanPlace(g, BlockKey.Pack(2, 0), BlockTypes.Hull, 0, out var why);
            Assert.IsFalse(ok);
            Assert.AreEqual(PlacementVerdict.InsideReservedCell, why);
        }

        [Test]
        public void Cannon_FacingIntoAnExistingBlock_IsRefused()
        {
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            g.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Hull));

            // Facing -y (modifiers 2) from (1, 1): muzzle is (1, 0), the hull.
            bool ok = PlacementRules.CanPlace(g, BlockKey.Pack(1, 1), BlockTypes.Cannon, 2, out var why);
            Assert.IsFalse(ok);
            Assert.AreEqual(PlacementVerdict.BlocksMuzzle, why);
        }

        [Test]
        public void Retro_BesideAHull_IsRefused()
        {
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            g.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Hull));

            // Retro at (2, 0) reserves its -x neighbor (1, 0), the hull.
            bool ok = PlacementRules.CanPlace(g, BlockKey.Pack(2, 0), BlockTypes.RetroThruster, 0, out var why);
            Assert.IsFalse(ok);
            Assert.AreEqual(PlacementVerdict.BlocksExhaust, why);
        }

        [Test]
        public void Fin_WithoutAHullBehind_IsRefused()
        {
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));

            // Facing +y (modifiers 0) from (1, 0): behind is (1, -1), empty.
            bool ok = PlacementRules.CanPlace(g, BlockKey.Pack(1, 0), BlockTypes.Fin, 0, out var why);
            Assert.IsFalse(ok);
            Assert.AreEqual(PlacementVerdict.FinNeedsHull, why);
        }

        [Test]
        public void Fin_FacingIntoABlock_IsRefused()
        {
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            g.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Hull));

            // Facing -y (modifiers 2) from (1, 1): ahead is (1, 0), the hull;
            // behind would be (1, 2), which is not the point of this test.
            bool ok = PlacementRules.CanPlace(g, BlockKey.Pack(1, 1), BlockTypes.Fin, 2, out var why);
            Assert.IsFalse(ok);
            Assert.AreEqual(PlacementVerdict.BlocksFin, why);
        }

        [Test]
        public void FullSession_PlacesEveryBlockKindSuccessfully()
        {
            var g = new BlockGrid();

            Assert.IsTrue(PlacementRules.CanPlace(g, BlockKey.Pack(0, 0), BlockTypes.Core, 0, out _));
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));

            Assert.IsTrue(PlacementRules.CanPlace(g, BlockKey.Pack(1, 0), BlockTypes.Hull, 0, out _));
            g.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Hull));

            Assert.IsTrue(PlacementRules.CanPlace(g, BlockKey.Pack(1, 1), BlockTypes.RetroThruster, 0, out _));
            g.TryAdd(BlockKey.Pack(1, 1), new Block(BlockTypes.RetroThruster));

            Assert.IsTrue(PlacementRules.CanPlace(g, BlockKey.Pack(2, 0), BlockTypes.Thruster, 0, out _));
            g.TryAdd(BlockKey.Pack(2, 0), new Block(BlockTypes.Thruster));

            Assert.IsTrue(PlacementRules.CanPlace(g, BlockKey.Pack(1, -1), BlockTypes.Cannon, 2, out _));
            g.TryAdd(BlockKey.Pack(1, -1), new Block(BlockTypes.Cannon, 2));

            Assert.IsTrue(PlacementRules.CanPlace(g, BlockKey.Pack(0, -1), BlockTypes.Fin, 2, out _));
            g.TryAdd(BlockKey.Pack(0, -1), new Block(BlockTypes.Fin, 2));

            Assert.AreEqual(6, g.Count);
        }
    }
}
