using NUnit.Framework;
using Hullbreach.Core;
using Hullbreach.Builder;

namespace Hullbreach.Builder.Tests
{
    public class BuilderSessionTests
    {
        [Test]
        public void FirstClick_PlacesCoreImmediately()
        {
            var session = new BuilderSession();
            session.Select(BlockTypes.Core);

            Assert.IsTrue(session.Click(BlockKey.Pack(0, 0)));
            Assert.AreEqual(BuilderState.Idle, session.State, "core is symmetric, so it commits on the first click");
            Assert.AreEqual(1, session.BlockCount);
            Assert.IsTrue(session.Grid.Contains(BlockKey.Pack(0, 0)));
        }

        [Test]
        public void Hull_CommitsImmediately_NoSecondClickNeeded()
        {
            var session = new BuilderSession();
            session.Select(BlockTypes.Core);
            session.Click(BlockKey.Pack(0, 0));

            session.Select(BlockTypes.Hull);
            Assert.IsTrue(session.Click(BlockKey.Pack(1, 0)));

            Assert.AreEqual(BuilderState.Idle, session.State);
            Assert.AreEqual(2, session.BlockCount);
        }

        [Test]
        public void Cannon_RequiresSecondClickToOrient()
        {
            var session = new BuilderSession();
            session.Select(BlockTypes.Core);
            session.Click(BlockKey.Pack(0, 0));

            session.Select(BlockTypes.Cannon);
            Assert.IsTrue(session.Click(BlockKey.Pack(1, 0)));
            Assert.AreEqual(BuilderState.Orienting, session.State, "a facing block waits for a facing");
            Assert.AreEqual(1, session.BlockCount, "not yet committed");

            // Hover to the +x side of the pending cell: facing should snap to +x (modifier 1).
            var hover = session.Hover(BlockKey.Pack(2, 0));
            Assert.IsTrue(hover.Valid);

            Assert.IsTrue(session.Click(BlockKey.Pack(2, 0)));
            Assert.AreEqual(BuilderState.Idle, session.State);
            Assert.AreEqual(2, session.BlockCount);

            Assert.IsTrue(session.Grid.TryGet(BlockKey.Pack(1, 0), out var placed));
            Assert.AreEqual((byte)1, placed.Modifiers, "hovering toward +x should orient the facing to +x");
        }

        [Test]
        public void Thruster_CommitsOnFirstClick_NoOrienting()
        {
            var session = new BuilderSession();
            session.Select(BlockTypes.Core);
            session.Click(BlockKey.Pack(0, 0));

            session.Select(BlockTypes.Thruster);
            // Placed beside the core (not behind it), so the thruster's own
            // -y exhaust neighbor (1,-1) is clear.
            Assert.IsTrue(session.Click(BlockKey.Pack(1, 0)));
            Assert.AreEqual(BuilderState.Idle, session.State, "thruster always pushes +y, so it has no facing and commits immediately");
            Assert.AreEqual(2, session.BlockCount);
        }

        [Test]
        public void Cancel_LeavesOrientingWithoutCommitting()
        {
            var session = new BuilderSession();
            session.Select(BlockTypes.Core);
            session.Click(BlockKey.Pack(0, 0));

            session.Select(BlockTypes.Cannon);
            session.Click(BlockKey.Pack(1, 0));
            Assert.AreEqual(BuilderState.Orienting, session.State);

            session.Cancel();
            Assert.AreEqual(BuilderState.Idle, session.State);
            Assert.AreEqual(1, session.BlockCount, "cancel must not place anything");
        }

        [Test]
        public void Remove_ThenUndo_RestoresBlock()
        {
            var session = new BuilderSession();
            session.Select(BlockTypes.Core);
            session.Click(BlockKey.Pack(0, 0));
            session.Select(BlockTypes.Hull);
            session.Click(BlockKey.Pack(1, 0));

            Assert.IsTrue(session.Remove(BlockKey.Pack(1, 0)));
            Assert.AreEqual(1, session.BlockCount);

            Assert.IsTrue(session.Undo());
            Assert.AreEqual(2, session.BlockCount);
            Assert.IsTrue(session.Grid.Contains(BlockKey.Pack(1, 0)));
        }

        [Test]
        public void TotalMassAndBlockCount_TrackThePlacedBlocks()
        {
            var session = new BuilderSession();
            session.Select(BlockTypes.Core);
            session.Click(BlockKey.Pack(0, 0));
            session.Select(BlockTypes.Hull);
            session.Click(BlockKey.Pack(1, 0));

            Assert.AreEqual(2, session.BlockCount);
            Assert.AreEqual(BlockTypes.Get(BlockTypes.Core).Mass + BlockTypes.Get(BlockTypes.Hull).Mass, session.TotalMass, 1e-4f);
        }

        [Test]
        public void ChangedEvent_FiresOnPlacement()
        {
            var session = new BuilderSession();
            int fired = 0;
            session.Changed += () => fired++;

            session.Select(BlockTypes.Core);
            session.Click(BlockKey.Pack(0, 0));

            Assert.AreEqual(1, fired);
        }

        [Test]
        public void Orienting_HoveringAnInvalidFacing_PreviewsInvalidAndRefusesCommit()
        {
            var session = new BuilderSession();
            session.Select(BlockTypes.Core);
            session.Click(BlockKey.Pack(0, 0));
            session.Select(BlockTypes.Hull);
            session.Click(BlockKey.Pack(1, 0));

            session.Select(BlockTypes.Cannon);
            Assert.IsTrue(session.Click(BlockKey.Pack(1, 1)));
            Assert.AreEqual(BuilderState.Orienting, session.State);

            // Hovering south snaps the facing to -y, whose muzzle is the
            // hull at (1, 0): this candidate orientation must preview as
            // invalid, and the commit click must be refused, not silently
            // placed with a bad facing.
            var hover = session.Hover(BlockKey.Pack(1, 0));
            Assert.IsFalse(hover.Valid);
            Assert.AreEqual(PlacementVerdict.BlocksMuzzle, hover.Verdict);

            Assert.IsFalse(session.Click(BlockKey.Pack(1, 0)));
            Assert.AreEqual(BuilderState.Orienting, session.State, "an invalid facing must not commit or cancel the pending placement");
            Assert.AreEqual(2, session.BlockCount, "still just core + hull");
        }

        [Test]
        public void FullSession_PlacesCoreHullThrusterRetroCannonFinSuccessfully()
        {
            var session = new BuilderSession();

            session.Select(BlockTypes.Core);
            Assert.IsTrue(session.Click(BlockKey.Pack(0, 0)));

            session.Select(BlockTypes.Hull);
            Assert.IsTrue(session.Click(BlockKey.Pack(1, 0)));

            session.Select(BlockTypes.RetroThruster);
            Assert.IsTrue(session.Click(BlockKey.Pack(1, 1)));

            session.Select(BlockTypes.Thruster);
            Assert.IsTrue(session.Click(BlockKey.Pack(2, 0)));

            session.Select(BlockTypes.Cannon);
            Assert.IsTrue(session.Click(BlockKey.Pack(1, -1)), "cell chosen");
            Assert.AreEqual(BuilderState.Orienting, session.State);
            session.Hover(BlockKey.Pack(1, -2)); // snap facing to -y
            Assert.IsTrue(session.Click(BlockKey.Pack(1, -2)), "commit facing");

            session.Select(BlockTypes.Fin);
            Assert.IsTrue(session.Click(BlockKey.Pack(0, -1)), "cell chosen");
            Assert.AreEqual(BuilderState.Orienting, session.State);
            session.Hover(BlockKey.Pack(0, -2)); // snap facing to -y, anchored on the core behind it
            Assert.IsTrue(session.Click(BlockKey.Pack(0, -2)), "commit facing");

            Assert.AreEqual(BuilderState.Idle, session.State);
            Assert.AreEqual(6, session.BlockCount);
        }
    }
}
