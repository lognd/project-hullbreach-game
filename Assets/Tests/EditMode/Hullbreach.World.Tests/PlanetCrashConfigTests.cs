using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.World;

namespace Hullbreach.World.Tests
{
    // The pure rules behind planet crashes and the warning, with no ship in the loop.
    public class PlanetCrashConfigTests
    {
        [Test]
        public void Defaults_AreValid()
        {
            Assert.IsNull(PlanetCrashConfig.Default().Validate());
        }

        [Test]
        public void Validate_RejectsNonsense()
        {
            var c = PlanetCrashConfig.Default();
            c.CatastrophicSpeed = c.SafeLandingSpeed;
            Assert.IsNotNull(c.Validate());
            c = PlanetCrashConfig.Default();
            c.CoreMaxDamagePerImpact = 255f;
            Assert.IsNotNull(c.Validate(), "a core must survive one ordinary hit");
            c = PlanetCrashConfig.Default();
            c.MaxRestitution = 1.5f;
            Assert.IsNotNull(c.Validate());
            c = PlanetCrashConfig.Default();
            c.HeatSpeed = 1f;
            Assert.IsNotNull(c.Validate());
        }

        [Test]
        public void ImpactDamage_IsZeroAtOrBelowTheSafeSpeed_ThenGrowsAndIsSharedByTouchingBlocks()
        {
            var c = PlanetCrashConfig.Default();
            Assert.AreEqual(0f, c.ImpactDamage(c.SafeLandingSpeed, 1, false));
            float one = c.ImpactDamage(10f, 1, false);
            Assert.Greater(one, 0f);
            Assert.Greater(c.ImpactDamage(15f, 1, false), one);
            Assert.Less(c.ImpactDamage(10f, 4, false), one, "a wide landing spreads the hit");
        }

        [Test]
        public void ImpactDamage_CoreIsSparedUntilCatastrophic()
        {
            var c = PlanetCrashConfig.Default();
            float closing = c.CatastrophicSpeed - 0.5f;
            float core = c.ImpactDamage(closing, 1, true);
            Assert.Less(core, c.ImpactDamage(closing, 1, false), "the core takes less than a hull block");
            Assert.LessOrEqual(core, c.CoreMaxDamagePerImpact);
            Assert.AreEqual(255f, c.ImpactDamage(c.CatastrophicSpeed + 20f, 1, true));
        }

        [Test]
        public void AtmosphereDensity_FadesFromSurfaceToTheTopOfTheBand()
        {
            var c = PlanetCrashConfig.Default();
            var body = new GravityBody(float2.zero, 900f, 18f, 0.2f);
            float depth = c.AtmosphereDepth(body);
            Assert.AreEqual(9f, depth, 1e-5f);
            Assert.AreEqual(1f, c.AtmosphereDensity(body, 0f), 1e-5f);
            Assert.AreEqual(0.25f, c.AtmosphereDensity(body, depth * 0.5f), 1e-5f);
            Assert.AreEqual(0f, c.AtmosphereDensity(body, depth), 1e-5f);
            Assert.AreEqual(0f, c.AtmosphereDensity(body, depth + 5f));
            var moon = new GravityBody(float2.zero, 100f, 2f, 0.2f);
            Assert.AreEqual(c.AtmosphereMinDepth, c.AtmosphereDepth(moon), "small bodies get the minimum band");
        }

        [Test]
        public void TimeToCover_MatchesConstantAccelerationKinematics()
        {
            // 20 units at 10 u/s closing with 2 u/s2 pulling: 20 = 10 t + t^2 -> t = 1.7082
            float t = PlanetProximity.TimeToCover(20f, 10f, 2f, out float impact);
            Assert.AreEqual(1.7082f, t, 1e-3f);
            Assert.AreEqual(10f + 2f * t, impact, 1e-3f);
            Assert.AreEqual(2f, PlanetProximity.TimeToCover(20f, 10f, 0f, out _), 1e-4f);
            Assert.AreEqual(0f, PlanetProximity.TimeToCover(0f, 5f, 1f, out _));
            Assert.IsTrue(float.IsPositiveInfinity(PlanetProximity.TimeToCover(10f, 0f, 0f, out _)));
        }

        [Test]
        public void Classify_FollowsTheSafeAndCatastrophicSpeeds()
        {
            var c = PlanetCrashConfig.Default();
            Assert.AreEqual(ImpactSeverity.Safe, PlanetProximity.Classify(c, c.SafeLandingSpeed));
            Assert.AreEqual(ImpactSeverity.Caution, PlanetProximity.Classify(c, c.SafeLandingSpeed + 1f));
            Assert.AreEqual(ImpactSeverity.Danger, PlanetProximity.Classify(c, c.CatastrophicSpeed - 1f));
            Assert.AreEqual(ImpactSeverity.Catastrophic, PlanetProximity.Classify(c, c.CatastrophicSpeed));
        }
    }
}
