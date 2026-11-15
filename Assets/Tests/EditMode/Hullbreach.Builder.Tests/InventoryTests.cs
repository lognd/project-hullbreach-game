using Hullbreach.Core;
using NUnit.Framework;

namespace Hullbreach.Builder.Tests
{
    // Pins the stock rules the economy relies on: no negative counts, a shelf cap, no core, and a refund that
    // never rounds up into a free block.
    // frob:ticket 01M7EYG4KH9BT32Q4ERZVBQ3M4
    public class InventoryTests
    {
        [Test]
        public void TakeFromEmpty_Fails_AndCountsStayNonNegative()
        {
            // frob:tests Assets/Scripts/Hullbreach.Builder/Inventory.cs::Hullbreach.Builder.Inventory.TryTake kind="unit"
            var inv = new Inventory();
            Assert.IsFalse(inv.TryTake(BlockTypes.Hull));
            Assert.IsTrue(inv.TryAdd(BlockTypes.Hull, 2));
            Assert.IsTrue(inv.TryTake(BlockTypes.Hull, 2));
            Assert.IsFalse(inv.TryTake(BlockTypes.Hull));
            Assert.AreEqual(0, inv.Get(BlockTypes.Hull));
            Assert.IsFalse(inv.TryTake(BlockTypes.Hull, 0));
            Assert.IsFalse(inv.TryTake(BlockTypes.Hull, -3));
        }

        [Test]
        public void TheCore_IsNeverStock()
        {
            // frob:tests Assets/Scripts/Hullbreach.Builder/Inventory.cs::Hullbreach.Builder.Inventory.TryAdd kind="unit"
            var inv = new Inventory();
            Assert.IsFalse(inv.TryAdd(BlockTypes.Core));
            Assert.IsFalse(inv.CanAdd(BlockTypes.Core));
            Assert.AreEqual(0, inv.AddRefundCredit(BlockTypes.Core, 100));
        }

        [Test]
        public void ShelfCap_RefusesAllOrNothing()
        {
            // frob:tests Assets/Scripts/Hullbreach.Builder/Inventory.cs::Hullbreach.Builder.Inventory.TryAdd kind="unit"
            var inv = new Inventory(3);
            Assert.IsTrue(inv.TryAdd(BlockTypes.Armor, 2));
            Assert.IsFalse(inv.TryAdd(BlockTypes.Armor, 2), "would reach 4");
            Assert.AreEqual(2, inv.Get(BlockTypes.Armor));
            Assert.IsTrue(inv.TryAdd(BlockTypes.Armor, 1));
            Assert.IsFalse(inv.CanAdd(BlockTypes.Armor));
        }

        [Test]
        public void HalfRefunds_NeedTwoDeconstructsPerBlock()
        {
            // frob:tests Assets/Scripts/Hullbreach.Builder/Inventory.cs::Hullbreach.Builder.Inventory.AddRefundCredit kind="unit"
            var inv = new Inventory();
            Assert.AreEqual(0, inv.AddRefundCredit(BlockTypes.Cannon, 50));
            Assert.AreEqual(1, inv.AddRefundCredit(BlockTypes.Cannon, 50));
            int total = 0;
            for (int i = 0; i < 20; i++) total += inv.AddRefundCredit(BlockTypes.Hull, 50);
            Assert.AreEqual(10, total, "20 deconstructs at 50% yield exactly 10 blocks");
        }

        [Test]
        public void Load_ClampsHostileCounts()
        {
            // frob:tests Assets/Scripts/Hullbreach.Builder/Inventory.cs::Hullbreach.Builder.Inventory.Load kind="unit"
            var inv = new Inventory(10);
            inv.Load(new[] { 0, -4, 999 });
            Assert.AreEqual(0, inv.Get(BlockTypes.Hull));
            Assert.LessOrEqual(inv.Get(BlockTypes.Armor), 10);
            Assert.GreaterOrEqual(inv.Get(BlockTypes.Armor), 0);
            Assert.AreEqual(0, inv.Get(BlockTypes.Thruster));
        }
    }

    // frob:ticket 01M7EYG4KH9BT32Q4ERZVBQ3M4
    public class BlockCostsTests
    {
        [Test]
        public void Defaults_AreValid_AndOrdered()
        {
            // frob:tests Assets/Scripts/Hullbreach.Builder/BlockCosts.cs::Hullbreach.Builder.BlockCosts.Validate kind="unit"
            BlockCosts c = BlockCosts.Default();
            Assert.IsNull(c.Validate());
            Assert.AreEqual(0, c.CostOf(BlockTypes.Core));
            Assert.Less(c.CostOf(BlockTypes.Hull), c.CostOf(BlockTypes.Thruster));
            Assert.Less(c.CostOf(BlockTypes.Thruster), c.CostOf(BlockTypes.Cannon));
        }

        [Test]
        public void Welded_CostsExtra_ButNotTheCore()
        {
            // frob:tests Assets/Scripts/Hullbreach.Builder/BlockCosts.cs::Hullbreach.Builder.BlockCosts.CostOf kind="unit"
            BlockCosts c = BlockCosts.Default();
            Assert.AreEqual(c.CostOf(BlockTypes.Armor) + c.WeldedSurcharge, c.CostOf(BlockTypes.Armor, true));
            Assert.AreEqual(0, c.CostOf(BlockTypes.Core, true));
        }

        [Test]
        public void Validate_RejectsBadTables()
        {
            // frob:tests Assets/Scripts/Hullbreach.Builder/BlockCosts.cs::Hullbreach.Builder.BlockCosts.Validate kind="unit"
            Assert.IsNotNull(BlockCosts.Default().WithCost(BlockTypes.Core, 1).Validate());
            Assert.IsNotNull(BlockCosts.Default().WithCost(BlockTypes.Hull, -1).Validate());
            Assert.IsNotNull(BlockCosts.Default().WithLimits(-1, 40, 2).Validate());
            Assert.IsNotNull(BlockCosts.Default().WithLimits(60, 0, 2).Validate());
        }
    }
}
