using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Ship;
using Hullbreach.World;

namespace Hullbreach.World.Tests
{
    // Flies a single-block ship on the computed velocity and checks the
    // orbit holds its radius.
    public class OrbitHelperTests
    {
        [Test]
        public void TryCircularOrbitVelocity_HoldsRadius_Within2Percent_Over200Steps()
        {
            var field = new GravityField();
            var body = new GravityBody(float2.zero, mu: 400f, radius: 1f, surfaceRestitution: 0.5f);
            field.Add(body);

            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core, 0));
            ship.RebuildDerivedViews();
            ship.Gravity = field;

            float2 start = new float2(20f, 0f);
            ship.Position = start;
            Assert.IsTrue(OrbitHelper.TryCircularOrbitVelocity(field, 0, start, out var orbitV));
            ship.Velocity = orbitV;

            float startRadius = math.length(start - body.Position);
            float dt = 1f / 60f;
            float maxDeviation = 0f;

            for (int i = 0; i < 200; i++)
            {
                ship.Step(default, dt);
                float radius = math.length(ship.Position - body.Position);
                float deviation = math.abs(radius - startRadius) / startRadius;
                maxDeviation = math.max(maxDeviation, deviation);
            }

            Assert.LessOrEqual(maxDeviation, 0.02f,
                $"orbit radius drifted {maxDeviation:P} from the starting radius over 200 steps");
        }

        [Test]
        public void TryCircularOrbitVelocity_FailsForBadIndexCenterAndRepellers()
        {
            var field = new GravityField();
            field.Add(new GravityBody(float2.zero, mu: 400f, radius: 1f, surfaceRestitution: 0.5f));
            field.Add(new GravityBody(new float2(100f, 0f), mu: -40f, radius: 1f, surfaceRestitution: 0.5f));

            Assert.IsFalse(OrbitHelper.TryCircularOrbitVelocity(field, 7, new float2(20f, 0f), out var v));
            Assert.AreEqual(float2.zero, v);
            Assert.IsFalse(OrbitHelper.TryCircularOrbitVelocity(field, 0, float2.zero, out _));
            Assert.IsFalse(OrbitHelper.TryCircularOrbitVelocity(field, 1, new float2(120f, 0f), out v));
            Assert.AreEqual(float2.zero, v);
            Assert.IsFalse(float.IsNaN(v.x));
        }
    }
}
