using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    // S48-3: the existing sync, run over the S48 target link (100 ms round trip, 2 percent unreliable loss).
    // frob:doc docs/testing.md#throttled-connection-harness
    public class ThrottledConnectionTests
    {
        static readonly LinkProfile Throttled = LinkProfile.Throttled();

        // Two thrusters under a hull wing: the ship both accelerates and spins, so position and angle both move.
        static readonly SnapshotBlock[] Racer =
        {
            new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0),
            new SnapshotBlock(1, 0, BlockTypes.Hull, 0, 0),
            new SnapshotBlock(0, -1, BlockTypes.Thruster, 0, 0),
            new SnapshotBlock(1, -1, BlockTypes.Thruster, 0, 0),
        };

        // A long spar with the engine at the far end: thrust bends it until blocks take damage and break.
        static readonly SnapshotBlock[] Spar = BuildSpar();

        static SnapshotBlock[] BuildSpar()
        {
            var blocks = new List<SnapshotBlock> { new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0) };
            for (int y = 1; y <= 12; y++) blocks.Add(new SnapshotBlock(0, (sbyte)y, BlockTypes.Hull, 0, 0));
            blocks.Add(new SnapshotBlock(0, 13, BlockTypes.Thruster, 0, 0));
            return blocks.ToArray();
        }

        static ShipSnapshot Design(SnapshotBlock[] blocks) => new ShipSnapshot(0, 0, blocks, 0, 0, 0, 0, 0, 0);

        // Already drifting at about 3 units a second, so one tick of motion spans many position quanta.
        static ShipSnapshot Moving(SnapshotBlock[] blocks)
            => new ShipSnapshot(0, 0, blocks, 0, 0, 0, Quantization.PackPosition(2.5f), Quantization.PackPosition(1.5f), 0);

        [Test]
        public void Profile_IsAHundredMillisecondRoundTrip_WithTwoPercentLoss()
        {
            Assert.AreEqual(100f, Throttled.RoundTripMs(NetRig.TickRate), 1e-3f);
            Assert.AreEqual(0.02f, Throttled.UnreliableDropRate);

            var hub = new LoopbackTransport(seed: 5);
            Throttled.ApplyTo(hub);
            var a = hub.CreateEndpoint(out int idA);
            var b = hub.CreateEndpoint(out int idB);
            hub.Connect(idA, idB);
            var buf = new byte[16];

            // Round trip: reliable ping out, reliable echo back, counted in hub ticks (one tick = 20 ms).
            const int pings = 400;
            long totalTicks = 0;
            for (int i = 0; i < pings; i++)
            {
                a.SendReliable(idB, new byte[] { 1 });
                int ticks = 0;
                bool back = false;
                while (!back && ticks < 50)
                {
                    hub.Tick();
                    ticks++;
                    if (b.TryReceive(out _, buf, out _)) b.SendReliable(idA, new byte[] { 2 });
                    back = a.TryReceive(out _, buf, out _);
                }
                Assert.IsTrue(back, "a reliable ping must always come home");
                totalTicks += ticks;
            }
            double meanRttMs = totalTicks * (1000.0 / NetRig.TickRate) / pings;
            Assert.AreEqual(100.0, meanRttMs, 1000.0 / NetRig.TickRate, "mean RTT within one tick of 100 ms");

            // Loss: only unreliable sends are dropped, at about the configured rate.
            const int sends = 20000;
            for (int i = 0; i < sends; i++) a.SendUnreliable(idB, new byte[] { 3 });
            for (int i = 0; i < 10; i++) hub.Tick();
            int received = 0;
            while (b.TryReceive(out _, buf, out _)) received++;
            double loss = 1.0 - received / (double)sends;
            Assert.That(loss, Is.InRange(0.012, 0.028), $"measured unreliable loss {loss:P2}");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void OwnShipPose_TracksTheServerWithinBoundedLag_AndNeverTeleports(int seed)
        {
            const int lookback = 10;     // ticks; a 3-tick one-way latency plus a few lost or reordered states
            const int warmUp = 12;       // lets the join snapshots land before the course starts
            const int course = 500;      // ten seconds

            var rig = new NetRig(Throttled, seed);
            rig.Host.Join(rig.IdA, Moving(Racer));
            rig.Host.Join(rig.IdB, Moving(Racer));
            rig.Play(warmUp);
            Assert.IsTrue(rig.ReplicaA.Ships.ContainsKey((ushort)rig.IdA), "join snapshot arrived over the throttled link");

            var serverShip = rig.Host.Simulation.Ships[rig.IdA];
            var history = new List<float2>();
            float2 lastReplica = default;
            bool haveLast = false;
            float maxReplicaStep = 0f, maxServerStep = 0f;
            float tol = 4f / Quantization.PositionScale;

            for (int tick = 0; tick < course; tick++)
            {
                int t = tick;
                rig.Step(() =>
                {
                    float steer = (float)Math.Sin(t / 40.0);
                    rig.ClientA.SendUnreliable(rig.ServerId, NetRig.InputBytes((ushort)rig.IdA, rig.ClientTick, 1f, steer));
                    rig.ClientB.SendUnreliable(rig.ServerId, NetRig.InputBytes((ushort)rig.IdB, rig.ClientTick, 1f, -steer));
                });

                history.Add(serverShip.Position);
                if (history.Count > 1)
                    maxServerStep = Math.Max(maxServerStep, math.distance(history[history.Count - 1], history[history.Count - 2]));

                float2 replica = rig.ReplicaA.Ships[(ushort)rig.IdA].Position;
                if (haveLast) maxReplicaStep = Math.Max(maxReplicaStep, math.distance(replica, lastReplica));
                lastReplica = replica;
                haveLast = true;

                bool explained = false;
                for (int i = history.Count - 1; i >= Math.Max(0, history.Count - 1 - lookback); i--)
                    if (math.distance(replica, history[i]) <= tol) { explained = true; break; }
                // Before the first state arrives the replica still sits at the snapshot pose (the origin).
                if (!explained && history.Count > lookback)
                    Assert.Fail($"seed {seed} tick {tick}: replica {replica} matches no server pose in the last {lookback} ticks");
            }

            TestContext.WriteLine($"seed {seed}: max server step {maxServerStep:F4}, max replica step {maxReplicaStep:F4}");
            Assert.Greater(maxServerStep, 2f * tol, "the course must actually move the ship, or the bounds prove nothing");
            // A correction is the replica catching up across lost or late states: a handful of ticks of motion, not a teleport.
            Assert.LessOrEqual(maxReplicaStep, 6f * maxServerStep + tol, "no visible teleport under 2 percent loss");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ReliableEvents_LeaveEveryReplicaGridEqualToTheServers(int seed)
        {
            // spareIds: keep peer ids clear of fragment ids, see FragmentIds_DoNotCollideWithPeerIds.
            var rig = new NetRig(Throttled, seed, spareIds: 32);
            rig.Host.Join(rig.IdA, Design(Spar));
            rig.Host.Join(rig.IdB, Design(Racer));
            rig.Play(12);

            rig.Play(300, tick =>
            {
                rig.ClientA.SendUnreliable(rig.ServerId, NetRig.InputBytes((ushort)rig.IdA, rig.ClientTick, 1f, 0f));
                // B idles but keeps talking, or the server times it out after five seconds of silence.
                rig.ClientB.SendUnreliable(rig.ServerId, NetRig.InputBytes((ushort)rig.IdB, rig.ClientTick, 0f, 0f));
                if (tick == 40)
                {
                    SendBuild(rig.ClientB, rig.ServerId, new BuildRequest(2, 0, BlockTypes.Armor, 0));
                    SendBuild(rig.ClientA, rig.ServerId, new BuildRequest(1, 0, BlockTypes.Hull, 0));
                }
                if (tick == 100) rig.Host.Simulation.DebugDestroyBlock(rig.IdA, 0, 4); // splits the spar
            });
            rig.Play(60, _ => rig.ClientB.SendUnreliable(rig.ServerId, NetRig.InputBytes((ushort)rig.IdB, rig.ClientTick, 0f, 0f)));
            // A sends nothing now: the structure unloads and the reliable tail drains

            AssertGridsEqual(rig.Host.Simulation, rig.ReplicaA, rig.IdA, "A's ship as A sees it");
            AssertGridsEqual(rig.Host.Simulation, rig.ReplicaB, rig.IdA, "A's ship as B sees it");
            AssertGridsEqual(rig.Host.Simulation, rig.ReplicaA, rig.IdB, "B's ship as A sees it");
            AssertGridsEqual(rig.Host.Simulation, rig.ReplicaB, rig.IdB, "B's ship as B sees it");

            var spar = rig.Host.Simulation.Ships[rig.IdA].Grid;
            Assert.IsFalse(spar.Contains(BlockKey.Pack(0, 8)), "the split really happened, so the equality above covers a detach");
            Assert.IsTrue(rig.Host.Simulation.Ships[rig.IdB].Grid.Contains(BlockKey.Pack(2, 0)), "B's build request ran");
            Assert.IsTrue(spar.Contains(BlockKey.Pack(1, 0)), "A's build request ran");
        }

        [Test, Ignore("bug ~FS0APDE: fragment ids start at 1 and collide with peer ids")]
        public void FragmentIds_DoNotCollideWithPeerIds()
        {
            var rig = new NetRig(LinkProfile.Ideal);
            rig.Host.Join(rig.IdA, Design(Spar));
            rig.Play(5);
            rig.Host.Simulation.DebugDestroyBlock(rig.IdA, 0, 10); // first fragment
            rig.Play(5);
            rig.Host.Simulation.DebugDestroyBlock(rig.IdA, 0, 5);  // second fragment: its id equals a peer id
            rig.Play(10);

            AssertGridsEqual(rig.Host.Simulation, rig.ReplicaA, rig.IdA, "A's ship after two detaches");
        }

        [Test, Ignore("bug ~FGNGKV2: ApplySnapshot does not drain events buffered ahead of it")]
        public void EventThatOvertakesItsSnapshot_IsStillApplied()
        {
            var replica = new ClientReplica();
            var placed = new byte[11];
            var w = new ByteWriter(placed);
            new BlockPlaced(4, 7, 1, 0, BlockTypes.Hull, 0).Write(ref w);

            replica.ApplyReliable(4, placed); // arrives first, buffered
            replica.ApplySnapshot(new ShipSnapshot(3, 7, new[] { new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0) }, 0, 0, 0, 0, 0, 0));

            Assert.IsTrue(replica.Ships[7].Grid.Contains(BlockKey.Pack(1, 0)));
        }

        static void SendBuild(ITransport client, int serverId, BuildRequest request)
        {
            var buf = new byte[BuildRequest.ByteSize];
            var w = new ByteWriter(buf);
            request.Write(ref w);
            client.SendReliable(serverId, buf);
        }

        static void AssertGridsEqual(ServerSimulation server, ClientReplica replica, int peer, string who)
        {
            var expected = server.Ships[peer].Grid;
            Assert.IsTrue(replica.Ships.TryGetValue((ushort)peer, out var body), who + ": replica has the ship");
            var actual = body.Grid;
            Assert.AreEqual(expected.Count, actual.Count, who + ": block count");
            foreach (var kv in expected.All)
            {
                Assert.IsTrue(actual.TryGet(kv.Key, out var block), who + ": block " + kv.Key + " present");
                Assert.AreEqual(kv.Value.TypeId, block.TypeId, who + ": type");
                Assert.AreEqual(kv.Value.Modifiers, block.Modifiers, who + ": modifiers");
                Assert.AreEqual(kv.Value.Damage, block.Damage, who + ": damage");
            }
        }
    }
}
