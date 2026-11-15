using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Builder;
using Hullbreach.Core;
using Hullbreach.Net;
using Hullbreach.World;

namespace Hullbreach.Bots.Tests
{
    // What the bots may see, how they use the economy through the server API, and that the real storm ends matches.
    // frob:ticket 01M7EXX75QK41EKPV5392HXCAX
    public sealed class BotEconomyTests
    {
        // Two bot ships joined to a live server, the way ArenaMatch sets one up.
        static ServerSimulation Arena(out BotShip shipA, out BotShip shipB)
        {
            shipA = BotBuilder.Build(1, BotDifficulty.Normal, BotPersonality.Balanced);
            shipB = BotBuilder.Build(2, BotDifficulty.Normal, BotPersonality.Balanced);
            var sim = new ServerSimulation(new DiscardOutbox()) { TickRate = 50f, EmitPoseMessages = false, StormEnabled = false };
            Assert.AreEqual(JoinResult.Joined, sim.Join(1, shipA.ToSnapshot(1, float2.zero, 0f, float2.zero)));
            Assert.AreEqual(JoinResult.Joined, sim.Join(2, shipB.ToSnapshot(2, float2.zero, 0f, float2.zero)));
            sim.ConfigureDemoArena();
            Assert.AreEqual(Hullbreach.Match.MatchPhase.Running, sim.Match.Phase);
            return sim;
        }

        // frob:tests Assets/Scripts/Hullbreach.Bots/BotPeer.cs::BotPeer.Observe kind="unit"
        [Test]
        public void Observe_GivesCoarseEnemyDamage_AndOnScreenCellsOnly()
        {
            ServerSimulation sim = Arena(out _, out _);
            var peer = new BotPeer(1, 2, new BotPilot(BotProfile.For(BotDifficulty.Normal, BotPersonality.Balanced), new BotRng(5)));
            var enemy = sim.Ships[2];
            int core = enemy.Grid.CoreKey.Value;
            enemy.Grid.TrySet(core, enemy.Grid.TryGet(core, out Block b) ? b.WithDamage(97) : b);

            BotObservation obs = peer.Observe(sim, 1, null);
            Assert.AreEqual(0.25f, obs.Enemy.CoreDamage, 1e-6f, "97/255 reads as a quarter, not as the number");
            Assert.IsNotNull(obs.Enemy.Cells);
            Assert.AreEqual(enemy.Grid.Count, obs.Enemy.Cells.Count);
            bool sawCore = false;
            foreach (EnemyCell c in obs.Enemy.Cells) sawCore |= c.Type == BlockTypes.Core;
            Assert.IsTrue(sawCore, "the core is on screen");
        }

        // frob:tests Assets/Scripts/Hullbreach.Bots/BotQuartermaster.cs::BotQuartermaster.Act kind="unit"
        [Test]
        public void Quartermaster_PutsALostBlockBack_FromStock_ThroughTheServer()
        {
            ServerSimulation sim = Arena(out BotShip design, out _);
            var peer = new BotPeer(1, 2, new BotPilot(BotProfile.For(BotDifficulty.Normal, BotPersonality.Balanced), new BotRng(5)));
            BlockGrid grid = sim.Ships[1].Grid;

            // Lose one removable hull block, the way a hit would.
            int lost = int.MinValue;
            foreach (BotPlacement p in design.Order)
            {
                int key = BlockKey.Pack(p.X, p.Y);
                if (p.TypeId == BlockTypes.Hull && grid.Contains(key) && PlacementRules.CanRemove(grid, key)) { lost = key; break; }
            }
            Assert.AreNotEqual(int.MinValue, lost, "the design has a removable hull block");
            grid.TryRemove(lost);
            sim.Ships[1].RebuildDerivedViews();
            int before = grid.Count;
            Assert.Greater(sim.StockOf(1)[BlockTypes.Hull], 0, "the starter kit holds hull");

            var qm = new BotQuartermaster(design);
            BotObservation obs = peer.Observe(sim, 1, null);
            obs.Enemy = default; // out of sight: a quiet moment
            bool acted = qm.Act(sim, 1, 1, obs, flags => sim.SetInput(1, new InputMessage(1, 1, 0, 0, flags)));

            Assert.IsTrue(acted);
            Assert.AreEqual(1, qm.Placed);
            Assert.AreEqual(before + 1, grid.Count);
            Assert.IsTrue(grid.Contains(lost));
        }

        // frob:tests Assets/Scripts/Hullbreach.Bots/BotQuartermaster.cs::BotQuartermaster.Act kind="unit"
        [Test]
        public void Quartermaster_StaysOutOfTheWay_WhenTheEnemyIsClose()
        {
            ServerSimulation sim = Arena(out BotShip design, out _);
            var peer = new BotPeer(1, 2, new BotPilot(BotProfile.For(BotDifficulty.Normal, BotPersonality.Balanced), new BotRng(5)));
            var qm = new BotQuartermaster(design);
            BotObservation obs = peer.Observe(sim, 1, null);
            obs.Enemy.Visible = true;
            obs.Enemy.Position = obs.Self.Position + new float2(5f, 0f);
            Assert.IsFalse(qm.Act(sim, 1, 1, obs, flags => Assert.Fail("must not enter build mode in a fight")));
        }

        static BotObservation OpenSpace(float2 pickupAt)
        {
            var obs = new BotObservation { Tick = 0, Dt = 0.02f };
            obs.Self = new SelfView
            {
                Position = float2.zero, Rotation = 0f, Alive = true, MaxForwardAccel = 2f, MaxRetroAccel = 1f,
                MaxAngularAccel = 0.6f, MaxTurnRate = 1.5f, AimLocal = new float2(0f, 1f), HasCannon = true,
                CannonCooldownSeconds = 0.3f,
            };
            obs.Pickups.Add(new PickupView { Position = pickupAt, Velocity = float2.zero, Type = BlockTypes.Armor, ExpireTick = 100000 });
            return obs;
        }

        // frob:tests Assets/Scripts/Hullbreach.Bots/BotPilot.cs::BotPilot.Next kind="unit"
        [Test]
        public void Pilot_FliesToACrateWhenTheFightIsQuiet_ButEasyDoesNot()
        {
            foreach (BotDifficulty d in new[] { BotDifficulty.Normal, BotDifficulty.Hard })
            {
                var pilot = new BotPilot(BotProfile.For(d, BotPersonality.Balanced), new BotRng(3));
                BotObservation obs = OpenSpace(new float2(0f, 15f));
                float thrust = 0f;
                for (uint t = 0; t < 20; t++) { obs.Tick = t; thrust = System.Math.Max(thrust, pilot.Next(obs).ThrustAxis); }
                Assert.AreEqual(BotMode.Scavenge, pilot.Mode, d.ToString());
                Assert.Greater(thrust, 0.5f, d + " burns toward the crate");
            }
            var easy = new BotPilot(BotProfile.For(BotDifficulty.Easy, BotPersonality.Balanced), new BotRng(3));
            BotObservation eo = OpenSpace(new float2(0f, 15f));
            easy.Next(eo);
            Assert.AreNotEqual(BotMode.Scavenge, easy.Mode);
        }

        // frob:tests Assets/Scripts/Hullbreach.Bots/BotPilot.cs::BotPilot.Next kind="unit"
        [Test]
        public void Pilot_IgnoresACrateNextToAPlanet()
        {
            var pilot = new BotPilot(BotProfile.For(BotDifficulty.Hard, BotPersonality.Balanced), new BotRng(3));
            BotObservation obs = OpenSpace(new float2(0f, 15f));
            obs.Planets.Add(new GravityBody(new float2(0f, 20f), 50f, 4f, 0.2f));
            pilot.Next(obs);
            Assert.AreNotEqual(BotMode.Scavenge, pilot.Mode);
        }

        // frob:tests Assets/Scripts/Hullbreach.Bots/ArenaMatch.cs::ArenaMatch.Run kind="unit"
        [Test]
        public void RealStorm_EndsAMatchBySuddenDeath_WithoutAnOverlay()
        {
            // One short phase: grace 1 s, hold 20 s, shrink 5 s, then 200 damage a second at the core.
            var storm = new StormConfig(50f, 100f, 1f, 0.1f, 20f, 200f, new[] { new StormPhase(20f, 5f, 0f, 20f) });
            ArenaMatchResult r = ArenaMatch.Run(new ArenaMatchConfig
            {
                Seed = 4, MaxSeconds = 60f, Storm = storm,
                A = new ArenaSide { Difficulty = BotDifficulty.Normal }, B = new ArenaSide { Difficulty = BotDifficulty.Normal },
            });
            Assert.IsNull(r.Error, r.Error);
            // Sudden death takes both cores, so it ends the match as a storm win or, when they fall together, a draw.
            Assert.IsTrue(r.Ending == ArenaEnding.Storm || r.Ending == ArenaEnding.Draw, "ended by " + r.Ending);
            Assert.Less(r.Seconds, 45f);
        }
    }
}
