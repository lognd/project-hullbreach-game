using System.Collections.Generic;
using Hullbreach.Core;
using Hullbreach.Net;
using Hullbreach.Match;
using Hullbreach.Platform;
using Hullbreach.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hullbreach.Net.Tests
{
    // A client that joins (or rejoins) a match that is already running must be told what it missed: the storm, every
    // pickup in the field, its own stock and, with ready-up, the match start. Replayed through a real ClientReplica.
    // frob:ticket 01M7FJ5MGNZ0RSXJ5AWKZEE3R4
    public class RejoinSnapshotTests
    {
        static StormConfig ShortStorm() => new StormConfig(50f, 100f, 0f, 0.1f, 20f, 200f, new[]
        {
            new StormPhase(20f, 1f, 0f, 10f),
        });

        static ShipSnapshot Ship() => new ShipSnapshot(0, 0, new[] { new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0) }, 0, 0, 0, 0, 0, 0);

        static (ServerSimulation server, ServerOutbox outbox) RunningMatch(bool requireReady)
        {
            var outbox = new ServerOutbox();
            var server = new ServerSimulation(outbox)
            {
                TickRate = 50f, TimeoutSeconds = 1e6f, Storm = ShortStorm(), MatchFeed = new MatchEndFeed(), RequireReady = requireReady,
            };
            server.Join(1, Ship());
            server.Join(2, Ship());
            if (requireReady)
            {
                server.SetReady(1, true);
                server.SetReady(2, true);
            }
            Assert.IsNotNull(server.Match);
            for (int i = 0; i < 3000 && server.Pickups.Count == 0; i++)
            {
                foreach (var ship in server.Ships.Values) ship.Velocity = float2.zero;
                server.Tick();
            }
            Assert.Greater(server.Pickups.Count, 0, "the field has pickups to snapshot");
            return (server, outbox);
        }

        static ClientReplica Replay(ServerOutbox outbox, int peer)
        {
            var replica = new ClientReplica();
            foreach (OutboxEntry e in outbox.Entries)
                if (e.Peer == peer) replica.ApplyReceived(e.Bytes, e.Bytes.Length);
            return replica;
        }

        [Test]
        public void MidMatchJoiner_ReceivesPickupsStormAndStock()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::Hullbreach.Net.ServerSimulation.Join kind="unit"
            var (server, outbox) = RunningMatch(false);
            outbox.Clear();

            Assert.AreEqual(JoinResult.Joined, server.Join(3, Ship()));
            ClientReplica joiner = Replay(outbox, 3);

            Assert.AreEqual(server.Pickups.Count, joiner.Economy.PickupCount, "every pickup in the field");
            Assert.IsNotNull(joiner.Storm, "storm state");
            Assert.IsTrue(joiner.StormStart.HasValue);
            Assert.AreEqual(server.Economy.Costs.MaxBlocks, joiner.Economy.MaxBlocks, "stock/inventory snapshot arrived");
            Assert.AreEqual(1, joiner.Economy.BlockCount);
            Assert.AreEqual(3, joiner.Ships.Count, "ships of both opponents and itself");
        }

        [Test]
        public void ExistingPeers_StayInOrder_WhenSomeoneJoinsMidMatch()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::Hullbreach.Net.ServerSimulation.Join kind="unit"
            var (server, outbox) = RunningMatch(false);
            ClientReplica existing = Replay(outbox, 1);
            int pickupsBefore = existing.Economy.PickupCount;
            outbox.Clear();
            server.Join(3, Ship());
            foreach (OutboxEntry e in outbox.Entries)
                if (e.Peer == 1) Assert.AreNotEqual(ReplicaApplyResult.Malformed, existing.ApplyReceived(e.Bytes, e.Bytes.Length));
            Assert.AreEqual(pickupsBefore, existing.Economy.PickupCount, "re-sent pickups are idempotent");
        }

        [Test]
        public void ReadyUpMatch_ResendsMatchStartToTheJoiner()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::Hullbreach.Net.ServerSimulation.Join kind="unit"
            var (server, outbox) = RunningMatch(true);
            outbox.Clear();
            server.Join(3, Ship());
            ClientReplica joiner = Replay(outbox, 3);
            Assert.AreEqual((1, 2), joiner.MatchStartedPeers.Value);
        }

        [Test]
        public void JoinBeforeAnyMatch_SendsNoLateState()
        {
            var outbox = new ServerOutbox();
            var server = new ServerSimulation(outbox) { TickRate = 50f, MatchFeed = new MatchEndFeed() };
            server.Join(1, Ship());
            ClientReplica one = Replay(outbox, 1);
            Assert.IsNull(one.Storm);
            Assert.AreEqual(0, one.Economy.PickupCount);
        }
    }
}
