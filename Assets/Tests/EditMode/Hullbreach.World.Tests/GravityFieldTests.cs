using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.World;

namespace Hullbreach.World.Tests
{
    // Covers GravityField's acceleration summation, softening,
    // and temporary-body expiry.
    public class GravityFieldTests
    {
        [Test]
        public void AccelerationAt_PointsTowardBody_WithInverseSquareMagnitude()
        {
            var field = new GravityField();
            field.Add(new GravityBody(new float2(10f, 0f), mu: 100f, radius: 1f, surfaceRestitution: 0f));

            float2 accel = field.AccelerationAt(float2.zero);

            // Points toward the body: from origin, that is +x.
            Assert.Greater(accel.x, 0f);
            Assert.AreEqual(0f, accel.y, 1e-4f);
            Assert.AreEqual(100f / (10f * 10f), math.length(accel), 1e-4f);
        }

        [Test]
        public void AccelerationAt_InsideSoftRadius_IsZeroAtCenter()
        {
            // radius 4 -> SoftRadius = max(4 * 1.5, 2) = 6.
            var field = new GravityField();
            field.Add(new GravityBody(float2.zero, mu: 64f, radius: 4f, surfaceRestitution: 0f));

            float atCenter = math.length(field.AccelerationAt(float2.zero));

            Assert.AreEqual(0f, atCenter, 1e-5f);
        }

        [Test]
        public void AccelerationAt_IsContinuous_AtSoftRadius()
        {
            var body = new GravityBody(float2.zero, mu: 64f, radius: 4f, surfaceRestitution: 0f);
            var field = new GravityField();
            field.Add(body);

            float softRadius = body.SoftRadius; // 6
            float justOutside = math.length(field.AccelerationAt(new float2(softRadius + 0.01f, 0f)));
            float justInside = math.length(field.AccelerationAt(new float2(softRadius - 0.01f, 0f)));

            float relativeDifference = math.abs(justOutside - justInside) / justOutside;
            Assert.Less(relativeDifference, 0.01f,
                $"acceleration jumped {relativeDifference:P} across SoftRadius");
        }

        [Test]
        public void AccelerationAt_InsideSoftRadius_IncreasesMonotonically_FromCenter()
        {
            var body = new GravityBody(float2.zero, mu: 64f, radius: 4f, surfaceRestitution: 0f);
            var field = new GravityField();
            field.Add(body);

            float previous = 0f;
            for (float dist = 0.5f; dist < body.SoftRadius; dist += 0.5f)
            {
                float current = math.length(field.AccelerationAt(new float2(dist, 0f)));
                Assert.Greater(current, previous, $"not monotonic at dist={dist}");
                previous = current;
            }
        }

        [Test]
        public void AccelerationAt_OutsideSoftRadius_MatchesInverseSquare()
        {
            var field = new GravityField();
            field.Add(new GravityBody(float2.zero, mu: 64f, radius: 4f, surfaceRestitution: 0f));

            float dist = 20f; // well outside SoftRadius (6)
            float accel = math.length(field.AccelerationAt(new float2(dist, 0f)));

            Assert.AreEqual(64f / (dist * dist), accel, 1e-4f);
        }

        [Test]
        public void SoftRadius_DefaultsToRadiusFactor_WhenAboveFloor()
        {
            var body = new GravityBody(float2.zero, mu: 1f, radius: 10f, surfaceRestitution: 0f);
            Assert.AreEqual(15f, body.SoftRadius, 1e-4f); // 10 * 1.5
        }

        [Test]
        public void SoftRadius_IsFlooredAtMinimum_ForTinyBodies()
        {
            // A temporary gravity-gun well with a small physical radius.
            var body = new GravityBody(float2.zero, mu: 1f, radius: 0.5f, surfaceRestitution: 0f);
            Assert.GreaterOrEqual(body.SoftRadius, GravityBody.MinSoftRadius);
            Assert.AreEqual(GravityBody.MinSoftRadius, body.SoftRadius, 1e-4f);
        }

        [Test]
        public void AccelerationAt_ClampsSummedMagnitude_ToMaxAcceleration()
        {
            var field = new GravityField();
            // Overlapping wells pulling roughly the same direction: sum
            // would otherwise exceed the cap.
            field.Add(new GravityBody(new float2(1f, 0f), mu: 1000f, radius: 0.5f, surfaceRestitution: 0f));
            field.Add(new GravityBody(new float2(-1f, 3f), mu: 1000f, radius: 0.5f, surfaceRestitution: 0f));
            field.Add(new GravityBody(new float2(-1f, -3f), mu: 1000f, radius: 0.5f, surfaceRestitution: 0f));

            float2 accel = field.AccelerationAt(float2.zero);

            Assert.LessOrEqual(math.length(accel), field.MaxAcceleration + 1e-4f);
            Assert.AreEqual(field.MaxAcceleration, math.length(accel), 1e-3f);
        }

        [Test]
        public void AccelerationAt_SumsOverMultipleBodies()
        {
            var field = new GravityField();
            field.Add(new GravityBody(new float2(10f, 0f), mu: 100f, radius: 1f, surfaceRestitution: 0f));
            field.Add(new GravityBody(new float2(-10f, 0f), mu: 100f, radius: 1f, surfaceRestitution: 0f));

            // Equal and opposite pulls at the midpoint cancel.
            float2 accel = field.AccelerationAt(float2.zero);
            Assert.AreEqual(0f, math.length(accel), 1e-4f);
        }

        [Test]
        public void TemporaryBody_Expires_AfterItsLifetime()
        {
            var field = new GravityField();
            field.AddTemporary(new GravityBody(new float2(10f, 0f), mu: 100f, radius: 1f, surfaceRestitution: 0f), seconds: 1f);

            Assert.Greater(math.length(field.AccelerationAt(float2.zero)), 0f);

            field.Tick(0.5f);
            Assert.Greater(math.length(field.AccelerationAt(float2.zero)), 0f);

            field.Tick(0.6f); // total 1.1s > 1s lifetime
            Assert.AreEqual(0f, math.length(field.AccelerationAt(float2.zero)), 1e-6f);
        }
    }
}
