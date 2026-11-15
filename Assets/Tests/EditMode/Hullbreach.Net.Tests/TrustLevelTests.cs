using NUnit.Framework;
using Hullbreach.Core;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    public class TrustLevelTests
    {
        static ShipSnapshot Ship() => new ShipSnapshot(0, 0,
            new[] { new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0) }, 0, 0, 0, 0, 0, 0);

        static ServerSimulation BuildingServer(out ServerOutbox outbox)
        {
            outbox = new ServerOutbox();
            var server = new ServerSimulation(outbox) { TickRate = 50f };
            server.Join(1, Ship());
            server.SetInput(1, new InputMessage(1, 1, 0, 0, InputMessage.BuildModeBit));
            return server;
        }

        [Test]
        public void PenaltiesStopAtZero_AndRecoveryStopsAtOne()
        {
            var t = new TrustLevel();
            for (int i = 0; i < 20; i++) t.Penalize();
            Assert.AreEqual(0f, t.Value);
            Assert.IsTrue(t.AtFloor);
            t.Recover(1000f);
            Assert.AreEqual(1f, t.Value);
            t.Recover(-5f);
            Assert.AreEqual(1f, t.Value, "negative time never lowers trust");
        }

        [Test]
        public void HitMargin_ShrinksWithTrust()
        {
            var tol = new HitTolerance(0.1f, 0.5f);
            Assert.AreEqual(0.2f, tol.MarginFor(2f), 1e-6f);
            Assert.AreEqual(0.1f, tol.MarginFor(2f, 0.5f), 1e-6f);
            Assert.AreEqual(0f, tol.MarginFor(2f, 0f), 1e-6f);
        }

        [Test]
        public void RejectedBuilds_LowerTrust_AndAreLogged_WithoutDisconnecting()
        {
            var server = BuildingServer(out _);
            Assert.AreEqual(1f, server.TrustOf(1));
            Assert.IsFalse(server.TryPlaceBlock(1, 0, 0, BlockTypes.Hull, 0), "cell already holds the core");
            Assert.AreEqual(0.8f, server.TrustOf(1), 1e-5f);
            Assert.IsTrue(server.TryDequeueTrustEvent(out var evt));
            Assert.AreEqual(TrustEventKind.RejectedBuild, evt.Kind);
            Assert.AreEqual(1, evt.Peer);
            Assert.AreEqual(1f, evt.Before, 1e-5f);
            Assert.AreEqual(0.8f, evt.After, 1e-5f);
            Assert.IsFalse(server.TryDequeueTrustEvent(out _));
            Assert.IsFalse(server.TryDequeueDroppedPeer(out _), "trust never drops a peer");
        }

        [Test]
        public void AtFloor_BuildClaimsAreRefused_ThenHealWithTime()
        {
            var server = BuildingServer(out _);
            for (int i = 0; i < 5; i++) server.TryPlaceBlock(1, 0, 0, BlockTypes.Hull, 0);
            Assert.AreEqual(0f, server.TrustOf(1), 1e-5f);
            Assert.IsFalse(server.TryPlaceBlock(1, 0, 1, BlockTypes.Hull, 0), "a legal placement is refused at the floor");
            // 100 ticks at 50 Hz = 2 s = 0.04 trust; keep the input fresh so the peer is not timed out.
            for (uint t = 2; t < 102; t++)
            {
                server.SetInput(1, new InputMessage(1, t, 0, 0, InputMessage.BuildModeBit));
                server.Tick();
            }
            Assert.Greater(server.TrustOf(1), 0f);
            Assert.IsTrue(server.TryPlaceBlock(1, 0, 1, BlockTypes.Hull, 0), "trust recovered");
        }

        [Test]
        public void ReservedFlagBits_LowerTrust_ButStaleTicksDoNot()
        {
            var server = BuildingServer(out _);
            server.SetInput(1, new InputMessage(1, 0, 0, 0, 0)); // older tick: ignored, honest lag
            Assert.AreEqual(1f, server.TrustOf(1));
            server.SetInput(1, new InputMessage(1, 5, 0, 0, 0x80));
            Assert.AreEqual(0.8f, server.TrustOf(1), 1e-5f);
            Assert.IsTrue(server.TryDequeueTrustEvent(out var evt));
            Assert.AreEqual(TrustEventKind.ReservedInputBits, evt.Kind);
        }

        [Test]
        public void BuildOutsideBuildMode_IsDistrusted_AndUnknownPeerIsMinusOne()
        {
            var server = BuildingServer(out _);
            server.SetInput(1, new InputMessage(1, 9, 0, 0, 0));
            Assert.IsFalse(server.TryPlaceBlock(1, 0, 1, BlockTypes.Hull, 0));
            Assert.IsTrue(server.TryDequeueTrustEvent(out var evt));
            Assert.AreEqual(TrustEventKind.BuildOutsideBuildMode, evt.Kind);
            Assert.AreEqual(-1f, server.TrustOf(99));
        }
    }
}
