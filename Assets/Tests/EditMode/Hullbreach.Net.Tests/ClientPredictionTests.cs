using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    // S48-1: the local ship is stepped from its own inputs at once and rewound to
    // the server pose plus unacked inputs. frob:ticket 01M3DG5Y3C3EJ8YSKH8A4XH0S3
    public class ClientPredictionTests
    {
        const ushort Id = 3;

        static SnapshotBlock[] ShipBlocks() => new[]
        {
            new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0),
            new SnapshotBlock(0, -1, BlockTypes.Thruster, 0, 0),
        };

        static ClientReplica LocalReplica()
        {
            var replica = new ClientReplica();
            replica.ApplySnapshot(new ShipSnapshot(1, Id, ShipBlocks(), 0, 0, 0, 0, 0, 0));
            replica.SetLocalShip(Id);
            return replica;
        }

        [Test]
        public void Predict_MovesTheLocalShipBeforeAnyServerReply()
        {
            var replica = LocalReplica();
            replica.TryGetPredicted(out var start, out _);

            for (uint t = 1; t <= 20; t++)
                Assert.IsTrue(replica.Predict(InputMessage.FromFloats(Id, t, 1f, 0f, false)));

            replica.TryGetPredicted(out var now, out _);
            Assert.Greater(math.length(now - start), 0.001f, "thrust must move the predicted ship immediately");
            Assert.AreEqual(20, replica.PendingInputCount);
        }

        [Test]
        public void Predict_WithoutLocalShip_ReturnsFalse()
        {
            var replica = new ClientReplica();
            Assert.IsFalse(replica.Predict(InputMessage.FromFloats(Id, 1, 1f, 0f, false)));
            Assert.IsFalse(replica.TryGetPredicted(out _, out _));
        }

        [Test]
        public void Reconcile_DropsAckedInputs_AndCorrectsWhenPredictionWasWrong()
        {
            var replica = LocalReplica();
            for (uint t = 1; t <= 10; t++)
                replica.Predict(InputMessage.FromFloats(Id, t, 1f, 0f, false));

            // The server saw only the first 4 inputs and says the ship is still at the origin.
            replica.ApplyState(new ShipState(Id, 0, 0, 0, 0, 0, 0, 4));

            Assert.AreEqual(6, replica.PendingInputCount, "inputs up to the ack are dropped");
            Assert.Greater(replica.LastCorrection, 0f, "the over-eager prediction had to be corrected");
        }

        [Test]
        public void Reconcile_WhenServerAgrees_CorrectionIsZero()
        {
            var replica = LocalReplica();
            replica.TryGetPredicted(out var p0, out var r0);
            replica.ApplyState(new ShipState(Id, 0, 0, 0, 0, 0, 0, 0));
            replica.ApplyState(new ShipState(Id, 0, 0, 0, 0, 0, 0, 0));
            replica.TryGetPredicted(out var p1, out var r1);
            Assert.AreEqual(0f, replica.LastCorrection, 1e-5f);
            Assert.AreEqual(p0.x, p1.x, 1e-5f);
            Assert.AreEqual(r0, r1, 1e-5f);
        }

        [Test]
        public void ThrottledLink_PredictionMatchesTheServerPoseForTheSameInput()
        {
            const float tickRate = 50f;
            var hub = LoopbackTransport.Throttled(tickRate, 100f, 0.02f, seed: 17);
            var serverTransport = hub.CreateEndpoint(out int serverId);
            var clientTransport = hub.CreateEndpoint(out int peer);
            hub.Connect(serverId, peer);

            var outbox = new ServerOutbox();
            var server = new ServerSimulation(outbox) { TickRate = tickRate };
            server.Join(peer, new ShipSnapshot(0, 0, ShipBlocks(), 0, 0, 0, 0, 0, 0));

            var replica = new ClientReplica { PredictionTickRate = tickRate, PredictionGravity = server.Gravity };
            replica.SetLocalShip((ushort)peer);
            var scratch = new byte[1024];

            // What the client showed at each input tick vs the pose the server reached once it applied that input.
            var serverPoseByAck = new System.Collections.Generic.Dictionary<uint, float2>();
            var predictedAt = new System.Collections.Generic.Dictionary<uint, float2>();
            var replicaAt = new System.Collections.Generic.Dictionary<uint, float2>();
            for (uint tick = 1; tick <= 300; tick++)
            {
                var input = InputMessage.FromFloats((ushort)peer, tick, 1f, tick % 100 < 50 ? 0.5f : -0.5f, false);
                var wbuf = new byte[16];
                var w = new ByteWriter(wbuf);
                input.Write(ref w);
                clientTransport.SendUnreliable(serverId, new System.ReadOnlySpan<byte>(wbuf, 0, w.Position));
                replica.Predict(input);
                replica.TryGetPredicted(out var predictedNow, out _);
                predictedAt[tick] = predictedNow;
                if (replica.Ships.TryGetValue((ushort)peer, out var shownNow)) replicaAt[tick] = shownNow.Position;

                while (serverTransport.TryReceive(out int from, scratch, out int inLen))
                {
                    var r = new ByteReader(scratch, 0, inLen);
                    var msg = InputMessage.Read(ref r);
                    if (!r.Failed) server.SetInput(from, msg);
                }
                server.Tick();
                foreach (var e in outbox.Entries)
                {
                    if (e.Reliable) serverTransport.SendReliable(e.Peer, e.Bytes);
                    else serverTransport.SendUnreliable(e.Peer, e.Bytes);
                }
                foreach (var e in outbox.Entries)
                {
                    if (e.Reliable || e.Peer != peer) continue;
                    var sr = new ByteReader(e.Bytes, 0, e.Bytes.Length);
                    var st = ShipState.Read(ref sr);
                    if (!sr.Failed && st.AckedInputTick > 0)
                        serverPoseByAck.TryAdd(st.AckedInputTick, new float2(Quantization.UnpackPosition(st.Px), Quantization.UnpackPosition(st.Py)));
                }
                outbox.Clear();
                hub.Tick();
                while (clientTransport.TryReceive(out _, scratch, out int len))
                    replica.ApplyReceived(scratch, len);

            }

            float predictedErr = 0f, replicaErr = 0f;
            int samples = 0;
            foreach (var kv in serverPoseByAck)
            {
                if (kv.Key < 80 || !predictedAt.TryGetValue(kv.Key, out var pa) || !replicaAt.TryGetValue(kv.Key, out var ra)) continue;
                predictedErr += math.length(pa - kv.Value);
                replicaErr += math.length(ra - kv.Value);
                samples++;
            }

            Assert.Greater(samples, 100);
            Assert.LessOrEqual(replica.PendingInputCount, ClientReplica.MaxPendingInputs, "unacked inputs stay bounded");
            Assert.Less(predictedErr, replicaErr * 0.5f,
                $"prediction ({predictedErr / samples}) must be far closer than the lagging replica ({replicaErr / samples})");
        }
    }
}
