using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    // S48-2: the replica blends its last two server poses so remote ships glide
    // instead of stepping. frob:ticket 01M3DG5Y3DCMV0TQED17NGJF5D
    public class ClientInterpolationTests
    {
        const ushort Id = 7;
        const float Tau = 6.28318530718f;

        static ClientReplica ReplicaWithShip()
        {
            var replica = new ClientReplica();
            var blocks = new[] { new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0) };
            replica.ApplySnapshot(new ShipSnapshot(1, Id, blocks, 0, 0, 0, 0, 0, 0));
            return replica;
        }

        static ShipState StateAt(float x, float rot, uint serverTick)
            => new ShipState(Id, Quantization.PackPosition(x), 0, Quantization.PackAngle(rot), 0, 0, 0, 0, serverTick);

        [Test]
        public void InterpolateAt_BlendsByTick_AndClampsWithoutExtrapolating()
        {
            var replica = ReplicaWithShip();
            replica.ApplyState(StateAt(10f, 0f, 10));
            replica.ApplyState(StateAt(20f, 0f, 12));

            Assert.IsTrue(replica.TryInterpolateAt(Id, 11f, out var mid, out _));
            Assert.AreEqual(15f, mid.x, 0.05f);

            replica.TryInterpolateAt(Id, 9f, out var early, out _);
            Assert.AreEqual(10f, early.x, 0.05f);
            replica.TryInterpolateAt(Id, 99f, out var late, out _);
            Assert.AreEqual(20f, late.x, 0.05f, "never extrapolates past the newest pose");
        }

        [Test]
        public void InterpolateAt_UnknownShip_ReturnsFalse()
        {
            Assert.IsFalse(new ClientReplica().TryInterpolateAt(Id, 1f, out _, out _));
        }

        [Test]
        public void Interpolate_RotationTakesTheShortArcAcrossTheWrap()
        {
            var replica = ReplicaWithShip();
            replica.ApplyState(StateAt(0f, 6.2f, 1));
            replica.ApplyState(StateAt(0f, 0.2f, 2));

            replica.TryInterpolateAt(Id, 1.5f, out _, out float rot);
            float wrapped = math.abs(math.fmod(rot + Tau, Tau));
            float distToZero = math.min(wrapped, Tau - wrapped);
            Assert.Less(distToZero, 0.1f, $"midpoint of 6.2 -> 0.2 should sit near 0, got {rot}");
        }

        [Test]
        public void SameArrivalTick_PosesBlendOverTheirServerTickSpan()
        {
            var replica = ReplicaWithShip();
            Receive(replica, StateAt(10f, 0f, 10));
            Receive(replica, StateAt(20f, 0f, 11));

            Assert.IsTrue(replica.TryInterpolateAt(Id, 10.5f, out var mid, out _));
            Assert.AreEqual(15f, mid.x, 0.05f, "a zero arrival span must not snap to the newer pose");
        }

        // An unreliable pose can arrive after a newer one; it must not become the newest sample.
        // frob:ticket 01M5VPSN6KVQRJ4PEF61GQH13M
        [Test]
        public void OlderServerTickPose_IsDropped_AndInterpolationStaysMonotonic()
        {
            var replica = ReplicaWithShip();
            Receive(replica, StateAt(10f, 0f, 10));
            Receive(replica, StateAt(20f, 0f, 12));
            Receive(replica, StateAt(15f, 0f, 11));

            Assert.AreEqual(12u, replica.LatestServerTick);
            Assert.IsTrue(replica.TryInterpolateAt(Id, 11f, out var mid, out _));
            Assert.AreEqual(15f, mid.x, 0.05f, "the stale pose must not replace the 10 -> 20 pair");
            replica.TryInterpolateAt(Id, 99f, out var late, out _);
            Assert.AreEqual(20f, late.x, 0.05f, "newest pose stays the clamp target");
        }

        // The ghost path shares the guard: a lower tick never becomes the newer sample.
        // frob:ticket 01M5VPSN6KVQRJ4PEF61GQH13M
        [Test]
        public void GhostPose_WithOlderTick_IsDropped()
        {
            var replica = ReplicaWithShip();
            Assert.IsTrue(replica.ApplyGhostPose(Id, new float2(10f, 0f), 0f, float2.zero, 0f, 10));
            Assert.IsTrue(replica.ApplyGhostPose(Id, new float2(20f, 0f), 0f, float2.zero, 0f, 12));
            Assert.IsTrue(replica.ApplyGhostPose(Id, new float2(15f, 0f), 0f, float2.zero, 0f, 11));

            replica.TryInterpolateAt(Id, 11f, out var mid, out _);
            Assert.AreEqual(15f, mid.x, 0.05f);
            replica.TryInterpolateAt(Id, 99f, out var late, out _);
            Assert.AreEqual(20f, late.x, 0.05f);
        }

        static void Receive(ClientReplica replica, ShipState state)
        {
            var buf = new byte[ShipState.ByteSize];
            var w = new ByteWriter(buf);
            state.Write(ref w);
            replica.ApplyReceived(buf, w.Position);
        }

        [Test]
        public void ThrottledLink_InterpolatedViewJumpsLessThanRawSnapping()
        {
            const float tickRate = 50f;
            const int subframes = 4;
            var hub = LoopbackTransport.Throttled(tickRate, 100f, 0.02f, seed: 31);
            var serverTransport = hub.CreateEndpoint(out int serverId);
            var clientTransport = hub.CreateEndpoint(out int peer);
            hub.Connect(serverId, peer);

            var outbox = new ServerOutbox();
            var server = new ServerSimulation(outbox) { TickRate = tickRate };
            var blocks = new[]
            {
                new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0),
                new SnapshotBlock(0, -1, BlockTypes.Thruster, 0, 0),
            };
            server.Join(peer, new ShipSnapshot(0, 0, blocks, 0, 0, 0, 0, 0, 0));

            var replica = new ClientReplica();
            var scratch = new byte[1024];
            var raw = new List<float>();
            var smooth = new List<float>();

            for (uint tick = 1; tick <= 250; tick++)
            {
                var wbuf = new byte[16];
                var w = new ByteWriter(wbuf);
                InputMessage.FromFloats((ushort)peer, tick, 1f, 0f, false).Write(ref w);
                clientTransport.SendUnreliable(serverId, new System.ReadOnlySpan<byte>(wbuf, 0, w.Position));

                while (serverTransport.TryReceive(out int from, scratch, out int inLen))
                {
                    var r = new ByteReader(scratch, 0, inLen);
                    var input = InputMessage.Read(ref r);
                    if (!r.Failed) server.SetInput(from, input);
                }
                server.Tick();
                foreach (var e in outbox.Entries)
                {
                    if (e.Reliable) serverTransport.SendReliable(e.Peer, e.Bytes);
                    else serverTransport.SendUnreliable(e.Peer, e.Bytes);
                }
                outbox.Clear();
                hub.Tick();
                while (clientTransport.TryReceive(out _, scratch, out int len))
                    replica.ApplyReceived(scratch, len);

                if (tick < 60 || !replica.Ships.TryGetValue((ushort)peer, out var ship)) continue;
                for (int f = 0; f < subframes; f++)
                {
                    raw.Add(math.length(ship.Position));
                    float renderTick = replica.LatestServerTick - ClientReplica.InterpolationDelayTicks + f / (float)subframes;
                    Assert.IsTrue(replica.TryInterpolateAt((ushort)peer, renderTick, out var p, out _));
                    smooth.Add(math.length(p));
                }
            }

            Assert.Greater(smooth.Count, 400);
            float rawJump = MaxStep(raw), smoothJump = MaxStep(smooth);
            Assert.Greater(rawJump, 0f, "the ship must be moving");
            Assert.Less(smoothJump, rawJump, $"interpolated max step {smoothJump} vs raw {rawJump}");
        }

        static float MaxStep(List<float> s)
        {
            float worst = 0f;
            for (int i = 1; i < s.Count; i++) worst = math.max(worst, math.abs(s[i] - s[i - 1]));
            return worst;
        }
    }
}
