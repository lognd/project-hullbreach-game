using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.World;

namespace Hullbreach.World.Tests
{
    // Covers GravityConfig's defaults, text format and shared field construction.
    public class GravityConfigTests
    {
        [Test]
        public void Default_EqualsTheFormerHardCodedValues()
        {
            Assert.AreEqual(40f, GravityConfig.Default.MaxAcceleration);
            Assert.AreEqual(0.2f, GravityConfig.Default.SurfaceRestitution);
            Assert.AreEqual(0, GravityConfig.Default.Planets.Count);
            Assert.AreEqual(40f, new GravityField().MaxAcceleration);
            Assert.AreEqual(40f, GravityConfig.Default.BuildField().MaxAcceleration);
        }

        [Test]
        public void TryParse_EmptyText_GivesDefaults()
        {
            Assert.IsTrue(GravityConfig.TryParse("  \n# only a comment\n", out var config, out var error), error);
            Assert.AreEqual(GravityConfig.Default.MaxAcceleration, config.MaxAcceleration);
            Assert.AreEqual(GravityConfig.Default.SurfaceRestitution, config.SurfaceRestitution);
        }

        [Test]
        public void TryParse_ReadsConstantsAndPlanets_AndBuildsMatchingField()
        {
            const string text = "max_acceleration = 25\r\nsurface_restitution = 0.5 # bouncy\r\n" +
                                "planet = 0 -60 900 18\r\nplanet = 45 20 120 6 2\r\n";

            Assert.IsTrue(GravityConfig.TryParse(text, out var config, out var error), error);
            var field = config.BuildField();

            Assert.AreEqual(25f, field.MaxAcceleration);
            Assert.AreEqual(2, field.Count);
            Assert.IsTrue(field.TryGetPermanent(0, out var big));
            Assert.AreEqual(new float2(0f, -60f), big.Position);
            Assert.AreEqual(900f, big.Mu);
            Assert.AreEqual(0.5f, big.SurfaceRestitution);
            Assert.AreEqual(18f * GravityBody.DefaultSoftRadiusFactor, big.SoftRadius, 1e-4f);
            Assert.IsTrue(field.TryGetPermanent(1, out var small));
            Assert.AreEqual(12f, small.SoftRadius, 1e-4f);
        }

        [Test]
        public void TryParse_TunedConstant_ChangesTheClamp()
        {
            GravityConfig.TryParse("max_acceleration = 5\nplanet = 0 0 1000 1", out var config, out _);
            var field = config.BuildField();

            float2 accel = field.AccelerationAt(new float2(3f, 0f));

            Assert.AreEqual(5f, math.length(accel), 1e-4f);
        }

        [TestCase("max_acceleration = 0", "line 1")]
        [TestCase("\n\nsurface_restitution = 1.5", "line 3")]
        [TestCase("planet = 1 2 3", "line 1")]
        [TestCase("planet = 1 2 -3 4", "line 1")]
        [TestCase("planet = 1 2 abc 4", "line 1")]
        [TestCase("gravity = 9", "unknown key 'gravity'")]
        [TestCase("just words", "line 1")]
        [TestCase("max_acceleration = NaN", "line 1")]
        public void TryParse_Malformed_FailsWithLineNumberAndNoConfig(string text, string expectedInError)
        {
            Assert.IsFalse(GravityConfig.TryParse(text, out var config, out var error));
            Assert.IsNull(config);
            StringAssert.Contains(expectedInError, error);
        }

        [Test]
        public void TryLoad_MissingFile_FailsWithPath()
        {
            Assert.IsFalse(GravityConfig.TryLoad("/definitely/not/here/gravity.txt", out var config, out var error));
            Assert.IsNull(config);
            StringAssert.Contains("gravity.txt", error);
        }
    }
}
