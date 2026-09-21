using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    /// <summary>
    /// The full pipeline over LoopbackTransport: a ServerSimulation with two
    /// ships, and one ClientReplica per ship, run for 200 ticks with
    /// scripted input (one ship thrusting, one firing). Exercises every
    /// deliverable together: message encoding, transport delivery with
    /// simulated jitter/reorder, server authority, and client-side
    /// derivation of detachment from ordered destruction events alone.
    ///
    /// Peer ids are the ids LoopbackTransport itself assigns each endpoint
    /// (there is no separate "player id" concept in this design): whatever
    /// id the hub hands the server for a given client's messages IS the peer
    /// key ServerSimulation.Join/SetInput/Leave use.
    /// </summary>
    public class ServerClientEndToEndTests
    {
        const int DelayTicks = 1;
        const int JitterTicks = 3;

        // Because ShipState is unreliable/unordered by design (see
        // ITransport's contract), a replica's applied pose after any given
        // Tick is whatever the most recently ARRIVED message says, not
        // necessarily the most recently SENT one: a jittery transport can
        // let an older update overtake a newer one. So "matches the server
        // within quantization error" is checked against the whole recent
        // window of server poses the transport could plausibly still be
        // delivering, not against the single latest server pose.
        const int Lookback = DelayTicks + JitterTicks + 2;

        static ShipSnapshot Design(SnapshotBlock[] blocks) => new ShipSnapshot(0, 0, blocks, 0, 0, 0, 0, 0, 0);

        [Test]
        public void Poses_Events_Ordering_Timeout_And_Bandwidth()
        {
            var hub = new LoopbackTransport(seed: 7) { DelayTicks = DelayTicks, JitterTicks = JitterTicks };
            var serverTransport = hub.CreateEndpoint(out int serverId);
            var client1Transport = hub.CreateEndpoint(out int peerThruster);
            var client2Transport = hub.CreateEndpoint(out int peerCannon);
            hub.Connect(serverId, peerThruster);
            hub.Connect(serverId, peerCannon);

            var outbox = new ServerOutbox();
            var server = new ServerSimulation(outbox) { TickRate = 50f, TimeoutSeconds = 5f };

            var shipA = new[]
            {
                new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0),
                new SnapshotBlock(0, 1, BlockTypes.Hull, 0, 0),
                new SnapshotBlock(0, 2, BlockTypes.Hull, 0, 0),
                new SnapshotBlock(0, -1, BlockTypes.Thruster, 0, 0),
            };
            var shipB = new[]
            {
                new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0),
                new SnapshotBlock(0, 1, BlockTypes.Cannon, 0, 0),
            };

            server.Join(peerThruster, Design(shipA));
            server.Join(peerCannon, Design(shipB));

            Assert.IsTrue(server.Ships.TryGetValue(peerThruster, out var serverShipA));
            Assert.IsTrue(server.Ships.TryGetValue(peerCannon, out var serverShipB));

            var replica1 = new ClientReplica();
            var replica2 = new ClientReplica();

            int measuredTick = -1;
            int unreliableBytesAtMeasuredTick = 0;

            var historyA = new List<float2>();
            var historyB = new List<float2>();

            for (int tick = 0; tick < 200; tick++)
            {
                uint clientTick = (uint)tick;

                SendInput(client1Transport, serverId, InputMessage.FromFloats((ushort)peerThruster, clientTick, 1f, 0f, false));
                if (tick < 150) // stop peer 2's input after tick 150 to exercise the timeout later
                    SendInput(client2Transport, serverId, InputMessage.FromFloats((ushort)peerCannon, clientTick, 0f, 0f, true));

                PumpServerInbound(serverTransport, server);
                server.Tick();

                historyA.Add(serverShipA.Position);
                historyB.Add(serverShipB.Position);

                int bytesThisTick = 0;
                foreach (var entry in outbox.Entries)
                {
                    if (!entry.Reliable) bytesThisTick += entry.Bytes.Length;
                    if (entry.Reliable) serverTransport.SendReliable(entry.Peer, entry.Bytes);
                    else serverTransport.SendUnreliable(entry.Peer, entry.Bytes);
                }
                outbox.Clear();

                if (tick == 50)
                {
                    measuredTick = tick;
                    unreliableBytesAtMeasuredTick = bytesThisTick;
                }

                hub.Tick();

                PumpClientInbound(client1Transport, replica1, clientTick);
                PumpClientInbound(client2Transport, replica2, clientTick);

                // "Replica poses match server poses within quantization
                // error after every state message": once a snapshot has
                // established the ship, its pose must always be explained
                // by SOME server pose from the recent lag window.
                if (replica1.Ships.TryGetValue((ushort)peerThruster, out var repA))
                    AssertPoseExplainedByRecentHistory(repA.Position, historyA, tick);
                if (replica2.Ships.TryGetValue((ushort)peerCannon, out var repB))
                    AssertPoseExplainedByRecentHistory(repB.Position, historyB, tick);
            }

            // --- Bandwidth: two ships, unreliable traffic per tick under 200 bytes. ---
            Assert.Greater(measuredTick, -1, "never measured a mid-run tick");
            Assert.Less(unreliableBytesAtMeasuredTick, 200,
                $"unreliable traffic for two ships must stay under 200 B/tick, was {unreliableBytesAtMeasuredTick}");

            // --- Poses: both replicas did establish their ship. ---
            Assert.IsTrue(replica1.Ships.ContainsKey((ushort)peerThruster));
            Assert.IsTrue(replica2.Ships.ContainsKey((ushort)peerCannon));

            // --- Forced destruction: both sides derive the same detach. ---
            server.DebugDestroyBlock(peerThruster, 0, 1); // the bridge block between core and the tip hull block
            foreach (var entry in outbox.Entries)
            {
                if (entry.Reliable) serverTransport.SendReliable(entry.Peer, entry.Bytes);
                else serverTransport.SendUnreliable(entry.Peer, entry.Bytes);
            }
            outbox.Clear();

            for (int drain = 0; drain < 8; drain++) // let jittered reliable delivery finish
            {
                hub.Tick();
                PumpClientInbound(client1Transport, replica1, 0);
                PumpClientInbound(client2Transport, replica2, 0);
            }

            Assert.IsFalse(serverShipA.Grid.Contains(BlockKey.Pack(0, 2)),
                "server: the stranded tip block must be gone after FindDetached");
            var replicaA = replica1.Ships[(ushort)peerThruster];
            Assert.IsFalse(replicaA.Grid.Contains(BlockKey.Pack(0, 2)),
                "client: FindDetached must strand the same block independently");
            Assert.IsFalse(replicaA.Grid.Contains(BlockKey.Pack(0, 1)),
                "the destroyed bridge block itself must be gone on the client too");
            Assert.IsTrue(replicaA.Grid.Contains(BlockKey.Pack(0, 0)), "the core must survive");

            // --- Timeout: peer stopped sending input at tick 150; TimeoutSeconds=5 at 50Hz is 250 ticks. ---
            for (int i = 0; i < 260; i++)
            {
                server.Tick();
                outbox.Clear();
            }
            Assert.IsFalse(server.Ships.ContainsKey(peerCannon), "a peer that stops sending input must be dropped after the timeout");
        }

        static void AssertPoseExplainedByRecentHistory(float2 replicaPosition, List<float2> history, int tick)
        {
            const float tol = 4f / Quantization.PositionScale; // a few quantization steps of slack
            int start = System.Math.Max(0, tick - Lookback);
            for (int i = tick; i >= start; i--)
            {
                if (math.distance(replicaPosition, history[i]) <= tol) return;
            }
            Assert.Fail($"replica pose {replicaPosition} at tick {tick} matches no server pose in the last {Lookback} ticks");
        }

        static void SendInput(ITransport transport, int serverPeer, InputMessage input)
        {
            var buf = new byte[16];
            var w = new ByteWriter(buf);
            input.Write(ref w);
            transport.SendUnreliable(serverPeer, new System.ReadOnlySpan<byte>(buf, 0, w.Position));
        }

        static void PumpServerInbound(ITransport serverTransport, ServerSimulation server)
        {
            var scratch = new byte[512];
            while (serverTransport.TryReceive(out int from, scratch, out int length))
            {
                var r = new ByteReader(scratch);
                var input = InputMessage.Read(ref r);
                server.SetInput(from, input);
            }
        }

        static void PumpClientInbound(ITransport clientTransport, ClientReplica replica, uint clientTick)
        {
            var buf = new byte[1024];
            while (clientTransport.TryReceive(out _, buf, out int length))
            {
                replica.ApplyReceived(buf, length, clientTick);
            }
        }
    }
}
