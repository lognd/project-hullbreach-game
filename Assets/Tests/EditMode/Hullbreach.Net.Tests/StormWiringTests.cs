using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Match;
using Hullbreach.Platform;
using Hullbreach.World;

namespace Hullbreach.Net.Tests
{
    // The storm is wired into the live server: damage, sudden death, projectile despawn and the wire format.
    public class StormWiringTests
    {
        // One 20 s hold at 10 HP/s, a 1 s shrink to a closed ring, then 200 HP/s sudden death at tick ~1050.
        static StormConfig ShortStorm() => new StormConfig(50f, 100f, 0f, 0.1f, 20f, 200f, new[]
        {
            new StormPhase(20f, 1f, 0f, 10f),
        });

        static ShipSnapshot Ship(params SnapshotBlock[] blocks) => new ShipSnapshot(0, 0, blocks, 0, 0, 0, 0, 0, 0);
        static SnapshotBlock Core() => new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0);
        static SnapshotBlock Plain(sbyte y) => new SnapshotBlock(0, y, BlockTypes.Cannon, 0, 0);

        static (ServerSimulation server, ServerOutbox outbox) Arena(StormConfig storm)
        {
            var outbox = new ServerOutbox();
            var server = new ServerSimulation(outbox) { TickRate = 50f, TimeoutSeconds = 1e6f, Storm = storm, MatchFeed = new MatchEndFeed() };
            return (server, outbox);
        }

        static void Run(ServerSimulation server, int ticks)
        {
            // Pinned in place so the arena edge push never drags a far ship into the other one.
            for (int i = 0; i < ticks; i++)
            {
                foreach (var ship in server.Ships.Values) ship.Velocity = float2.zero;
                server.Tick();
            }
        }

        static int Damage(ServerSimulation server, int peer, int x, int y)
        {
            Assert.IsTrue(server.Ships[peer].Grid.TryGet(BlockKey.Pack(x, y), out Block b), "block exists");
            return b.Damage;
        }

        [Test]
        public void ShipOutsideTheRing_TakesDamage_AndShipInsideDoesNot()
        {
            var (server, _) = Arena(ShortStorm());
            server.Join(1, Ship(Core(), Plain(1)));
            server.Join(2, Ship(Core(), Plain(1)));
            server.Ships[1].Position = new float2(60f, 0f);
            server.Ships[2].Position = new float2(300f, 0f);   // far outside the 100 radius
            Run(server, 100);

            Assert.AreEqual(0, Damage(server, 1, 0, 1), "inside the ring");
            // 10 HP/s * 2 s = 20, from fractional 0.2/tick carried between ticks.
            Assert.That(Damage(server, 2, 0, 1), Is.InRange(19, 20), "outside the ring");
        }

        // The results screen once showed "Damage dealt: 420" next to "Blocks destroyed: 0" after a storm-only match, so the
        // stats are pinned: the ring is nobody's weapon, its damage and the blocks it destroys credit no player.
        [Test]
        public void StormDamage_IsNotCreditedToAnyPlayersStats()
        {
            var feed = new MatchEndFeed();
            var raised = new List<MatchEndedArgs>();
            feed.Ended += raised.Add;
            var server = new ServerSimulation(new ServerOutbox()) { TickRate = 50f, TimeoutSeconds = 1e6f, Storm = ShortStorm(), MatchFeed = feed };
            server.Join(1, Ship(Core(), Plain(1), Plain(2)));
            server.Join(2, Ship(Core(), Plain(1), Plain(2)));
            server.Ships[1].Position = new float2(-300f, 0f);   // both far outside the ring: only the storm hurts them
            server.Ships[2].Position = new float2(300f, 0f);
            for (int i = 0; i < 4000 && server.Match.Phase == MatchPhase.Running; i++)
            {
                foreach (var ship in server.Ships.Values) ship.Velocity = float2.zero;
                server.Tick();
            }
            Assert.AreEqual(MatchPhase.Ended, server.Match.Phase, "sudden death ends the match");
            Assert.AreEqual(1, raised.Count);
            Assert.AreEqual(0, raised[0].StatsA.DamageDealt, "storm damage is not damage dealt (A)");
            Assert.AreEqual(0, raised[0].StatsB.DamageDealt, "storm damage is not damage dealt (B)");
            Assert.AreEqual(0, raised[0].StatsA.BlocksDestroyed);
            Assert.AreEqual(0, raised[0].StatsB.BlocksDestroyed);
        }

        [Test]
        public void LowDamage_IsNotLostToTruncation()
        {
            var slow = new StormConfig(50f, 100f, 0f, 0.1f, 20f, 200f, new[] { new StormPhase(20f, 1f, 0f, 1f) });
            var (server, _) = Arena(slow);
            server.Join(1, Ship(Core(), Plain(1)));
            server.Join(2, Ship(Core(), Plain(1)));
            server.Ships[1].Position = float2.zero;
            server.Ships[2].Position = new float2(300f, 0f);
            Run(server, 250);   // 1 HP/s over 5 s: 0.02 per tick would floor to zero every tick
            Assert.That(Damage(server, 2, 0, 1), Is.InRange(4, 5));
        }

        [Test]
        public void Core_IsSpared_UntilSuddenDeath()
        {
            var (server, _) = Arena(ShortStorm());
            server.Join(1, Ship(Core(), Plain(1)));
            server.Join(2, Ship(Core(), Plain(1)));
            server.Ships[1].Position = float2.zero;
            server.Ships[2].Position = new float2(300f, 0f);
            Run(server, 1000);
            Assert.AreEqual(0, Damage(server, 2, 0, 0), "core untouched through hold and shrink");
            Assert.Greater(Damage(server, 2, 0, 1), 0, "the other block is hurt");

            Run(server, 80);   // sudden death starts at tick 1050
            Assert.Greater(Damage(server, 2, 0, 0), 0, "sudden death reaches the core");
        }

        [Test]
        public void SuddenDeath_BreachesTheWeakerCore_AndEndsTheMatch()
        {
            var (server, _) = Arena(ShortStorm());
            server.Join(1, Ship(Core()));
            server.Join(2, Ship(Core()));
            server.Ships[1].Position = float2.zero;
            server.Ships[2].Position = new float2(5f, 0f);
            // A join resets damage, so wound peer 1's core on the live grid instead.
            int coreKey = BlockKey.Pack(0, 0);
            server.Ships[1].Grid.TryGet(coreKey, out Block core);
            server.Ships[1].Grid.TrySet(coreKey, core.WithDamage(100));
            Run(server, 1040);
            Assert.AreEqual(MatchPhase.Running, server.Match.Phase, "the ring alone does not end the match early");

            Run(server, 200);
            Assert.AreEqual(MatchPhase.Ended, server.Match.Phase);
            Assert.AreEqual(MatchResult.CoreBreached, server.Match.Result);
            Assert.AreEqual(2, server.Match.WinnerPeer, "the more damaged core falls first");
        }

        [Test]
        public void Projectile_PastTheHardLimit_IsDespawned_ButSurvivesWithoutAStorm()
        {
            int Furthest(bool storm)
            {
                var (server, _) = Arena(ShortStorm());
                server.StormEnabled = storm;
                server.Join(1, Ship(Core(), new SnapshotBlock(0, 1, BlockTypes.Cannon, 0, 0)));
                server.Join(2, Ship(Core()));
                server.Ships[1].Position = new float2(0f, 115f);
                server.Ships[2].Position = new float2(0f, -60f);
                float maxY = 0f;
                for (int i = 0; i < 60; i++)
                {
                    server.SetInput(1, InputMessage.FromFloats(1, (uint)i + 1, 0f, 0f, i == 0));
                    server.Tick();
                    foreach (var p in server.Projectiles) maxY = math.max(maxY, p.Position.y);
                }
                return (int)maxY;
            }

            Assert.Greater(Furthest(false), 120, "precondition: the shot leaves the hard limit when nothing stops it");
            Assert.LessOrEqual(Furthest(true), 120, "the storm removes it at the limit");
        }

        [Test]
        public void MatchStart_SendsStormStarted_ToBothPeers_WithTheMatchSeed()
        {
            var (server, outbox) = Arena(ShortStorm());
            server.ConfigureDemoArena();
            server.Join(1, Ship(Core()));
            server.Join(2, Ship(Core()));

            var seen = new List<StormStarted>();
            var peers = new List<int>();
            foreach (OutboxEntry e in outbox.Entries)
            {
                if (e.Bytes[0] != (byte)MessageKind.StormStarted) continue;
                var r = new ByteReader(e.Bytes, 0, e.Bytes.Length);
                var m = StormStarted.Read(ref r);
                Assert.IsFalse(r.Failed);
                Assert.IsTrue(e.Reliable);
                seen.Add(m);
                peers.Add(e.Peer);
            }
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, peers);
            Assert.AreEqual(ServerSimulation.StormSeedFor(server.Match.MatchId), seen[0].Seed);
            Assert.AreEqual(2, seen[0].Candidates.Length, "one candidate per arena body");
            Assert.AreEqual(server.ActiveStorm.FinalCenter, seen[0].BuildZone().FinalCenter);
        }

        [Test]
        public void StormStarted_RoundTrips_SoClientsRebuildTheSameZone()
        {
            var config = StormConfig.Default();
            var cands = new[] { new float2(0f, -60f), new float2(45f, 20f) };
            var msg = new StormStarted(7, 0xDEADBEEFu, 1234, new float2(1f, -2f), cands, config);
            var buf = new byte[StormStarted.MaxByteSize];
            var w = new ByteWriter(buf);
            Assert.IsTrue(msg.TryWrite(ref w));
            var r = new ByteReader(buf, 0, w.Position);
            var back = StormStarted.Read(ref r);
            Assert.IsFalse(r.Failed);
            Assert.AreEqual(0, r.Remaining);
            Assert.AreEqual(msg.Seed, back.Seed);
            Assert.AreEqual(msg.StartTick, back.StartTick);
            Assert.AreEqual(msg.Sequence, back.Sequence);

            StormZone a = msg.BuildZone(), b = back.BuildZone();
            Assert.AreEqual(a.FinalCenter, b.FinalCenter);
            Assert.AreEqual(a.SuddenDeathTick, b.SuddenDeathTick);
            for (int t = 0; t < a.SuddenDeathTick + 50; t += 37)
            {
                Assert.AreEqual(a.At(t).Radius, b.At(t).Radius);
                Assert.AreEqual(a.At(t).Center, b.At(t).Center);
                Assert.AreEqual(a.At(t).DamagePerTick, b.At(t).DamagePerTick);
            }
        }

        [Test]
        public void StormStarted_RejectsTruncationAndBadConfig()
        {
            var msg = new StormStarted(1, 5, 0, float2.zero, new[] { float2.zero }, StormConfig.Default());
            var buf = new byte[StormStarted.MaxByteSize];
            var w = new ByteWriter(buf);
            msg.TryWrite(ref w);
            for (int cut = 0; cut < w.Position; cut += 7)
            {
                var r = new ByteReader(buf, 0, cut);
                StormStarted.Read(ref r);
                Assert.IsTrue(r.Failed, "cut at " + cut);
            }

            var bad = new StormStarted(1, 5, 0, float2.zero, null,
                new StormConfig(50f, 100f, 0f, 0.1f, 20f, 200f, new[] { new StormPhase(1f, 1f, 0f, 1f) }));
            var w2 = new ByteWriter(new byte[StormStarted.MaxByteSize]);
            Assert.IsFalse(bad.TryWrite(ref w2), "hold under the warning minimum is refused on write");
        }

        [Test]
        public void ClientReplica_AppliesStormStarted_InSequence()
        {
            var (server, outbox) = Arena(ShortStorm());
            server.Join(1, Ship(Core()));
            server.Join(2, Ship(Core()));
            var replica = new ClientReplica();
            Assert.IsNull(replica.Storm);
            foreach (OutboxEntry e in outbox.Entries)
                if (e.Peer == 1) replica.ApplyReceived(e.Bytes, e.Bytes.Length);

            Assert.IsNotNull(replica.Storm);
            Assert.AreEqual(server.ActiveStorm.SuddenDeathTick, replica.Storm.SuddenDeathTick);
            Assert.AreEqual(server.ActiveStorm.FinalCenter, replica.Storm.FinalCenter);
        }

        [Test]
        public void Seed_IsStablePerMatchId_AndDiffersBetweenIds()
        {
            Assert.AreEqual(ServerSimulation.StormSeedFor("a"), ServerSimulation.StormSeedFor("a"));
            Assert.AreNotEqual(ServerSimulation.StormSeedFor("a"), ServerSimulation.StormSeedFor("b"));
            Assert.AreEqual(2166136261u, ServerSimulation.StormSeedFor(""));
        }

        [Test]
        public void ArenaConfig_StormBlock_DefaultsWhenAbsent_AndOverridesWhenPresent()
        {
            var none = ArenaConfigLoader.Parse("{}");
            Assert.IsTrue(none.IsOk, none.Error);
            Assert.AreEqual(StormConfig.Default().StartRadius, none.Value.Storm.StartRadius);
            Assert.AreEqual(5, none.Value.Storm.Phases.Length);

            var r = ArenaConfigLoader.Parse(
                "{\"storm\":{\"graceSeconds\":5,\"suddenDeathDamagePerSecond\":99,"
                + "\"phases\":[{\"hold\":20,\"shrink\":10,\"radius\":40,\"damage\":5}]}}");
            Assert.IsTrue(r.IsOk, r.Error);
            Assert.AreEqual(5f, r.Value.Storm.GraceSeconds);
            Assert.AreEqual(99f, r.Value.Storm.SuddenDeathDamagePerSecond);
            Assert.AreEqual(100f, r.Value.Storm.StartRadius, "omitted keys keep defaults");
            Assert.AreEqual(1, r.Value.Storm.Phases.Length);
            Assert.AreEqual(40f, r.Value.Storm.Phases[0].TargetRadius);
        }

        [TestCase("{\"storm\":5}")]
        [TestCase("{\"storm\":{\"startRadius\":-1}}")]
        [TestCase("{\"storm\":{\"phases\":[]}}")]
        [TestCase("{\"storm\":{\"phases\":[{\"hold\":5,\"shrink\":5,\"radius\":10,\"damage\":1}]}}")]
        [TestCase("{\"storm\":{\"phases\":[{\"hold\":30,\"shrink\":5,\"radius\":500,\"damage\":1}]}}")]
        [TestCase("{\"storm\":{\"phases\":[{\"hold\":30,\"shrink\":5,\"radius\":10}]}}")]
        [TestCase("{\"storm\":{\"warningFraction\":\"x\"}}")]
        public void ArenaConfig_BadStormBlock_IsAnErrorNotAThrow(string json)
        {
            var r = ArenaConfigLoader.Parse(json);
            Assert.IsFalse(r.IsOk);
            Assert.IsNotEmpty(r.Error);
        }

        [Test]
        public void ConfigureDemoArena_UsesTheConfigStorm()
        {
            var parsed = ArenaConfigLoader.Parse("{\"storm\":{\"graceSeconds\":7}}");
            var (server, _) = Arena(StormConfig.Default());
            server.ConfigureDemoArena(parsed.Value);
            Assert.AreEqual(7f, server.Storm.GraceSeconds);
        }
    }
}
