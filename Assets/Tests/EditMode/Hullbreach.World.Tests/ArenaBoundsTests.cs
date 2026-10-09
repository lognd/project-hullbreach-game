using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.World;

namespace Hullbreach.World.Tests
{
    // Covers ArenaBounds' spring push-back law.
    public class ArenaBoundsTests
    {
        static ArenaBounds Arena() => new ArenaBounds(new float2(10f, 0f), radius: 20f);

        [Test]
        public void PushBackAcceleration_InsideOrOnEdge_IsZero()
        {
            var arena = Arena();

            Assert.AreEqual(float2.zero, arena.PushBackAcceleration(new float2(10f, 0f), new float2(50f, 0f)));
            Assert.AreEqual(float2.zero, arena.PushBackAcceleration(new float2(30f, 0f), new float2(50f, 0f)));
            Assert.IsTrue(arena.Contains(new float2(30f, 0f)));
        }

        [Test]
        public void PushBackAcceleration_Outside_PointsInwardAndGrowsWithOvershoot()
        {
            var arena = Arena();

            float2 near = arena.PushBackAcceleration(new float2(31f, 0f), float2.zero);
            float2 far = arena.PushBackAcceleration(new float2(33f, 0f), float2.zero);

            Assert.Less(near.x, 0f);
            Assert.AreEqual(0f, near.y, 1e-5f);
            Assert.AreEqual(-arena.Stiffness * 1f, near.x, 1e-4f);
            Assert.Less(far.x, near.x);
        }

        [Test]
        public void PushBackAcceleration_DampsOutwardSpeedOnly()
        {
            var arena = Arena();
            var point = new float2(31f, 0f);

            float2 movingOut = arena.PushBackAcceleration(point, new float2(3f, 0f));
            float2 movingIn = arena.PushBackAcceleration(point, new float2(-3f, 0f));
            float2 still = arena.PushBackAcceleration(point, float2.zero);

            Assert.AreEqual(still.x - arena.Damping * 3f, movingOut.x, 1e-4f);
            Assert.AreEqual(still.x, movingIn.x, 1e-5f);
        }

        [Test]
        public void PushBackAcceleration_IsClampedToMaxAcceleration()
        {
            var arena = Arena();

            float2 accel = arena.PushBackAcceleration(new float2(500f, 0f), new float2(100f, 0f));

            Assert.AreEqual(arena.MaxAcceleration, math.length(accel), 1e-3f);
        }
    }
}
