using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.World;

namespace Hullbreach.Net.Tests
{
    // Planet damage reaches the clients, the config loads from the planets block, and the server and
    // the client prediction run the same crash rules.
    public class PlanetCrashServerTests
    {
        static ShipSnapshot Column(params SnapshotBlock[] blocks) => new ShipSnapshot(0, 0, blocks, 0, 0, 0, 0, 0, 0);

        static (ServerSimulation server, ServerOutbox outbox) Arena()
        {
            var outbox = new ServerOutbox();
            var server = new ServerSimulation(outbox) { TickRate = 50f, TimeoutSeconds = 1e6f };
            return (server, outbox);
        }

        static List<BlockDamaged> Damaged(ServerOutbox outbox)
        {
            var list = new List<BlockDamaged>();
            foreach (OutboxEntry e in outbox.Entries)
            {
                if (e.Bytes[0] != (byte)MessageKind.BlockDamaged) continue;
                var r = new ByteReader(e.Bytes, 0, e.Bytes.Length);
                list.Add(BlockDamaged.Read(ref r));
            }
            return list;
        }

        // Lowest block on the surface of the first default body, falling at `speed`.
        static void Drop(ServerSimulation server, int peer, float speed)
        {
            var ship = server.Ships[peer];
            server.Gravity.TryGetPermanent(0, out var body);
            ship.Rotation = 0f;
            ship.Position = body.Position + new float2(-0.5f, body.Radius + ship.ContactClearance + 0.2f);
            ship.Velocity = new float2(0f, -speed);
        }

        [Test]
        public void HardLanding_IsBroadcastToClients_AndAWornOutBlockIsRemoved()
        {
            var (server, outbox) = Arena();
            server.Join(1, Column(
                new SnapshotBlock(0, 0, BlockTypes.Hull, 0, 0),
                new SnapshotBlock(0, 1, BlockTypes.Core, 0, 0)));
            server.ConfigureDemoArena();
            Drop(server, 1, 14f);
            for (int i = 0; i < 20; i++) server.Tick();

            var damaged = Damaged(outbox);
            Assert.IsTrue(damaged.Exists(m => m.Y == 0 && m.Damage > 0), "the touching block damage reached the wire");
            Assert.IsFalse(damaged.Exists(m => m.Y == 1), "the core above was not hurt");

            // A catastrophic hit wears the hull block down to 255: it is removed, the core is only ever damaged.
            Drop(server, 1, 70f);
            for (int i = 0; i < 20; i++) server.Tick();
            Assert.IsFalse(server.Ships[1].Grid.Contains(BlockKey.Pack(0, 0)), "block destroyed");
        }

        [Test]
        public void Loader_ReadsThePlanetsBlock_AndRejectsBadOnes()
        {
            var ok = ArenaConfigLoader.Parse("{\"planets\":{\"safeLandingSpeed\":6,\"catastrophicSpeed\":40,\"maxSubsteps\":8}}");
            Assert.IsTrue(ok.IsOk, ok.Error);
            Assert.AreEqual(6f, ok.Value.Crash.SafeLandingSpeed);
            Assert.AreEqual(40f, ok.Value.Crash.CatastrophicSpeed);
            Assert.AreEqual(8, ok.Value.Crash.MaxSubsteps);
            Assert.AreEqual(PlanetCrashConfig.Default().DamagePerSpeed, ok.Value.Crash.DamagePerSpeed, "untouched keys keep defaults");

            Assert.AreEqual(PlanetCrashConfig.Default().SafeLandingSpeed, ArenaConfigLoader.Parse("{}").Value.Crash.SafeLandingSpeed);
            Assert.IsFalse(ArenaConfigLoader.Parse("{\"planets\":{\"safeLandingSpeed\":50}}").IsOk, "safe above catastrophic");
            Assert.IsFalse(ArenaConfigLoader.Parse("{\"planets\":{\"damagePerSpeed\":\"lots\"}}").IsOk);
            Assert.IsFalse(ArenaConfigLoader.Parse("{\"planets\":3}").IsOk);
            Assert.IsFalse(ArenaConfigLoader.Parse("{\"planets\":{\"coreMaxDamagePerImpact\":255}}").IsOk);
        }

        [Test]
        public void ConfiguredCrashRules_ReachEveryShip_AndNewJoiners()
        {
            var (server, _) = Arena();
            server.Join(1, Column(new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0)));
            var custom = PlanetCrashConfig.Default();
            custom.SafeLandingSpeed = 9f;
            server.ConfigureDemoArena(new ArenaConfig(ArenaConfig.Defaults().Bodies, 12f, 30f, 2f, 0.18f, null, null, custom));
            server.Join(2, Column(new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0)));
            Assert.AreEqual(9f, server.Ships[1].PlanetCrash.SafeLandingSpeed);
            Assert.AreEqual(9f, server.Ships[2].PlanetCrash.SafeLandingSpeed);
        }

        [Test]
        public void PredictedShip_MatchesTheServer_ThroughAnAtmosphereAndATouchdown()
        {
            var (server, _) = Arena();
            server.Join(1, Column(
                new SnapshotBlock(0, 0, BlockTypes.Hull, 0, 0),
                new SnapshotBlock(0, 1, BlockTypes.Core, 0, 0)));
            server.ConfigureDemoArena();
            Drop(server, 1, 0f);
            server.Ships[1].Position += new float2(0f, 12f);
            server.Ships[1].Velocity = new float2(6f, -16f);

            // The prediction body uses the same shared ShipBody code with the same config.
            var predicted = new Hullbreach.Ship.ShipBody { Gravity = server.Gravity, PlanetCrash = server.PlanetCrash, Tuning = server.FlightTuning };
            foreach (var kv in server.Ships[1].Grid.All) predicted.Grid.TryAdd(kv.Key, kv.Value);
            predicted.RebuildDerivedViews();
            predicted.Position = server.Ships[1].Position;
            predicted.Velocity = server.Ships[1].Velocity;

            for (int i = 0; i < 150; i++)
            {
                server.Tick();
                predicted.Step(default, 1f / 50f);
            }
            Assert.AreEqual(server.Ships[1].Position.x, predicted.Position.x, 1e-3f);
            Assert.AreEqual(server.Ships[1].Position.y, predicted.Position.y, 1e-3f);
        }
    }
}
