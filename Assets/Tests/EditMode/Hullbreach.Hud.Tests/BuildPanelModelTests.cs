using Hullbreach.Hud;
using NUnit.Framework;

namespace Hullbreach.Hud.Tests
{
    public sealed class BuildPanelModelTests
    {
        [Test]
        public void BudgetBarDrainsAndWarnsWhenLow()
        {
            var full = BuildPanelModel.Budget(60, 60);
            Assert.AreEqual(1f, full.Fill, 1e-5f);
            Assert.AreEqual("Points 60/60", full.Label);
            Assert.IsFalse(full.Low);

            var low = BuildPanelModel.Budget(10, 60);
            Assert.IsTrue(low.Low);
            Assert.AreEqual(10f / 60f, low.Fill, 1e-5f);
        }

        [Test]
        public void BudgetBarClampsOverspendAndHandlesNoBudget()
        {
            Assert.AreEqual(0f, BuildPanelModel.Budget(-5, 60).Fill);
            Assert.AreEqual(1f, BuildPanelModel.Budget(99, 60).Fill);
            Assert.AreEqual(0f, BuildPanelModel.Budget(5, 0).Fill);
        }

        [Test]
        public void SizeBarWarnsNearTheCap()
        {
            Assert.IsFalse(BuildPanelModel.Size(20, 40).Low);
            Assert.IsTrue(BuildPanelModel.Size(36, 40).Low);
            Assert.AreEqual("Blocks 40/40", BuildPanelModel.Size(50, 40).Label);
        }

        [Test]
        public void SlotShowsStockInMatchAndCostBefore()
        {
            Assert.AreEqual("x3", BuildPanelModel.SlotDetail(true, 3, 4));
            Assert.AreEqual("4 pt", BuildPanelModel.SlotDetail(false, 3, 4));
            Assert.AreEqual("none left", BuildPanelModel.SlotDetail(true, 0, 4), "an empty slot says why it is greyed");
            Assert.IsFalse(BuildPanelModel.SlotUsable(true, 0, 4, 100));
            Assert.IsTrue(BuildPanelModel.SlotUsable(true, 1, 4, 0));
            Assert.IsFalse(BuildPanelModel.SlotUsable(false, 9, 5, 4));
            Assert.IsTrue(BuildPanelModel.SlotUsable(false, 0, 5, 5));
        }

        [Test]
        public void ReadyButtonFollowsThePhase()
        {
            Assert.AreEqual("READY", BuildPanelModel.ReadyLabel(ReadyPhase.Building, true, out bool press));
            Assert.IsTrue(press);
            BuildPanelModel.ReadyLabel(ReadyPhase.Building, false, out press);
            Assert.IsFalse(press);
            BuildPanelModel.ReadyLabel(ReadyPhase.Ready, true, out press);
            Assert.IsFalse(press);
            Assert.AreEqual(string.Empty, BuildPanelModel.ReadyLabel(ReadyPhase.Live, true, out _));
        }

        [Test]
        public void ReadyWarnsAboutUnspentPointsButNeverBlocks()
        {
            Assert.AreEqual("READY (38 pts unspent)", BuildPanelModel.ReadyLabel(ReadyPhase.Building, true, 38, false, out bool press));
            Assert.IsTrue(press, "a warning is not a blocker");
            Assert.AreEqual("READY ANYWAY?", BuildPanelModel.ReadyLabel(ReadyPhase.Building, true, 38, true, out press));
            Assert.IsTrue(press);
            Assert.AreEqual("READY", BuildPanelModel.ReadyLabel(ReadyPhase.Building, true, 4, false, out _), "a few spare points are not worth a prompt");
            Assert.AreEqual("READY", BuildPanelModel.ReadyLabel(ReadyPhase.Building, true, 0, false, out _));
            Assert.AreEqual("READY - waiting for opponent", BuildPanelModel.ReadyLabel(ReadyPhase.Ready, true, 38, false, out _));
            Assert.IsTrue(BuildPanelModel.ShouldConfirmReady(BuildPanelModel.UnspentWarnAt));
            Assert.IsFalse(BuildPanelModel.ShouldConfirmReady(BuildPanelModel.UnspentWarnAt - 1));
        }

        [Test]
        public void ToastsExpireAndDoNotStack()
        {
            var q = new ToastQueue(2f, 2);
            q.Push("a", 0f);
            q.Push("a", 1f);
            Assert.AreEqual("a", q.Text(2.5f)); // extended by the repeat
            Assert.AreEqual(string.Empty, q.Text(3.5f));
            q.Push("a", 10f); q.Push("b", 10f); q.Push("c", 10f);
            Assert.AreEqual("b\nc", q.Text(10.5f)); // capacity 2 drops the oldest
        }
    }
}
