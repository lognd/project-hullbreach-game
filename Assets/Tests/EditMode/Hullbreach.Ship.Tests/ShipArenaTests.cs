using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Ship;
using Hullbreach.World;

namespace Hullbreach.Ship.Tests
{
    // Covers ShipBody's arena push-back: returns inside, never loads or damages the hull.
    public class ShipArenaTests
    {
        const float Dt = 1f / 50f;

        static ShipBody MakeShip(ArenaBounds arena, float2 position, float2 velocity)
        {
            var ship = new ShipBody { Arena = arena, Position = position, Velocity = velocity };
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core, 0));
            ship.Grid.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Hull, 0));
            ship.Grid.TryAdd(BlockKey.Pack(2, 0), new Block(BlockTypes.Hull, 0));
            ship.RebuildDerivedViews();
            return ship;
        }

        static float2 CenterOfMass(ShipBody ship) => ship.LocalToWorld(ship.Grid.Mass.CenterOfMass);

        [Test]
        public void ShipFlyingOutOfTheArena_IsBackInsideWithinAFewSeconds()
        {
            var arena = new ArenaBounds(float2.zero, radius: 40f);
            var ship = MakeShip(arena, new float2(38f, 0f), new float2(25f, 0f));

            float peakOvershoot = 0f;
            float secondsToReturn = -1f;
            bool leftFirst = false;
            for (int i = 0; i < 50 * 10; i++)
            {
                ship.Step(default, Dt);
                float dist = math.length(CenterOfMass(ship));
                peakOvershoot = math.max(peakOvershoot, dist - arena.Radius);
                if (dist > arena.Radius) leftFirst = true;
                if (leftFirst && dist <= arena.Radius) { secondsToReturn = (i + 1) * Dt; break; }
            }

            Assert.IsTrue(leftFirst, "the ship was supposed to cross the boundary first");
            Assert.Greater(peakOvershoot, 1f, "the push-back is soft: it does not stop the ship at the line");
            Assert.Greater(secondsToReturn, 0f, "never came back inside");
            Assert.Less(secondsToReturn, 5f);
        }

        [Test]
        public void PushBack_AddsNoForceAndNoBlockDamage()
        {
            var arena = new ArenaBounds(float2.zero, radius: 40f);
            var ship = MakeShip(arena, new float2(60f, 0f), new float2(10f, 0f));

            for (int i = 0; i < 50 * 6; i++)
            {
                ship.Step(default, Dt);
                Assert.AreEqual(0, ship.AppliedForcesThisStep.Count, "push-back must not reach the structural solver");
                Assert.AreEqual(float2.zero, ship.LastLinearAcceleration);
            }

            foreach (var kv in ship.Grid.All) Assert.AreEqual(0, kv.Value.Damage);
        }

        [Test]
        public void NullArena_LeavesTheShipDrifting()
        {
            var ship = MakeShip(null, new float2(60f, 0f), new float2(10f, 0f));

            for (int i = 0; i < 50; i++) ship.Step(default, Dt);

            Assert.AreEqual(10f, ship.Velocity.x, 1e-4f);
        }
    }
}
