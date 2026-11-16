using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.World;

namespace Hullbreach.Bots.Tests
{
    // The pilot reads the storm ring: it flies back inside when its blocks are, or are about to be, outside the safe
    // radius, reads the ring further ahead the better it is, and presses the attack once the ring has closed.
    // frob:ticket 01M7FJ5MVJTV8AYFMFN3AW942X
    public sealed class StormAwarenessTests
    {
        // Arena at the origin, no planets to drift toward: every ring is centred on (0, 0).
        static readonly StormZone Zone = new StormZone(StormConfig.Default(), float2.zero, null, 1u);

        // First tick of phase 1's shrink (70 -> 45).
        static readonly int Phase1Shrink = FindShrinkStart(1);

        static int FindShrinkStart(int phase)
        {
            for (int t = 0; t < 40000; t++)
            {
                StormState s = Zone.At(t);
                if (s.Stage == StormStage.Shrink && s.PhaseIndex == phase) return t;
            }
            return 0;
        }

        static BotObservation Scene(int tick, float2 at, float2? enemyAt = null, float coreDamage = 0f)
        {
            var obs = new BotObservation { Tick = 0, Dt = 0.02f, HasStorm = true, Storm = Zone.At(tick) };
            obs.Self = new SelfView
            {
                Position = at, Rotation = 0f, Alive = true, MaxForwardAccel = 2f, MaxRetroAccel = 1f, MaxAngularAccel = 0.6f,
                MaxTurnRate = 1.5f, AimLocal = new float2(0f, 1f), HasCannon = true, CannonCooldownSeconds = 0.3f,
                Radius = 3f, CoreDamage = coreDamage,
            };
            if (enemyAt.HasValue)
                obs.Enemy = new EnemyView { Visible = true, Position = enemyAt.Value, CorePosition = enemyAt.Value, Radius = 3f };
            return obs;
        }

        static BotMode FirstMode(BotDifficulty d, BotObservation obs)
        {
            var pilot = new BotPilot(BotProfile.For(d, BotPersonality.Balanced), new BotRng(5));
            pilot.Next(obs);
            return pilot.Mode;
        }

        // frob:tests Assets/Scripts/Hullbreach.Bots/BotPilot.cs::BotPilot.Next kind="unit"
        [Test]
        public void OutsideTheRing_EveryDifficultyHeadsBackToTheSafeZone()
        {
            int tick = Phase1Shrink + 700; // mid-shrink: the ring is well under 70
            foreach (BotDifficulty d in new[] { BotDifficulty.Easy, BotDifficulty.Normal, BotDifficulty.Hard })
            {
                BotObservation obs = Scene(tick, new float2(90f, 0f));
                Assert.IsTrue(obs.Storm.DamagePerTick > 0f && !obs.Storm.Contains(obs.Self.Position));
                Assert.AreEqual(BotMode.ReturnToRing, FirstMode(d, obs), d.ToString());
            }
        }

        // frob:tests Assets/Scripts/Hullbreach.Bots/BotPilot.cs::BotPilot.Next kind="unit"
        [Test]
        public void WellInsideTheRing_NoneOfThemLeavesTheFight()
        {
            int tick = Phase1Shrink - 2000;
            foreach (BotDifficulty d in new[] { BotDifficulty.Easy, BotDifficulty.Normal, BotDifficulty.Hard })
            {
                BotObservation obs = Scene(tick, new float2(10f, 0f), new float2(30f, 0f));
                Assert.AreNotEqual(BotMode.ReturnToRing, FirstMode(d, obs), d.ToString());
            }
        }

        // frob:tests Assets/Scripts/Hullbreach.Bots/BotPilot.cs::BotPilot.Next kind="unit"
        [Test]
        public void ReadsTheComingShrinkFurtherAheadTheBetterItIs()
        {
            int shrink = Phase1Shrink;
            var spot = new float2(55f, 0f); // inside the 70 ring, outside the 45 ring that follows
            // 10 s before the shrink: only Hard looks that far ahead.
            Assert.AreEqual(BotMode.ReturnToRing, FirstMode(BotDifficulty.Hard, Scene(shrink - 500, spot)));
            Assert.AreNotEqual(BotMode.ReturnToRing, FirstMode(BotDifficulty.Normal, Scene(shrink - 500, spot)));
            Assert.AreNotEqual(BotMode.ReturnToRing, FirstMode(BotDifficulty.Easy, Scene(shrink - 500, spot)));
            // 6 s before: Normal has caught on, Easy still has not.
            Assert.AreEqual(BotMode.ReturnToRing, FirstMode(BotDifficulty.Normal, Scene(shrink - 300, spot)));
            Assert.AreNotEqual(BotMode.ReturnToRing, FirstMode(BotDifficulty.Easy, Scene(shrink - 300, spot)));
            // 1 s before: Easy finally moves.
            Assert.AreEqual(BotMode.ReturnToRing, FirstMode(BotDifficulty.Easy, Scene(shrink - 50, spot)));
        }

        // frob:tests Assets/Scripts/Hullbreach.Bots/BotProfile.cs::BotProfile.For kind="unit"
        [Test]
        public void StormSkill_KeepsHardOverNormalOverEasy()
        {
            BotProfile e = BotProfile.For(BotDifficulty.Easy, BotPersonality.Balanced);
            BotProfile n = BotProfile.For(BotDifficulty.Normal, BotPersonality.Balanced);
            BotProfile h = BotProfile.For(BotDifficulty.Hard, BotPersonality.Balanced);
            Assert.Greater(h.StormLeadSeconds, n.StormLeadSeconds);
            Assert.Greater(n.StormLeadSeconds, e.StormLeadSeconds);
            Assert.Greater(h.StormExtentFactor, n.StormExtentFactor);
            Assert.Greater(n.StormExtentFactor, e.StormExtentFactor);
            Assert.Less(h.StormSteerSigma, n.StormSteerSigma);
            Assert.Less(n.StormSteerSigma, e.StormSteerSigma);
        }

        // frob:tests Assets/Scripts/Hullbreach.Bots/BotPilot.cs::BotPilot.Next kind="unit"
        [Test]
        public void AboutToBeCaughtWithTheEnemyInRange_KeepsFighting()
        {
            int shrink = Phase1Shrink;
            // Hard sees the ring coming, but the enemy is 15 blocks away, inside engagement range: no flight.
            BotObservation obs = Scene(shrink - 500, new float2(55f, 0f), new float2(55f, 15f));
            BotMode mode = FirstMode(BotDifficulty.Hard, obs);
            Assert.AreNotEqual(BotMode.ReturnToRing, mode);
            Assert.AreNotEqual(BotMode.Idle, mode);
            // Actually outside, the ring wins over the fight.
            BotObservation outside = Scene(shrink + 900, new float2(80f, 0f), new float2(80f, 15f));
            Assert.AreEqual(BotMode.ReturnToRing, FirstMode(BotDifficulty.Hard, outside));
        }

        // frob:tests Assets/Scripts/Hullbreach.Bots/BotPilot.cs::BotPilot.Next kind="unit"
        [Test]
        public void SuddenDeath_PressesTheAttackInsteadOfRunningOrRetreating()
        {
            int tick = Zone.SuddenDeathTick + 10;
            BotObservation hurt = Scene(tick, new float2(5f, 0f), new float2(20f, 0f), coreDamage: 0.95f);
            Assert.IsTrue(hurt.Storm.IsSuddenDeath);
            Assert.AreEqual(BotMode.Engage, FirstMode(BotDifficulty.Hard, hurt));
            // The same fight with no storm: a badly hurt bot withdraws.
            BotObservation calm = Scene(tick, new float2(5f, 0f), new float2(20f, 0f), coreDamage: 0.95f);
            calm.HasStorm = false;
            Assert.AreEqual(BotMode.Retreat, FirstMode(BotDifficulty.Hard, calm));
        }

        // frob:tests Assets/Scripts/Hullbreach.Bots/ArenaMatch.cs::ArenaMatch.Run kind="unit"
        [Test]
        public void InTheHeadlessArena_AStormAwareBotSpendsLessTimeOutsideTheRingThanABlindOne()
        {
            // Grace 1 s, then two quick rings with real damage; the match is cut before sudden death.
            var storm = new StormConfig(50f, 100f, 1f, 0.1f, 20f, 100f, new[]
            {
                new StormPhase(20f, 15f, 40f, 4f),
                new StormPhase(20f, 15f, 15f, 8f),
            });
            int aware = 0, blind = 0;
            foreach (ulong seed in new ulong[] { 3, 4 })
            {
                ArenaMatchResult r = ArenaMatch.Run(new ArenaMatchConfig
                {
                    Seed = seed, MaxSeconds = 60f, Storm = storm, SameShip = true, SwapSlots = (seed & 1) == 1,
                    A = new ArenaSide { Difficulty = BotDifficulty.Hard },
                    B = new ArenaSide { Difficulty = BotDifficulty.Hard, IgnoreStorm = true },
                });
                Assert.IsNull(r.Error, r.Error);
                aware += r.OutsideTicksA;
                blind += r.OutsideTicksB;
            }
            Assert.Less(aware, blind, "aware " + aware + " ticks outside vs blind " + blind);
        }
    }
}
