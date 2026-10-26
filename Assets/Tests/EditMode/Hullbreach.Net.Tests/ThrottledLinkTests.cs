using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    // S48-3: the 100 ms round trip, 2 percent loss profile every netcode
    // smoothing test runs against. frob:ticket 01M3DG5Y3EWDFMPEXFWR9YHWQY
    public class ThrottledLinkTests
    {
        const float TickRate = 50f;
        const float RoundTripMs = 100f;
        const float Loss = 0.02f;

        [Test]
        public void Profile_MapsRoundTripToTicksAndLoss()
        {
            var hub = LoopbackTransport.Throttled(TickRate, RoundTripMs, Loss, seed: 1);
            Assert.AreEqual(2, hub.DelayTicks);
            Assert.AreEqual(1, hub.JitterTicks);
            Assert.AreEqual(Loss, hub.UnreliableDropRate);

            var whole = LoopbackTransport.Throttled(TickRate, 80f, 0f, seed: 1);
            Assert.AreEqual(2, whole.DelayTicks);
            Assert.AreEqual(0, whole.JitterTicks, "an exact tick count needs no jitter");
        }

        [Test]
        public void MeasuredLoss_IsNearTwoPercent_AndReliableIsNeverLost()
        {
            var hub = LoopbackTransport.Throttled(TickRate, RoundTripMs, Loss, seed: 11);
            var a = hub.CreateEndpoint(out int idA);
            var b = hub.CreateEndpoint(out int idB);
            hub.Connect(idA, idB);

            const int sent = 20000;
            var payload = new byte[] { 1 };
            for (int i = 0; i < sent; i++)
            {
                a.SendUnreliable(idB, payload);
                a.SendReliable(idB, payload);
            }
            for (int i = 0; i < 10; i++) hub.Tick();

            int got = 0;
            var buf = new byte[8];
            while (b.TryReceive(out _, buf, out _)) got++;

            int unreliableGot = got - sent;
            Assert.LessOrEqual(unreliableGot, sent);
            float lost = 1f - unreliableGot / (float)sent;
            Assert.That(lost, Is.InRange(0.01f, 0.03f), $"unreliable loss was {lost:P2}");
        }

        [Test]
        public void RoundTrip_TakesAboutFiveTicks()
        {
            var hub = LoopbackTransport.Throttled(TickRate, RoundTripMs, 0f, seed: 5);
            var a = hub.CreateEndpoint(out int idA);
            var b = hub.CreateEndpoint(out int idB);
            hub.Connect(idA, idB);

            var buf = new byte[8];
            int total = 0;
            const int pings = 400;
            for (int n = 0; n < pings; n++)
            {
                a.SendReliable(idB, new byte[] { 1 });
                int ticks = 0;
                bool pong = false;
                bool pinged = false;
                while (!pong)
                {
                    hub.Tick();
                    ticks++;
                    if (!pinged && b.TryReceive(out _, buf, out _))
                    {
                        pinged = true;
                        b.SendReliable(idA, new byte[] { 2 });
                    }
                    else if (pinged && a.TryReceive(out _, buf, out _)) pong = true;
                    Assert.Less(ticks, 20, "a reliable ping must always come back");
                }
                total += ticks;
            }

            float mean = total / (float)pings;
            Assert.That(mean, Is.InRange(4.5f, 6.5f), $"mean round trip was {mean} ticks");
        }

        [Test]
        public void ServerAndClient_StayCoherentOverTheThrottledLink()
        {
            var hub = LoopbackTransport.Throttled(TickRate, RoundTripMs, Loss, seed: 21);
            var serverTransport = hub.CreateEndpoint(out int serverId);
            var clientTransport = hub.CreateEndpoint(out int peer);
            hub.Connect(serverId, peer);

            var outbox = new ServerOutbox();
            var server = new ServerSimulation(outbox) { TickRate = TickRate };
            var blocks = new[]
            {
                new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0),
                new SnapshotBlock(0, 1, BlockTypes.Hull, 0, 0),
                new SnapshotBlock(0, 2, BlockTypes.Hull, 0, 0),
                new SnapshotBlock(0, -1, BlockTypes.Thruster, 0, 0),
            };
            server.Join(peer, new ShipSnapshot(0, 0, blocks, Quantization.PackPosition(0f), 0, 0, 0, 0, 0));
            Assert.IsTrue(server.Ships.TryGetValue(peer, out var serverShip));

            var replica = new ClientReplica();
            var history = new List<float2>();
            var ages = new List<int>();
            var scratch = new byte[1024];

            for (int tick = 0; tick < 300; tick++)
            {
                var input = InputMessage.FromFloats((ushort)peer, (uint)tick, 1f, 0f, false);
                var wbuf = new byte[16];
                var w = new ByteWriter(wbuf);
                input.Write(ref w);
                clientTransport.SendUnreliable(serverId, new System.ReadOnlySpan<byte>(wbuf, 0, w.Position));

                while (serverTransport.TryReceive(out int from, scratch, out _))
                {
                    var r = new ByteReader(scratch);
                    server.SetInput(from, InputMessage.Read(ref r));
                }
                server.Tick();
                history.Add(serverShip.Position);

                foreach (var entry in outbox.Entries)
                {
                    if (entry.Reliable) serverTransport.SendReliable(entry.Peer, entry.Bytes);
                    else serverTransport.SendUnreliable(entry.Peer, entry.Bytes);
                }
                outbox.Clear();
                hub.Tick();

                while (clientTransport.TryReceive(out _, scratch, out int length))
                    replica.ApplyReceived(scratch, length);

                if (replica.Ships.TryGetValue((ushort)peer, out var rep))
                    ages.Add(AgeOfMatchingServerPose(rep.Position, history));
            }

            Assert.IsTrue(replica.Ships.ContainsKey((ushort)peer), "the reliable snapshot must get through");
            Assert.Greater(ages.Count, 250, "the ship must be established within a few ticks");

            int worst = 0;
            long sum = 0;
            foreach (int age in ages) { if (age > worst) worst = age; sum += age; }
            float mean = sum / (float)ages.Count;

            // Half the round trip is 2.5 ticks; a few lost states stretch the tail.
            Assert.That(worst, Is.LessThanOrEqualTo(12), $"worst staleness {worst} ticks");
            Assert.That(mean, Is.LessThanOrEqualTo(5f), $"mean staleness {mean} ticks");

            server.DebugDestroyBlock(peer, 0, 1);
            foreach (var entry in outbox.Entries)
                serverTransport.SendReliable(entry.Peer, entry.Bytes);
            outbox.Clear();
            for (int i = 0; i < 12; i++)
            {
                hub.Tick();
                while (clientTransport.TryReceive(out _, scratch, out int length))
                    replica.ApplyReceived(scratch, length);
            }
            Assert.IsFalse(replica.Ships[(ushort)peer].Grid.Contains(BlockKey.Pack(0, 1)),
                "reliable destruction must converge despite the loss");
            Assert.IsFalse(replica.Ships[(ushort)peer].Grid.Contains(BlockKey.Pack(0, 2)),
                "the stranded tip must follow");
        }

        // Ticks back from the newest server pose to the one the replica shows; 99 if none.
        static int AgeOfMatchingServerPose(float2 pose, List<float2> history)
        {
            const float tol = 4f / Quantization.PositionScale;
            for (int age = 0; age < history.Count; age++)
            {
                if (math.distance(pose, history[history.Count - 1 - age]) <= tol) return age;
                if (age > 40) break;
            }
            return 99;
        }
    }
}
