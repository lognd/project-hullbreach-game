using System;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.World;

namespace Hullbreach.World.Tests
{
    // Pins the storm schedule (S41): boundaries, shrink interpolation, damage, determinism.
    public class StormZoneTests
    {
        static readonly float2[] Planets = { new float2(0f, -60f), new float2(45f, 20f) };

        static StormZone Make(uint seed = 1u) =>
            new StormZone(StormConfig.Default(), float2.zero, Planets, seed);

        [Test]
        public void DefaultConfig_IsValid_AndEndsBeforeTimeLimit()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/StormZone.cs::Hullbreach.World.StormConfig.Validate kind="unit"
            StormConfig c = StormConfig.Default();
            Assert.IsNull(c.Validate());
            Assert.LessOrEqual(c.TotalSeconds(), 300f);
            Assert.Less(c.TotalSeconds(), 600f, "server MatchTimeLimitSeconds");
            Assert.AreEqual((int)(c.TotalSeconds() * c.TickRate), Make().SuddenDeathTick);
        }

        [Test]
        public void DefaultSchedule_RadiusNeverGrows_AndEveryShrinkIsTelegraphed()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/StormZone.cs::Hullbreach.World.StormZone.At kind="unit"
            StormZone z = Make();
            float last = float.MaxValue;
            bool wasShrinking = false;
            int holdTicks = 0;
            for (int t = 0; t <= z.SuddenDeathTick + 10; t++)
            {
                StormState s = z.At(t);
                Assert.LessOrEqual(s.Radius, last + 1e-4f, "tick " + t);
                last = s.Radius;
                if (s.Stage == StormStage.Shrink && !wasShrinking)
                    Assert.GreaterOrEqual(holdTicks, 20 * 50, "warning before shrink at " + t);
                holdTicks = s.Stage == StormStage.Hold || s.Stage == StormStage.Grace ? holdTicks + 1 : 0;
                // Grace does not count towards a later phase's warning.
                if (s.Stage == StormStage.Grace) holdTicks = 0;
                wasShrinking = s.Stage == StormStage.Shrink;
            }
        }

        [Test]
        public void Grace_HasNoDamage_AndRingStaysFull()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/StormZone.cs::Hullbreach.World.StormState.DamageFor kind="unit"
            StormState s = Make().At(0);
            Assert.AreEqual(StormStage.Grace, s.Stage);
            Assert.AreEqual(100f, s.Radius);
            Assert.AreEqual(0f, s.DamageFor(new float2(500f, 0f)));
            Assert.AreEqual(30 * 50, s.TicksUntilNextStage);
            Assert.AreEqual(55 * 50, s.TicksUntilShrink);
        }

        [Test]
        public void StageBoundaries_AreExact()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/StormZone.cs::Hullbreach.World.StormZone.At kind="unit"
            StormZone z = Make();
            Assert.AreEqual(StormStage.Grace, z.At(1499).Stage);
            Assert.AreEqual(StormStage.Hold, z.At(1500).Stage);
            Assert.AreEqual(0, z.At(1500).PhaseIndex);
            Assert.AreEqual(StormStage.Hold, z.At(2749).Stage);
            Assert.AreEqual(StormStage.Shrink, z.At(2750).Stage);
            Assert.AreEqual(1, z.At(1500 + 55 * 50).PhaseIndex, "phase 1 holds right after phase 0 shrink");
            Assert.AreEqual(StormStage.Hold, z.At(1500 + 55 * 50).Stage);
            Assert.AreEqual(StormStage.Shrink, z.At(z.SuddenDeathTick - 1).Stage);
            Assert.AreEqual(StormStage.SuddenDeath, z.At(z.SuddenDeathTick).Stage);
        }

        [Test]
        public void Shrink_InterpolatesLinearly()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/StormZone.cs::Hullbreach.World.StormZone.At kind="unit"
            StormZone z = Make();
            int start = 2750, len = 30 * 50;
            Assert.AreEqual(100f, z.At(start).Radius, 1e-4f);
            Assert.AreEqual(85f, z.At(start + len / 2).Radius, 1e-3f);
            Assert.AreEqual(70f, z.At(start + len).Radius, 1e-4f);
            StormState mid = z.At(start + len / 2);
            Assert.AreEqual(70f, mid.NextRadius);
            Assert.AreEqual(len / 2, mid.TicksUntilNextStage);
            Assert.AreEqual(0, mid.TicksUntilShrink);
        }

        [Test]
        public void Hold_ReportsNextExtentsAndCountdown()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/StormZone.cs::Hullbreach.World.StormState.TicksUntilShrink kind="unit"
            StormState s = Make().At(1500 + 100);
            Assert.AreEqual(StormStage.Hold, s.Stage);
            Assert.AreEqual(100f, s.Radius);
            Assert.AreEqual(70f, s.NextRadius);
            Assert.AreEqual(s.TicksUntilNextStage, s.TicksUntilShrink);
            Assert.AreEqual(25 * 50 - 100, s.TicksUntilShrink);
        }

        [Test]
        public void Damage_OutsideOnly_EscalatesPerPhase_AndWarningBandIsFree()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/StormZone.cs::Hullbreach.World.StormState.DamageFor kind="unit"
            // frob:tests Assets/Scripts/Hullbreach.World/StormZone.cs::Hullbreach.World.StormState.InWarningBand kind="unit"
            StormZone z = Make();
            StormState p0 = z.At(1600), p1 = z.At(1500 + 55 * 50 + 10);
            float2 inside = new float2(10f, 0f), outside = new float2(150f, 0f), band = new float2(95f, 0f);
            Assert.AreEqual(0f, p0.DamageFor(inside));
            Assert.AreEqual(8f / 50f, p0.DamageFor(outside), 1e-6f);
            Assert.AreEqual(16f / 50f, p1.DamageFor(new float2(500f, 0f)), 1e-6f);
            Assert.IsTrue(p0.InWarningBand(band));
            Assert.AreEqual(0f, p0.DamageFor(band));
            Assert.IsFalse(p0.InWarningBand(inside));
            Assert.IsFalse(p0.InWarningBand(outside));
        }

        [Test]
        public void Core_IsSpared_UntilSuddenDeath()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/StormZone.cs::Hullbreach.World.StormState.DamageFor kind="unit"
            StormZone z = Make();
            float2 far = new float2(400f, 0f);
            Assert.AreEqual(0f, z.At(z.SuddenDeathTick - 1).DamageFor(far, isCore: true));
            StormState sd = z.At(z.SuddenDeathTick);
            Assert.IsTrue(sd.IsSuddenDeath);
            Assert.AreEqual(200f / 50f, sd.DamageFor(far, isCore: true), 1e-6f);
            Assert.AreEqual(200f / 50f, sd.DamageFor(z.FinalCenter, isCore: true), 1e-6f,
                "a closed ring has no safe point");
            Assert.AreEqual(-1, sd.TicksUntilShrink);
            Assert.AreEqual(-1, sd.TicksUntilNextStage);
        }

        [Test]
        public void ZeroRadius_ContainsNothing_AndHasNoPull()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/StormZone.cs::Hullbreach.World.StormState.Contains kind="unit"
            StormZone z = Make();
            StormState sd = z.At(z.SuddenDeathTick + 1000);
            Assert.AreEqual(0f, sd.Radius);
            Assert.IsFalse(sd.Contains(sd.Center));
            Assert.IsFalse(sd.InWarningBand(sd.Center));
            Assert.AreEqual(0f, math.length(sd.PullDirection(sd.Center)));
        }

        [Test]
        public void Queries_SignedDistance_Pull_HardLimit()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/StormZone.cs::Hullbreach.World.StormState.SignedDistance kind="unit"
            // frob:tests Assets/Scripts/Hullbreach.World/StormZone.cs::Hullbreach.World.StormState.PullDirection kind="unit"
            // frob:tests Assets/Scripts/Hullbreach.World/StormZone.cs::Hullbreach.World.StormState.WithinHardLimit kind="unit"
            StormState s = Make().At(0);
            Assert.AreEqual(40f, s.SignedDistance(new float2(60f, 0f)), 1e-4f);
            Assert.AreEqual(-20f, s.SignedDistance(new float2(0f, 120f)), 1e-4f);
            Assert.IsTrue(s.Contains(new float2(100f, 0f)), "edge is inside");
            float2 pull = s.PullDirection(new float2(0f, 50f));
            Assert.AreEqual(-1f, pull.y, 1e-5f);
            Assert.IsTrue(s.WithinHardLimit(new float2(119f, 0f)));
            Assert.IsFalse(s.WithinHardLimit(new float2(121f, 0f)));
            Assert.AreEqual(120f, s.HardLimitRadius);
        }

        [Test]
        public void Determinism_SameSeedSameStorm_AndSeedPicksPlanet()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/StormZone.cs::Hullbreach.World.StormZone.At kind="unit"
            StormZone a = Make(7u), b = Make(7u);
            for (int t = 0; t < a.SuddenDeathTick + 5; t += 37)
            {
                StormState x = a.At(t), y = b.At(t);
                Assert.AreEqual(x.Center, y.Center);
                Assert.AreEqual(x.Radius, y.Radius);
                Assert.AreEqual(x.DamagePerTick, y.DamagePerTick);
            }
            bool sawFirst = false, sawSecond = false;
            for (uint seed = 0; seed < 64; seed++)
            {
                float2 f = Make(seed).FinalCenter;
                Assert.IsTrue(f.Equals(Planets[0]) || f.Equals(Planets[1]));
                if (f.Equals(Planets[0])) sawFirst = true; else sawSecond = true;
            }
            Assert.IsTrue(sawFirst && sawSecond, "seed varies the finale");
        }

        [Test]
        public void Rings_AreNested_AndFinaleLandsNearThePlanet()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/StormZone.cs::Hullbreach.World.StormZone.FinalCenter kind="unit"
            foreach (uint seed in new uint[] { 1u, 2u, 3u, 4u, 5u })
            {
                StormZone z = Make(seed);
                for (int t = 1; t < z.SuddenDeathTick; t += 50)
                {
                    StormState s = z.At(t);
                    // Each ring must sit inside the previous hold ring; check against the arena disc.
                    Assert.LessOrEqual(math.length(s.Center) + s.Radius, 100f + 1e-3f);
                }
                StormState end = z.At(z.SuddenDeathTick);
                Assert.LessOrEqual(math.length(end.Center - z.FinalCenter), 5f);
            }
        }

        [Test]
        public void NegativeTick_ReadsAsStart()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/StormZone.cs::Hullbreach.World.StormZone.At kind="unit"
            Assert.AreEqual(Make().At(0).Radius, Make().At(-5).Radius);
        }

        [Test]
        public void NoCandidates_FinalCenterIsArenaCentre()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/StormZone.cs::Hullbreach.World.StormZone.FinalCenter kind="unit"
            var z = new StormZone(StormConfig.Default(), new float2(3f, 4f), null, 9u);
            Assert.AreEqual(new float2(3f, 4f), z.FinalCenter);
            Assert.AreEqual(new float2(3f, 4f), z.At(z.SuddenDeathTick).Center);
        }

        [Test]
        public void Validate_RejectsBadConfigs_AndCtorThrows()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/StormZone.cs::Hullbreach.World.StormConfig.Validate kind="unit"
            StormPhase ok = new StormPhase(25f, 30f, 50f, 8f);
            Assert.IsNotNull(new StormConfig(0f, 100f, 0f, 0.1f, 0f, 1f, new[] { ok }).Validate());
            Assert.IsNotNull(new StormConfig(50f, 0f, 0f, 0.1f, 0f, 1f, new[] { ok }).Validate());
            Assert.IsNotNull(new StormConfig(50f, 100f, 0f, 0.1f, 0f, 1f, null).Validate());
            Assert.IsNotNull(new StormConfig(50f, 100f, 0f, 1f, 0f, 1f, new[] { ok }).Validate());
            var shortWarn = new StormPhase(5f, 30f, 50f, 8f);
            Assert.IsNotNull(new StormConfig(50f, 100f, 0f, 0.1f, 0f, 1f, new[] { shortWarn }).Validate());
            var grows = new StormPhase(25f, 30f, 150f, 8f);
            Assert.IsNotNull(new StormConfig(50f, 100f, 0f, 0.1f, 0f, 1f, new[] { grows }).Validate());
            Assert.Throws<ArgumentException>(() =>
                new StormZone(new StormConfig(50f, 100f, 0f, 0.1f, 0f, 1f, new[] { grows }), float2.zero, Planets, 1u));
        }

        [Test]
        public void DefaultDamage_EscalatesEveryPhase()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/StormZone.cs::Hullbreach.World.StormConfig.Default kind="unit"
            StormConfig c = StormConfig.Default();
            for (int i = 1; i < c.Phases.Length; i++)
                Assert.Greater(c.Phases[i].DamagePerSecond, c.Phases[i - 1].DamagePerSecond);
            Assert.Greater(c.SuddenDeathDamagePerSecond, c.Phases[c.Phases.Length - 1].DamagePerSecond);
        }
    }
}
