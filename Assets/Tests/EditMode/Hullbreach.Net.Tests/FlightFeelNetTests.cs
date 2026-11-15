using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Ship;

namespace Hullbreach.Net.Tests
{
    // The flight feel rides the same wire and the same ShipBody code online as offline: the assist-off key is an
    // input flag, the server accepts it, and the predicted ship matches the server tick for tick.
    public class FlightFeelNetTests
    {
        static ShipSnapshot Design() => new ShipSnapshot(0, 0, new[]
        {
            new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0),
            new SnapshotBlock(0, 1, BlockTypes.Hull, 0, 0),
            new SnapshotBlock(0, -1, BlockTypes.Hull, 0, 0),
            new SnapshotBlock(-1, -1, BlockTypes.Thruster, 0, 0),
            new SnapshotBlock(1, -1, BlockTypes.Thruster, 0, 0),
            new SnapshotBlock(0, -2, BlockTypes.RetroThruster, 0, 0),
            new SnapshotBlock(-1, 1, BlockTypes.Fin, 3, 0),
            new SnapshotBlock(1, 1, BlockTypes.Fin, 1, 0),
        }, 0, 0, 0, 0, 0, 0);

        [Test]
        public void AssistOffFlag_RoundTripsThroughTheWire_AndIsNotDistrusted()
        {
            var on = InputMessage.FromFloats(1, 5, 0.5f, -0.5f, true, assistOff: true);
            Assert.IsTrue(on.AssistOff);
            Assert.IsTrue(on.FirePressed);
            Assert.IsFalse(InputMessage.FromFloats(1, 5, 0f, 0f, true).AssistOff);
            Assert.AreEqual(0x04, InputMessage.AssistOffBit);

            var bytes = new byte[64];
            var w = new ByteWriter(bytes);
            on.Write(ref w);
            var r = new ByteReader(bytes, 0, w.Position);
            Assert.IsTrue(InputMessage.Read(ref r).AssistOff);

            var server = new ServerSimulation(new ServerOutbox()) { TickRate = 50f, TimeoutSeconds = 1e6f };
            server.Join(1, Design());
            float before = server.TrustOf(1);
            server.SetInput(1, on);
            Assert.AreEqual(before, server.TrustOf(1), "a known flag is not a reserved-bit offence");
        }

        [Test]
        public void ServerAndPrediction_UseTheSameTuning_AndAgreeThroughSteeringBrakingAndAssistOff()
        {
            var server = new ServerSimulation(new ServerOutbox()) { TickRate = 50f, TimeoutSeconds = 1e6f };
            server.Join(1, Design());
            var replica = new ClientReplica();
            Assert.AreEqual(server.FlightTuning.AssistEnabled, replica.PredictionFlightTuning.AssistEnabled);
            Assert.AreEqual(server.FlightTuning.GyroTorquePerMass, replica.PredictionFlightTuning.GyroTorquePerMass);
            Assert.AreEqual(server.FlightTuning.ThrusterRampUpScale, replica.PredictionFlightTuning.ThrusterRampUpScale);
            Assert.AreEqual(server.Ships[1].Tuning.BrakeGain, replica.PredictionFlightTuning.BrakeGain);

            var predicted = new ShipBody { Gravity = server.Gravity, PlanetCrash = server.PlanetCrash, Tuning = replica.PredictionFlightTuning };
            foreach (var kv in server.Ships[1].Grid.All) predicted.Grid.TryAdd(kv.Key, kv.Value);
            predicted.RebuildDerivedViews();

            for (uint t = 1; t <= 300; t++)
            {
                float thrust = t % 100 < 50 ? 1f : (t % 100 < 80 ? -1f : 0f);
                float steer = math.sin(t * 0.07f);
                bool assistOff = t % 90 > 70;
                var msg = InputMessage.FromFloats(1, t, thrust, steer, false, assistOff);
                server.SetInput(1, msg);
                server.Tick();
                predicted.Step(new ShipInput(msg.ThrustAxisFloat, msg.SteerFloat, false, msg.AssistOff), 1f / 50f);
            }
            Assert.AreEqual(server.Ships[1].Position.x, predicted.Position.x, 1e-3f);
            Assert.AreEqual(server.Ships[1].Position.y, predicted.Position.y, 1e-3f);
            Assert.AreEqual(server.Ships[1].Rotation, predicted.Rotation, 1e-3f);
        }
    }
}
