using System.Collections.Generic;
using NUnit.Framework;
using Hullbreach.Builder;
using Hullbreach.Core;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    // S34-2: a client's build request is validated by the server with the builder's own
    // PlacementRules, then replicated to every peer as BlockPlaced.
    public class BuildReplicationTests
    {
        static ShipSnapshot Design(SnapshotBlock[] blocks) => new ShipSnapshot(0, 0, blocks, 0, 0, 0, 0, 0, 0);

        // Core at the origin, a hull above it, a thruster below (exhaust cell (0,-2) is reserved).
        static readonly SnapshotBlock[] Rocket =
        {
            new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0),
            new SnapshotBlock(0, 1, BlockTypes.Hull, 0, 0),
            new SnapshotBlock(0, -1, BlockTypes.Thruster, 0, 0),
        };

        static BuildRequest Req(int x, int y, byte type, byte mods = 0) => new BuildRequest((sbyte)x, (sbyte)y, type, mods);

        static byte[] Bytes(BuildRequest r)
        {
            var buf = new byte[BuildRequest.ByteSize];
            var w = new ByteWriter(buf);
            r.Write(ref w);
            return buf;
        }

        [Test]
        public void BuildRequest_RoundTripsAndHasTheDeclaredSize()
        {
            var original = Req(-3, 7, BlockTypes.Cannon, 2);
            var bytes = Bytes(original);
            Assert.AreEqual(BuildRequest.ByteSize, bytes.Length);

            var r = new ByteReader(bytes);
            var back = BuildRequest.Read(ref r);
            Assert.AreEqual(BuildRequest.ByteSize, r.Position, "Read must consume exactly ByteSize bytes");
            Assert.AreEqual(((byte)MessageKind.BuildRequest), bytes[0]);
            Assert.AreEqual(original.X, back.X);
            Assert.AreEqual(original.Y, back.Y);
            Assert.AreEqual(original.TypeId, back.TypeId);
            Assert.AreEqual(original.Mods, back.Mods);
        }

        [Test]
        public void ValidRequest_PlacesTheBlockAndBroadcastsBlockPlacedToEveryPeer()
        {
            var outbox = new ServerOutbox();
            var server = new ServerSimulation(outbox);
            server.Join(1, Design(Rocket));
            server.Join(2, Design(Rocket));
            outbox.Clear();

            Assert.IsTrue(server.TryPlaceBlock(1, Req(1, 0, BlockTypes.Armor), out var refusal, out var verdict));
            Assert.AreEqual(BuildRefusal.None, refusal);
            Assert.AreEqual(PlacementVerdict.Ok, verdict);

            Assert.IsTrue(server.Ships[1].Grid.Contains(BlockKey.Pack(1, 0)), "authoritative grid has the block");
            Assert.IsFalse(server.Ships[2].Grid.Contains(BlockKey.Pack(1, 0)), "the other ship is untouched");

            var placed = new List<(int peer, BlockPlaced msg)>();
            foreach (var e in outbox.Entries)
            {
                Assert.IsTrue(e.Reliable, "BlockPlaced rides the reliable channel");
                var r = new ByteReader(e.Bytes);
                placed.Add((e.Peer, BlockPlaced.Read(ref r)));
            }
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, placed.ConvertAll(p => p.peer), "broadcast to the builder and the opponent");
            foreach (var (_, msg) in placed)
            {
                Assert.AreEqual(1, msg.NetId, "tagged with the building peer's ship");
                Assert.AreEqual(1, msg.X);
                Assert.AreEqual(0, msg.Y);
                Assert.AreEqual(BlockTypes.Armor, msg.TypeId);
            }
        }

        // Each row is something a lying client could send; none may change the grid or emit BlockPlaced.
        [TestCase(0, 1, BlockTypes.Hull, 0, BuildRefusal.PlacementRules, PlacementVerdict.Occupied)]
        [TestCase(5, 5, BlockTypes.Hull, 0, BuildRefusal.PlacementRules, PlacementVerdict.NotAdjacent)]
        [TestCase(1, 0, BlockTypes.Core, 0, BuildRefusal.PlacementRules, PlacementVerdict.CoreAlreadyPlaced)]
        [TestCase(0, -2, BlockTypes.Hull, 0, BuildRefusal.PlacementRules, PlacementVerdict.InsideReservedCell)]
        [TestCase(1, 0, 200, 0, BuildRefusal.UnknownBlockType, PlacementVerdict.Ok)]
        [TestCase(1, 0, BlockTypes.Hull, 0x10, BuildRefusal.ForbiddenModifiers, PlacementVerdict.Ok)]
        [TestCase(1, 0, BlockTypes.Thruster, 0x08, BuildRefusal.ForbiddenModifiers, PlacementVerdict.Ok)]
        public void RefusedRequest_ChangesNothingAndSendsNothing(int x, int y, int type, int mods, BuildRefusal expected, PlacementVerdict expectedVerdict)
        {
            var outbox = new ServerOutbox();
            var server = new ServerSimulation(outbox);
            server.Join(1, Design(Rocket));
            outbox.Clear();
            int before = server.Ships[1].Grid.Count;

            Assert.IsFalse(server.TryPlaceBlock(1, Req(x, y, (byte)type, (byte)mods), out var refusal, out var verdict));

            Assert.AreEqual(expected, refusal);
            Assert.AreEqual(expectedVerdict, verdict);
            Assert.AreEqual(before, server.Ships[1].Grid.Count);
            Assert.AreEqual(0, outbox.Entries.Count, "a refusal must not leak a BlockPlaced");
        }

        [Test]
        public void Request_FromUnknownPeerOrDestroyedShip_IsRefused()
        {
            var outbox = new ServerOutbox();
            var server = new ServerSimulation(outbox);
            server.Join(1, Design(new SnapshotBlock[0]));
            outbox.Clear();

            Assert.IsFalse(server.TryPlaceBlock(99, Req(0, 1, BlockTypes.Hull), out var refusal, out _));
            Assert.AreEqual(BuildRefusal.UnknownPeer, refusal);

            Assert.IsFalse(server.TryPlaceBlock(1, Req(0, 0, BlockTypes.Core), out refusal, out _));
            Assert.AreEqual(BuildRefusal.ShipDestroyed, refusal, "a dead ship cannot re-seed itself with a new core");
            Assert.AreEqual(0, outbox.Entries.Count);
        }

        [Test]
        public void OverTheTransport_TheOpponentsReplicaShowsTheBlock_AndAClientCannotBuildOnAnotherShip()
        {
            var hub = new LoopbackTransport(seed: 11) { DelayTicks = 1, JitterTicks = 2 };
            var serverEp = hub.CreateEndpoint(out int serverId);
            var clientA = hub.CreateEndpoint(out int idA);
            var clientB = hub.CreateEndpoint(out int idB);
            hub.Connect(serverId, idA);
            hub.Connect(serverId, idB);
            var host = new ServerHost(serverEp);
            host.Join(idA, Design(Rocket));
            host.Join(idB, Design(Rocket));
            var replicaA = new ClientReplica();
            var replicaB = new ClientReplica();

            // Peer B sends a request; whatever it targets can only ever be B's own ship.
            clientB.SendReliable(serverId, Bytes(Req(1, 0, BlockTypes.Armor)));
            clientB.SendReliable(serverId, new byte[] { (byte)MessageKind.BuildRequest, 1 }); // truncated: dropped

            var buf = new byte[1024];
            for (int tick = 0; tick < 12; tick++)
            {
                hub.Tick();
                host.Advance(1.0 / 50.0);
                hub.Tick();
                while (clientA.TryReceive(out _, buf, out int la)) replicaA.ApplyReceived(buf, la, (uint)tick);
                while (clientB.TryReceive(out _, buf, out int lb)) replicaB.ApplyReceived(buf, lb, (uint)tick);
            }

            int key = BlockKey.Pack(1, 0);
            Assert.IsTrue(host.Simulation.Ships[idB].Grid.Contains(key), "server placed it on B's ship");
            Assert.IsFalse(host.Simulation.Ships[idA].Grid.Contains(key), "and not on A's");
            Assert.IsTrue(replicaA.Ships[(ushort)idB].Grid.Contains(key), "the opponent sees the new block");
            Assert.IsTrue(replicaB.Ships[(ushort)idB].Grid.Contains(key), "and so does the builder, once the server confirms");
            Assert.IsFalse(replicaA.Ships[(ushort)idA].Grid.Contains(key));
        }
    }
}
