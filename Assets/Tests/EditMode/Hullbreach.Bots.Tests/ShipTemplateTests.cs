using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Builder;
using Hullbreach.Core;
using Hullbreach.Net;
using Hullbreach.Ship;

namespace Hullbreach.Bots.Tests
{
    // The starter templates the build phase offers: every one must be a legal, affordable, stable, straight-flying ship.
    // frob:doc docs/reference/hullbreach-builder.md#shiptemplates
    public sealed class ShipTemplateTests
    {
        static IEnumerable<ShipTemplate> Templates() => ShipTemplates.All;

        static BotShip AsBotShip(ShipTemplate t)
        {
            Assert.IsTrue(ShipDesignCodec.TryBuildGrid(t.Blocks, out BlockGrid grid, out string why), t.Id + ": " + why);
            Assert.IsTrue(ShipDesignCodec.TryDecode(ShipDesignCodec.Encode(grid), out var ordered));
            var order = new List<BotPlacement>();
            foreach (DesignBlock b in ordered) order.Add(new BotPlacement(b.X, b.Y, b.TypeId, b.Modifiers, false));
            return new BotShip(grid, order);
        }

        // frob:tests Assets/Scripts/Hullbreach.Builder/ShipTemplates.cs::ShipTemplates.All kind="unit"
        [Test]
        public void EveryTemplate_IsLegalAndInsideBudgetAndSizeCap([ValueSource(nameof(Templates))] ShipTemplate t)
        {
            BlockCosts costs = BlockCosts.Default();
            Assert.IsTrue(ShipDesignCodec.TryBuildGrid(t.Blocks, out BlockGrid grid, out string why), why);
            Assert.LessOrEqual(t.Cost(costs), costs.Budget, t.Id + " over budget");
            Assert.GreaterOrEqual(t.Cost(costs), costs.Budget - 8, t.Id + " leaves too many points unspent");
            Assert.LessOrEqual(grid.Count, costs.MaxBlocks);
            Assert.IsTrue(t.Blocks.Count >= 15, t.Id + " is too small to compete with a bot");
        }

        // frob:tests Assets/Scripts/Hullbreach.Builder/ShipTemplates.cs::ShipTemplates.All kind="unit"
        [Test]
        public void EveryTemplate_PassesTheServersDesignChecksAndTheBotStressTest([ValueSource(nameof(Templates))] ShipTemplate t)
        {
            BotShip ship = AsBotShip(t);
            Assert.IsTrue(BotBuilder.Validate(ship, out string reason), t.Id + ": " + reason);
        }

        // frob:tests Assets/Scripts/Hullbreach.Builder/ShipTemplates.cs::ShipTemplates.All kind="unit"
        [Test]
        public void EveryTemplate_FliesStraightUnderFullThrust([ValueSource(nameof(Templates))] ShipTemplate t)
        {
            BotShip ship = AsBotShip(t);
            var body = new ShipBody();
            foreach (var kv in ship.Grid.All) body.Grid.TryAdd(kv.Key, kv.Value);
            body.RebuildDerivedViews();
            const float dt = 0.02f;
            float peakSpin = 0f;
            for (int i = 0; i < 250; i++)
            {
                body.Step(new ShipInput(1f, 0f, false), dt);
                peakSpin = math.max(peakSpin, math.abs(body.AngularVelocity));
            }
            Assert.Less(peakSpin, 0.05f, t.Id + " spins up under straight thrust");
            Assert.Less(math.abs(body.Rotation), 0.1f, t.Id + " turns under straight thrust");
            Assert.Greater(math.length(body.Velocity), 1f, t.Id + " barely accelerates");
        }

        // frob:tests Assets/Scripts/Hullbreach.Builder/ShipTemplates.cs::ShipTemplates.Default kind="unit"
        [Test]
        public void Default_IsBalancedAndHasABrake()
        {
            Assert.AreEqual("balanced", ShipTemplates.Default.Id);
            bool retro = false;
            foreach (DesignBlock b in ShipTemplates.Default.Blocks) if (b.TypeId == BlockTypes.RetroThruster) retro = true;
            Assert.IsTrue(retro, "the Cadet Trial asks the player to brake");
            Assert.IsNull(ShipTemplates.ById("nope"));
            Assert.AreSame(ShipTemplates.Default, ShipTemplates.ById("balanced"));
        }

        // frob:tests Assets/Scripts/Hullbreach.Builder/ShipTemplates.cs::ShipDesignCodec.TryDecode kind="unit"
        [Test]
        public void Codec_RoundTripsATemplate_AndRejectsGarbage([ValueSource(nameof(Templates))] ShipTemplate t)
        {
            string text = t.Encode();
            Assert.LessOrEqual(text.Length, ShipDesignCodec.MaxChars);
            Assert.IsTrue(ShipDesignCodec.TryDecode(text, out var back));
            Assert.AreEqual(t.Blocks.Count, back.Count);
            Assert.AreEqual(text, ShipDesignCodec.Encode(back));
            foreach (string bad in new[] { "", "1,2,3", "a,b,c,d", "0,0,0,9", "0,0,99,0", "200,0,1,0", "0,0,0,0;", "0,0,0,0;1,1,1", "0,0,0,0 ;1,1,1,0", new string('1', 600) })
                Assert.IsFalse(ShipDesignCodec.TryDecode(bad, out _), "accepted: " + bad);
        }

        // frob:tests Assets/Scripts/Hullbreach.Builder/ShipTemplates.cs::ShipDesignCodec.TryBuildGrid kind="unit"
        [Test]
        public void BuildGrid_RefusesIllegalDesigns()
        {
            var noCore = new List<DesignBlock> { new DesignBlock(1, 0, BlockTypes.Hull, 0) };
            Assert.IsFalse(ShipDesignCodec.TryBuildGrid(noCore, out _, out _));
            var floating = new List<DesignBlock> { new DesignBlock(0, 0, BlockTypes.Core, 0), new DesignBlock(5, 5, BlockTypes.Hull, 0) };
            Assert.IsFalse(ShipDesignCodec.TryBuildGrid(floating, out _, out _));
            var offCenter = new List<DesignBlock> { new DesignBlock(1, 0, BlockTypes.Core, 0) };
            Assert.IsFalse(ShipDesignCodec.TryBuildGrid(offCenter, out _, out _));
        }
    }
}
