using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Builder;
using Hullbreach.Core;
using Hullbreach.Match;
using Hullbreach.Net;
using Hullbreach.Platform;
using Hullbreach.Ship;

namespace Hullbreach.Bots.Tests
{
    // Offline "play vs bot" runs the real ServerSimulation with RequireReady, a build phase and a virtual bot peer
    // (HullbreachNetCodeServerSystem.ManageSparringPartner). These tests drive it the same way: same joins at the same
    // starting points, the same SparringDriver, the human frozen in build mode until READY.
    public sealed class OfflineSparringTests
    {
        const int Human = 1;
        const int BotId = ServerSimulation.MaxPeerId - 1;
        // HullbreachNetCodeServerSystem.SpawnX: where a peer's ship sits through the build phase.
        static float BuildX(int peer) => ((peer & 1) == 0 ? 1f : -1f) * (3f + (peer % 4) * 2f);

        sealed class Offline
        {
            public readonly ServerOutbox Outbox = new ServerOutbox();
            public readonly ServerSimulation Sim;
            public readonly ClientReplica Replica = new ClientReplica();
            public readonly SparringDriver Driver;
            public readonly BotShip Design;
            public uint Tick;

            public Offline(BotDifficulty difficulty, ulong seed = 7)
            {
                Sim = new ServerSimulation(Outbox)
                {
                    TickRate = 50f, EmitPoseMessages = false, RequireReady = true, MatchFeed = new MatchEndFeed(),
                    ReconnectGraceSeconds = 0f, TimeoutSeconds = 1e6f,
                };
                Sim.ConfigureDemoArena();
                Replica.UseArena(ArenaConfig.Defaults());

                var blocks = new List<SnapshotBlock>();
                foreach (var b in ShipTemplates.Default.Blocks) blocks.Add(new SnapshotBlock((sbyte)b.X, (sbyte)b.Y, b.TypeId, b.Modifiers, 0));
                Assert.AreEqual(JoinResult.Joined, Sim.Join(Human, new ShipSnapshot(0, Human, blocks.ToArray(), Quantization.PackPosition(BuildX(Human)), 0, 0, 0, 0, 0)));
                Design = BotBuilder.Build(seed, difficulty, BotPersonality.Balanced);
                Assert.AreEqual(JoinResult.Joined, Sim.Join(BotId, Design.ToSnapshot((ushort)BotId, new float2(BuildX(BotId), 0f), 0f, float2.zero)));
                Sim.SetReady(BotId, true);
                Driver = new SparringDriver(Sim, new BotPeer(BotId, Human, new BotPilot(BotProfile.For(difficulty, BotPersonality.Balanced), new BotRng(seed))));
            }

            // What the human's connection would receive goes to the replica the HUD reads.
            public void Pump()
            {
                foreach (var e in Outbox.Entries)
                    if (e.Peer == Human) Replica.ApplyReceived(e.Bytes, e.Bytes.Length);
                Outbox.Clear();
            }

            // One server tick as the server system runs it: the human's input, the bot's, then the simulation.
            public void Step(byte humanFlags)
            {
                Tick++;
                Sim.SetInput(Human, new InputMessage(Human, Tick, 0, 0, humanFlags));
                Driver.Tick(Sim, Tick);
                Sim.Tick();
                Pump();
            }

            public void Build(float seconds) { for (int i = 0; i < (int)(seconds * 50); i++) Step(InputMessage.BuildModeBit); }
            public void Fly(float seconds) { for (int i = 0; i < (int)(seconds * 50); i++) Step(0); }
            public void Ready() => Assert.IsTrue(Sim.SetReady(Human, true));
        }

        static float CoreHealth(ShipBody ship)
        {
            foreach (var pair in ship.Grid.All)
                if (pair.Value.TypeId == BlockTypes.Core) return 1f - pair.Value.DamageFraction;
            return 0f;
        }

        static int TotalDamage(ShipBody ship)
        {
            int sum = 0;
            foreach (var pair in ship.Grid.All) sum += pair.Value.Damage;
            return sum;
        }

        [Test]
        public void BotStaysFrozenOnItsSpawnThroughALongBuildPhase()
        {
            var game = new Offline(BotDifficulty.Easy);
            game.Build(120f);
            ShipBody bot = game.Sim.Ships[BotId];
            Assert.AreEqual(BuildX(BotId), bot.Position.x, 0.05f, "the bot drifted during the build phase");
            Assert.AreEqual(0f, bot.Position.y, 0.05f, "the bot fell toward the planet during the build phase");
            Assert.AreEqual(game.Design.BlockCount, bot.Grid.Count, "the bot lost blocks before the match");
            Assert.AreEqual(0, TotalDamage(bot));
        }

        [Test]
        public void MatchStartPlacesBothShipsOnTheSharedSpawnsAtRest()
        {
            var game = new Offline(BotDifficulty.Easy);
            game.Build(30f);
            game.Ready();
            Assert.AreEqual(MatchPhase.Running, game.Sim.Match.Phase);
            ArenaConfig arena = ArenaConfig.Defaults();
            foreach (int peer in new[] { Human, BotId })
            {
                arena.SpawnPose(peer == Human ? 0 : 1, 2, out float2 position, out float rotation);
                Assert.AreEqual(position.x, game.Sim.Ships[peer].Position.x, 1e-3f);
                Assert.AreEqual(position.y, game.Sim.Ships[peer].Position.y, 1e-3f);
                Assert.AreEqual(rotation, game.Sim.Ships[peer].Rotation, 1e-3f);
                Assert.AreEqual(0f, math.length(game.Sim.Ships[peer].Velocity), 1e-4f);
            }
        }

        [Test]
        public void TheBotReadsTheStormOnTheMatchClockNotTheServerClock()
        {
            var game = new Offline(BotDifficulty.Easy);
            game.Build(240f);                      // the server clock is about 12000 ticks ahead of the match clock
            game.Ready();
            game.Fly(1f);
            Assert.NotNull(game.Sim.ActiveStorm, "the offline server runs a storm");
            Assert.Greater(game.Sim.StormStartTick, 11900u);
            var source = new SimulationStormSource(game.Sim);
            Assert.IsTrue(source.TryGetState(game.Tick, out var seen));
            var truth = game.Sim.ActiveStorm.At((int)(game.Tick - game.Sim.StormStartTick));
            Assert.AreEqual(truth.Radius, seen.Radius, 1e-4f);
            Assert.AreEqual(truth.Stage, seen.Stage);
            var wrong = game.Sim.ActiveStorm.At((int)game.Tick);
            Assert.IsTrue(wrong.Radius != seen.Radius || wrong.Stage != seen.Stage, "the absolute tick must read a different ring than the match clock, or this test proves nothing");
        }

        [Test]
        public void TheEasyBotEngagesAnIdlePlayer_ShootingAndLandingHits()
        {
            var game = new Offline(BotDifficulty.Easy);
            game.Build(20f);
            game.Ready();
            var seen = new HashSet<uint>();
            int shots = 0;
            for (int i = 0; i < 50 * 120; i++)
            {
                game.Step(0);
                foreach (var p in game.Sim.Projectiles)
                    if (seen.Add(p.Id) && p.OwnerPeer == BotId) shots++;
            }
            Assert.GreaterOrEqual(shots, 30, "the bot barely fired in two minutes");
            Assert.Greater(TotalDamage(game.Sim.Ships[Human]), 0, "none of the bot's shots landed");
        }

        [Test]
        public void TheReplicaCoreMatchesTheServerCore_ThroughAFullMatch()
        {
            // Both ships flown by bots so cannons, planets, storm and contacts all hurt: the HUD reads the replica, so the
            // replica must agree with the server's block damage the whole way (HullbreachMatchHudView.CoreHealth).
            var game = new Offline(BotDifficulty.Normal, 11);
            var humanPilot = new BotPeer(Human, BotId, new BotPilot(BotProfile.For(BotDifficulty.Normal, BotPersonality.Balanced), new BotRng(5)));
            game.Build(5f);
            game.Ready();
            var storm = new SimulationStormSource(game.Sim);
            int checks = 0;
            for (int i = 0; i < 50 * 240 && game.Sim.Match.Phase == MatchPhase.Running; i++)
            {
                game.Tick++;
                humanPilot.Drive(game.Sim, game.Tick, storm);
                game.Driver.Tick(game.Sim, game.Tick);
                game.Sim.Tick();
                game.Pump();
                if (i % 100 != 0) continue;
                foreach (int peer in new[] { Human, BotId })
                {
                    Assert.IsTrue(game.Replica.Ships.TryGetValue((ushort)peer, out ShipBody mirror), "replica lost ship " + peer);
                    Assert.AreEqual(CoreHealth(game.Sim.Ships[peer]), CoreHealth(mirror), 1e-4f, "core bar disagrees with the server for peer " + peer + " at step " + i);
                    Assert.AreEqual(TotalDamage(game.Sim.Ships[peer]), TotalDamage(mirror), "block damage disagrees for peer " + peer + " at step " + i);
                    checks++;
                }
            }
            Assert.Greater(checks, 20);
        }
    }
}
