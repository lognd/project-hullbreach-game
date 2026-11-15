using System.Collections.Generic;
using NUnit.Framework;
using Hullbreach.World;

namespace Hullbreach.Bots.Tests
{
    // Bot-vs-bot matches through the real ServerSimulation. Short on purpose: the structural solver runs every
    // tick, so a simulated second costs real time. Wider sweeps live in tools/plaincs/bot_report.sh.
    // frob:ticket 01M7EXX75QK41EKPV5392HXCAX
    public sealed class ArenaTests
    {
        const float Seconds = 20f;

        static ArenaMatchResult Play(ulong seed, BotDifficulty a, BotDifficulty b, StormConfig? storm = null, float seconds = Seconds)
            => ArenaMatch.Run(new ArenaMatchConfig
            {
                Seed = seed, MaxSeconds = seconds, Storm = storm,
                A = new ArenaSide { Difficulty = a }, B = new ArenaSide { Difficulty = b },
            });

        // frob:tests Assets/Scripts/Hullbreach.Bots/ArenaMatch.cs::ArenaMatch.Run kind="unit"
        [Test]
        public void Match_RunsWithoutExceptions_AndTerminates()
        {
            foreach (var pair in new[] { (BotDifficulty.Easy, BotDifficulty.Hard), (BotDifficulty.Normal, BotDifficulty.Normal) })
            {
                ArenaMatchResult r = Play(11, pair.Item1, pair.Item2, seconds: 15f);
                Assert.IsNull(r.Error, r.Error);
                Assert.AreNotEqual(ArenaEnding.Crash, r.Ending);
                Assert.AreNotEqual(ArenaEnding.Unfinished, r.Ending, "the match must end by itself");
                Assert.LessOrEqual(r.Seconds, 15.5f);
                Assert.Greater(r.ShotsA + r.ShotsB, 0, "bots should fire");
            }
        }

        // frob:tests Assets/Scripts/Hullbreach.Bots/ArenaMatch.cs::ArenaMatch.Run kind="unit"
        [Test]
        public void Match_IsDeterministicForASeed()
        {
            ArenaMatchResult x = Play(21, BotDifficulty.Hard, BotDifficulty.Normal, seconds: 12f);
            ArenaMatchResult y = Play(21, BotDifficulty.Hard, BotDifficulty.Normal, seconds: 12f);
            Assert.AreEqual(x.Ending, y.Ending);
            Assert.AreEqual(x.ShotsA, y.ShotsA);
            Assert.AreEqual(x.ShotsB, y.ShotsB);
            Assert.AreEqual(x.DamageA, y.DamageA);
            Assert.AreEqual(x.DamageB, y.DamageB);
            Assert.AreEqual(x.BlocksA, y.BlocksA);
            Assert.AreEqual(x.CoreHealthB, y.CoreHealthB);
        }

        // frob:tests Assets/Scripts/Hullbreach.Bots/BalanceReport.cs::BalanceReport.RunBatch kind="unit"
        [Test]
        public void Hard_BeatsEasy_ByAClearMajority()
        {
            ArenaBatch batch = BalanceReport.RunBatch("Hard vs Easy",
                new ArenaSide { Difficulty = BotDifficulty.Hard }, new ArenaSide { Difficulty = BotDifficulty.Easy },
                8, Seconds, null);
            TestContext.Out.WriteLine(BalanceReport.Format(new List<ArenaBatch> { batch }, "Hard vs Easy"));
            Assert.AreEqual(0, batch.Crashes);
            Assert.GreaterOrEqual(batch.WinsA, 5, "Hard won " + batch.WinsA + " of " + batch.Count + " (" + batch.WinsB + " losses)");
            Assert.Greater(batch.WinsA, batch.WinsB * 2);
        }

        // frob:tests Assets/Scripts/Hullbreach.Bots/BalanceReport.cs::BalanceReport.RunBatch kind="unit"
        [Test]
        public void Mirror_IsRoughlyEven_WithSlotsSwapped()
        {
            ArenaBatch batch = BalanceReport.RunBatch("Mirror Normal",
                new ArenaSide { Difficulty = BotDifficulty.Normal }, new ArenaSide { Difficulty = BotDifficulty.Normal },
                8, Seconds, null);
            TestContext.Out.WriteLine(BalanceReport.Format(new List<ArenaBatch> { batch }, "Mirror Normal"));
            Assert.AreEqual(0, batch.Crashes);
            Assert.That(batch.ShareA, Is.InRange(0.15, 0.85), "A share " + batch.ShareA);
        }

        // frob:tests Assets/Scripts/Hullbreach.Bots/ArenaMatch.cs::ArenaMatch.Run kind="unit"
        [Test]
        public void Match_WithStorm_DoesNotCrash()
        {
            ArenaMatchResult r = Play(31, BotDifficulty.Normal, BotDifficulty.Normal, StormConfig.Default(), 30f);
            Assert.IsNull(r.Error, r.Error);
            Assert.AreNotEqual(ArenaEnding.Unfinished, r.Ending);
        }
    }
}
