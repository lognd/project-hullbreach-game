using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Builder;
using Hullbreach.Core;
using Hullbreach.Net;

namespace Hullbreach.Bots.Tests
{
    // The cheap checks: randomness, profiles and generated ships. The arena runs are in ArenaTests.
    // frob:ticket 01M7EXX75QK41EKPV5392HXCAX
    public sealed class BotTests
    {
        static readonly BotDifficulty[] Levels = { BotDifficulty.Easy, BotDifficulty.Normal, BotDifficulty.Hard };

        // frob:tests Assets/Scripts/Hullbreach.Bots/BotRng.cs::BotRng.NextULong kind="unit"
        [Test]
        public void Rng_SameSeedSameSequence_DifferentSeedDiffers()
        {
            var a = new BotRng(42); var b = new BotRng(42); var c = new BotRng(43);
            bool differs = false;
            for (int i = 0; i < 64; i++)
            {
                ulong x = a.NextULong();
                Assert.AreEqual(x, b.NextULong());
                if (x != c.NextULong()) differs = true;
            }
            Assert.IsTrue(differs);
        }

        // frob:tests Assets/Scripts/Hullbreach.Bots/BotRng.cs::BotRng.Range kind="unit"
        [Test]
        public void Rng_RangeStaysInBounds_AndGaussianIsCentred()
        {
            var r = new BotRng(7);
            double sum = 0;
            for (int i = 0; i < 4000; i++)
            {
                float f = r.Range(2f, 5f);
                Assert.That(f, Is.InRange(2f, 5f));
                int n = r.Range(3, 9);
                Assert.That(n, Is.InRange(3, 8));
                sum += r.Gaussian();
            }
            Assert.That(sum / 4000.0, Is.InRange(-0.1, 0.1));
        }

        // Never superhuman: nobody reacts in under a sixth of a second or aims without a little error.
        // frob:tests Assets/Scripts/Hullbreach.Bots/BotProfile.cs::BotProfile.For kind="unit"
        [Test]
        public void Profiles_AreHumanBounded_AndOrderedBySkill()
        {
            foreach (BotPersonality p in System.Enum.GetValues(typeof(BotPersonality)))
            {
                BotProfile easy = BotProfile.For(BotDifficulty.Easy, p);
                BotProfile normal = BotProfile.For(BotDifficulty.Normal, p);
                BotProfile hard = BotProfile.For(BotDifficulty.Hard, p);
                foreach (BotProfile x in new[] { easy, normal, hard })
                {
                    Assert.GreaterOrEqual(x.ReactionTicks, 8, "reaction under 160 ms at 50 Hz");
                    Assert.GreaterOrEqual(x.AimSigma, 0.01f, "perfect aim");
                    Assert.LessOrEqual(x.LeadQuality, 1f);
                    Assert.LessOrEqual(x.TriggerDiscipline, 1f);
                }
                Assert.Greater(easy.ReactionTicks, normal.ReactionTicks);
                Assert.Greater(normal.ReactionTicks, hard.ReactionTicks);
                Assert.Greater(easy.AimSigma, normal.AimSigma);
                Assert.Greater(normal.AimSigma, hard.AimSigma);
            }
        }

        // frob:tests Assets/Scripts/Hullbreach.Bots/BotBuilder.cs::BotBuilder.Build kind="unit"
        [Test]
        public void Builder_ShipsAreValid_ForManySeedsDifficultiesAndPersonalities()
        {
            foreach (BotDifficulty d in Levels)
                foreach (BotPersonality p in System.Enum.GetValues(typeof(BotPersonality)))
                    for (ulong seed = 1; seed <= 4; seed++)
                    {
                        BotShip ship = BotBuilder.Build(seed, d, p);
                        string tag = d + "/" + p + "/" + seed;
                        Assert.IsTrue(BotBuilder.Validate(ship, out string why), tag + ": " + why);
                        Assert.LessOrEqual(ship.BlockCount, BotBuilder.BlockBudget, tag);
                        Assert.AreEqual(DesignVerdict.Ok, DesignValidator.ValidateDesign(ship.ToSnapshot(1, float2.zero, 0f, float2.zero)), tag);
                    }
        }

        // Replaying the order through the real placement rules is how a human would build it.
        // frob:tests Assets/Scripts/Hullbreach.Bots/BotBuilder.cs::BotBuilder.Validate kind="unit"
        [Test]
        public void Builder_OrderReplaysThroughPlacementRules_AndWeldsAreHonoured()
        {
            for (ulong seed = 1; seed <= 10; seed++)
            {
                BotShip ship = BotBuilder.Build(seed, BotDifficulty.Hard, BotPersonality.Balanced);
                var grid = new BlockGrid();
                bool anyWeld = false;
                foreach (BotPlacement p in ship.Order)
                {
                    int key = BlockKey.Pack(p.X, p.Y);
                    Assert.IsTrue(PlacementRules.CanPlace(grid, key, p.TypeId, p.Modifiers, out _), "seed " + seed + " step " + p.X + "," + p.Y);
                    grid.TryAdd(key, new Block(p.TypeId, p.Modifiers, 0, p.Welded ? Block.FlagWelded : (byte)0));
                    if (p.Welded)
                    {
                        anyWeld = true;
                        Assert.IsFalse(PlacementRules.CanRemove(grid, key), "welded block must not be removable");
                    }
                }
                Assert.IsTrue(anyWeld, "the core is always welded");
                Assert.AreEqual(ship.BlockCount, grid.Count);
            }
        }

        // frob:tests Assets/Scripts/Hullbreach.Bots/BotBuilder.cs::BotBuilder.Build kind="unit"
        [Test]
        public void Builder_IsDeterministic_AndVariesWithSeedAndDifficulty()
        {
            string Sig(BotShip s)
            {
                var parts = new List<string>();
                foreach (BotPlacement p in s.Order) parts.Add(p.X + "," + p.Y + ":" + p.TypeId + "." + p.Modifiers);
                return string.Join(";", parts);
            }
            Assert.AreEqual(Sig(BotBuilder.Build(5, BotDifficulty.Normal, BotPersonality.Balanced)),
                            Sig(BotBuilder.Build(5, BotDifficulty.Normal, BotPersonality.Balanced)));
            var seen = new HashSet<string>();
            for (ulong seed = 1; seed <= 16; seed++) seen.Add(Sig(BotBuilder.Build(seed, BotDifficulty.Normal, BotPersonality.Balanced)));
            Assert.GreaterOrEqual(seen.Count, 3, "ships should vary with the seed");
            Assert.AreNotEqual(Sig(BotBuilder.Build(3, BotDifficulty.Easy, BotPersonality.Balanced)),
                               Sig(BotBuilder.Build(3, BotDifficulty.Hard, BotPersonality.Balanced)));
        }

        // frob:tests Assets/Scripts/Hullbreach.Bots/BotBuilder.cs::BotShip.RebuildSeconds kind="unit"
        [Test]
        public void RebuildSeconds_ScalesWithCooldown()
        {
            Assert.AreEqual(4.5f, BotShip.RebuildSeconds(3, 1.5f), 1e-4f);
            Assert.AreEqual(0f, BotShip.RebuildSeconds(-2, 1.5f), 1e-4f);
        }
    }
}
