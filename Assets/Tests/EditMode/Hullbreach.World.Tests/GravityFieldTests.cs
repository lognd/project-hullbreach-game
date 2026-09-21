using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.World;

namespace Hullbreach.World.Tests
{
    /// <summary>Covers GravityField's acceleration summation, softening,
    /// and temporary-body expiry.</summary>
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
        public void AccelerationAt_InsideRadius_IsClampedAtSurface()
        {
            var field = new GravityField();
            field.Add(new GravityBody(float2.zero, mu: 64f, radius: 4f, surfaceRestitution: 0f));

            // Both at the exact surface and halfway to the center, the
            // clamp should read the same magnitude (evaluated at Radius).
            float atSurface = math.length(field.AccelerationAt(new float2(4f, 0f)));
            float insideCore = math.length(field.AccelerationAt(new float2(2f, 0f)));

            Assert.AreEqual(64f / (4f * 4f), atSurface, 1e-4f);
            Assert.AreEqual(atSurface, insideCore, 1e-4f);
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
