using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Net;
using Hullbreach.World;

namespace Hullbreach.Net.Tests
{
    // S40 criterion 2 on the authoritative server: shots follow curved paths near planetoids.
    public class ServerProjectileGravityTests
    {
        const int Peer = 1;
        const int TicksAfterFire = 25;

        static ShipSnapshot CannonShip() => new ShipSnapshot(
            0, 0,
            new[]
            {
                new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0),
                new SnapshotBlock(0, 1, BlockTypes.Cannon, 0, 0),
            },
            0, 0, 0, 0, 0, 0);

        // Fires one shot straight along +y and returns where it is TicksAfterFire ticks later.
        static float2 FireAndStep(GravityConfig gravity, out float elapsed)
        {
            var server = new ServerSimulation(new ServerOutbox(), gravity) { TickRate = 50f, TimeoutSeconds = 60f };
            server.Join(Peer, CannonShip());

            for (int i = 0; i <= TicksAfterFire; i++)
            {
                server.SetInput(Peer, InputMessage.FromFloats((ushort)Peer, (uint)i, 0f, 0f, firePressed: i == 0));
                server.Tick();
            }

            Assert.AreEqual(1, server.ProjectilePositions.Count, "the shot should still be in flight");
            // The shot is created on tick 1, then integrates on each later tick.
            elapsed = TicksAfterFire / 50f;
            return server.ProjectilePositions[0];
        }

        [Test]
        public void ShotWithoutPlanetoid_FliesStraight()
        {
            float2 pos = FireAndStep(null, out float elapsed);

            Assert.AreEqual(0.5f, pos.x, 0.01f);
            Assert.Greater(pos.y, 2f + 20f * elapsed * 0.9f);
        }

        [Test]
        public void ShotBesidePlanetoid_BendsTowardIt_ByHalfPullTimesTimeSquared()
        {
            // A planet far out on +x pulls with roughly constant 4 units/s^2.
            const float pull = 4f;
            const float distance = 2000f;
            GravityConfig.TryParse($"planet = {distance} 0 {pull * distance * distance} 5", out var config, out var error);
            Assert.IsNull(error);

            float2 pos = FireAndStep(config, out float elapsed);

            float expectedDeflection = 0.5f * pull * elapsed * elapsed;
            Assert.AreEqual(0.5f + expectedDeflection, pos.x, expectedDeflection * 0.2f);
            Assert.Greater(pos.x, 0.5f + 0.25f, "the path must visibly bend toward the planetoid");
        }
    }
}
