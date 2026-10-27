using Hullbreach.Platform;
using NUnit.Framework;

namespace Hullbreach.Platform.Tests
{
    // Tab ordering for the sign-in form: forward, backward, wrap, skipping disabled widgets.
    // frob:ticket 01M5XZJRNREX0M9DF1M6PE7G7V
    public sealed class TabOrderTests
    {
        static readonly bool[] AllUsable = { true, true, true, true };

        [Test]
        public void TabMovesForwardThroughTheForm()
        {
            Assert.AreEqual(1, TabOrder.Step(AllUsable, 0, false));
            Assert.AreEqual(2, TabOrder.Step(AllUsable, 1, false));
            Assert.AreEqual(3, TabOrder.Step(AllUsable, 2, false));
        }

        [Test]
        public void ShiftTabMovesBackward()
        {
            Assert.AreEqual(1, TabOrder.Step(AllUsable, 2, true));
            Assert.AreEqual(0, TabOrder.Step(AllUsable, 1, true));
        }

        [Test]
        public void WrapsAtBothEnds()
        {
            Assert.AreEqual(0, TabOrder.Step(AllUsable, 3, false));
            Assert.AreEqual(3, TabOrder.Step(AllUsable, 0, true));
        }

        [Test]
        public void WithoutWrapStopsAtTheEnds()
        {
            Assert.AreEqual(-1, TabOrder.Step(AllUsable, 3, false, wrap: false));
            Assert.AreEqual(-1, TabOrder.Step(AllUsable, 0, true, wrap: false));
            Assert.AreEqual(1, TabOrder.Step(AllUsable, 0, false, wrap: false));
        }

        [Test]
        public void SkipsUnusableEntries()
        {
            bool[] usable = { true, true, false, true };
            Assert.AreEqual(3, TabOrder.Step(usable, 1, false));
            Assert.AreEqual(1, TabOrder.Step(usable, 3, true));
        }

        [Test]
        public void NothingSelectedStartsAtTheEdge()
        {
            Assert.AreEqual(0, TabOrder.Step(AllUsable, -1, false));
            Assert.AreEqual(3, TabOrder.Step(AllUsable, -1, true));
        }

        [Test]
        public void NothingUsableReturnsMinusOne()
        {
            Assert.AreEqual(-1, TabOrder.Step(new[] { false, false }, 0, false));
            Assert.AreEqual(-1, TabOrder.Step(new bool[0], -1, false));
        }

        [Test]
        public void LoneUsableEntryStaysPutWhenWrapping()
        {
            bool[] usable = { false, true, false };
            Assert.AreEqual(1, TabOrder.Step(usable, 1, false));
            Assert.AreEqual(-1, TabOrder.Step(usable, 1, false, wrap: false));
        }

        [Test]
        public void IndexOfFindsTheCurrentItemOrMinusOne()
        {
            string[] order = { "user", "pass", "go" };
            Assert.AreEqual(1, TabOrder.IndexOf(order, order[1]));
            Assert.AreEqual(-1, TabOrder.IndexOf(order, new string('x', 3)));
            Assert.AreEqual(-1, TabOrder.IndexOf(order, null));
        }
    }
}
