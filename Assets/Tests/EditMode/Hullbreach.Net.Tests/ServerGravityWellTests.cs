using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    // S43 criteria 1 and 2 on the authoritative server: a gravity-gun shot plants a well where it lands.
    public class ServerGravityWellTests
    {
        const int Shooter = 1;
        const int Target = 2;

        static ShipSnapshot Design(params SnapshotBlock[] blocks) => new ShipSnapshot(0, 0, blocks, 0, 0, 0, 0, 0, 0);

        // Shooter at the origin firing up at a lone hull block at (0, 6); returns the server once the well exists.
        static ServerSimulation FireWellShot(byte variant, ServerOutbox outbox)
        {
            var server = new ServerSimulation(outbox) { TickRate = 50f, TimeoutSeconds = 60f };
            server.Join(Shooter, Design(
                new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0),
                new SnapshotBlock(0, 1, BlockTypes.Cannon, BlockVariants.With(0, variant), 0)));
            server.Join(Target, Design(new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0)));
            server.Ships[Target].Position = new float2(0f, 6f);

            for (int i = 0; i < 100 && server.Gravity.Count == 0; i++)
            {
                server.SetInput(Shooter, InputMessage.FromFloats((ushort)Shooter, (uint)i, 0f, 0f, firePressed: i == 0));
                server.SetInput(Target, InputMessage.FromFloats((ushort)Target, (uint)i, 0f, 0f, false));
                server.Tick();
            }
            return server;
        }

        [Test]
        public void GravityGunShot_PlantsAnAttractiveWellAtImpact_SeenByEveryShip()
        {
            var outbox = new ServerOutbox();
            var server = FireWellShot(variant: 1, outbox);

            Assert.AreEqual(1, server.Gravity.Count, "the impact should plant exactly one well");
            // Far to the +x side of the well the pull points back toward it (-x).
            Assert.Less(server.Gravity.AccelerationAt(new float2(10f, 6f)).x, 0f);
            foreach (var kv in server.Ships) Assert.AreSame(server.Gravity, kv.Value.Gravity, "ships and shots read one field");
            Assert.IsTrue(AnyWellMessage(outbox), "peers must be told about the well");
        }

        [Test]
        public void AntiGravityGunShot_PlantsARepulsiveWellAtImpact()
        {
            var server = FireWellShot(variant: 2, new ServerOutbox());

            Assert.AreEqual(1, server.Gravity.Count);
            Assert.Greater(server.Gravity.AccelerationAt(new float2(10f, 6f)).x, 0f);
        }

        static bool AnyWellMessage(ServerOutbox outbox)
        {
            foreach (var entry in outbox.Entries)
                if (entry.Reliable && entry.Bytes[0] == (byte)MessageKind.GravityWellSpawned) return true;
            return false;
        }
    }
}
