using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Match;
using Hullbreach.Platform;
using Hullbreach.World;

namespace Hullbreach.Net.Tests
{
    // Match pacing: the two ships start far apart on opposite sides, facing each other and clear of every planet's gravity
    // well and atmosphere, so a fight opens with an approach (playtest: spawns 25 to 45 units apart, shot dead in 55 s).
    public class ArenaSpawnTests
    {
        // A core between two hull blocks: legal, and it gives QueryPlanetProximity a real lowest block.
        static ShipSnapshot Ship() => new ShipSnapshot(0, 0, new[]
        {
            new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0),
            new SnapshotBlock(0, 1, BlockTypes.Hull, 0, 0),
            new SnapshotBlock(0, -1, BlockTypes.Hull, 0, 0),
        }, 0, 0, 0, 0, 0, 0);

        static ServerSimulation Started(ArenaConfig arena)
        {
            var server = new ServerSimulation(new ServerOutbox()) { TickRate = 50f, MatchFeed = new MatchEndFeed(), ReconnectGraceSeconds = 0f, TimeoutSeconds = 1e6f };
            server.ConfigureDemoArena(arena);
            server.Join(1, Ship());
            server.Join(2, Ship());
            return server;
        }

        [Test]
        public void Defaults_AreFarApartOnOppositeSidesFacingEachOther()
        {
            ArenaConfig arena = ArenaConfig.Defaults();
            Assert.GreaterOrEqual(arena.SpawnSeparation, 120f);
            arena.SpawnPose(0, 2, out float2 a, out float ra);
            arena.SpawnPose(1, 2, out float2 b, out float rb);
            Assert.AreEqual(arena.SpawnSeparation, math.distance(a, b), 1e-3f);
            Assert.Less(a.x, arena.Bounds.Center.x);
            Assert.Greater(b.x, arena.Bounds.Center.x);
            // Rotation 0 points up the +y axis, counter-clockwise positive: -90 degrees faces +x, +90 faces -x.
            float2 noseA = new float2(-math.sin(ra), math.cos(ra)), noseB = new float2(-math.sin(rb), math.cos(rb));
            Assert.Greater(math.dot(noseA, math.normalize(b - a)), 0.99f, "slot 0 faces slot 1");
            Assert.Greater(math.dot(noseB, math.normalize(a - b)), 0.99f, "slot 1 faces slot 0");
            Assert.Less(math.length(a - arena.Bounds.Center), arena.Bounds.Radius - arena.Bounds.Margin, "inside the soft edge");
        }

        [Test]
        public void EverySpawn_IsClearOfEveryPlanetAtmosphereAndItsStrongestPull()
        {
            ArenaConfig arena = ArenaConfig.Defaults();
            PlanetCrashConfig crash = arena.Crash;
            for (int slot = 0; slot < 2; slot++)
            {
                arena.SpawnPose(slot, 2, out float2 p, out _);
                foreach (ArenaBodyConfig body in arena.Bodies)
                {
                    GravityBody g = body.ToGravityBody();
                    float distance = math.distance(p, g.Position);
                    float surfaceGap = distance - g.Radius;
                    Assert.Greater(surfaceGap, crash.AtmosphereDepth(g) + 15f, "slot " + slot + " too near a planet's atmosphere");
                    Assert.Less(g.Mu / (distance * distance), 0.25f, "slot " + slot + " inside a strong gravity well");
                }
            }
        }

        [Test]
        public void ShipsStartTheMatchOnTheirSpawns_WithTheLaunchVelocity_NotWhereTheClientLeftThem()
        {
            ServerSimulation server = Started(ArenaConfig.Defaults());
            Assert.AreEqual(MatchPhase.Running, server.Match.Phase);
            ArenaConfig arena = ArenaConfig.Defaults();
            for (int peer = 1; peer <= 2; peer++)
            {
                arena.SpawnPose(peer - 1, 2, out float2 p, out float r);
                Assert.AreEqual(p.x, server.Ships[peer].Position.x, 1e-3f);
                Assert.AreEqual(p.y, server.Ships[peer].Position.y, 1e-3f);
                Assert.AreEqual(r, server.Ships[peer].Rotation, 1e-3f);
                float2 v = arena.SpawnVelocity(peer - 1, 2);
                Assert.AreEqual(v.x, server.Ships[peer].Velocity.x, 1e-4f);
                Assert.AreEqual(v.y, server.Ships[peer].Velocity.y, 1e-4f);
                Assert.AreEqual(arena.SpawnSpeed, math.length(server.Ships[peer].Velocity), 1e-3f);
            }
            Assert.GreaterOrEqual(math.distance(server.Ships[1].Position, server.Ships[2].Position), 120f);
        }

        [Test]
        public void ReadyUpMatch_PlacesShipsWhenTheMatchStarts()
        {
            var server = new ServerSimulation(new ServerOutbox()) { TickRate = 50f, MatchFeed = new MatchEndFeed(), RequireReady = true, ReconnectGraceSeconds = 0f };
            server.ConfigureDemoArena();
            server.Join(1, Ship());
            server.Join(2, Ship());
            // A client may park its ship anywhere through the build phase; the match start puts it back on its spawn.
            server.Ships[1].Position = new float2(3f, 0f);
            server.Ships[2].Position = new float2(-3f, 0f);
            server.SetReady(1, true);
            server.SetReady(2, true);
            Assert.AreEqual(MatchPhase.Running, server.Match.Phase);
            Assert.GreaterOrEqual(math.distance(server.Ships[1].Position, server.Ships[2].Position), 120f);
        }

        [Test]
        public void SpawnedShips_AreSafeFromThePlanetsForTheWholeOpening()
        {
            ServerSimulation server = Started(ArenaConfig.Defaults());
            // Playtest: an idle ship fell onto the big planet and rested on it by 25 s. The launch velocity arcs it up and over
            // the arena instead; 90 s of nobody flying must stay out of every atmosphere and at least 12 units off every surface.
            for (int i = 0; i < 50 * 90; i++)
            {
                server.Tick();
                if (i % 25 != 0) continue;
                for (int peer = 1; peer <= 2; peer++)
                {
                    PlanetProximity prox = server.Ships[peer].QueryPlanetProximity();
                    Assert.AreEqual(0f, prox.AtmosphereDensity, "peer " + peer + " drifted into an atmosphere at " + i / 50f + " s");
                    Assert.Greater(prox.Altitude, 12f, "peer " + peer + " drifted close to a surface at " + i / 50f + " s");
                }
            }
            for (int peer = 1; peer <= 2; peer++)
                foreach (var kv in server.Ships[peer].Grid.All) Assert.AreEqual(0, kv.Value.Damage, "no crash damage in the opening");
        }

        [Test]
        public void LaunchVelocity_IsMirroredAndLeansAwayFromTheCentre_AndZeroForTheLegacyRing()
        {
            ArenaConfig arena = ArenaConfig.Defaults();
            float2 a = arena.SpawnVelocity(0, 2), b = arena.SpawnVelocity(1, 2);
            Assert.AreEqual(arena.SpawnSpeed, math.length(a), 1e-3f);
            Assert.AreEqual(a.x, -b.x, 1e-4f, "mirrored sideways");
            Assert.AreEqual(a.y, b.y, 1e-4f, "same climb");
            Assert.Greater(a.y, 0f, "climbs away from the planets");
            Assert.Less(a.x, 0f, "slot 0 leans away from the centre");
            var atRest = new ArenaConfig(arena.Bodies, 12f, 30f, 2f, 0.18f, null, null, null, null, 130f, -20f);
            Assert.AreEqual(0f, math.length(atRest.SpawnVelocity(0, 2)));
            var legacy = new ArenaConfig(arena.Bodies, 12f, 30f, 2f, 0.18f, null, null, null, null, 0f, 0f, 4f, 112f);
            Assert.AreEqual(0f, math.length(legacy.SpawnVelocity(0, 2)));
        }

        [Test]
        public void RestingSpawnVelocityZero_FallsOntoThePlanetWithin30Seconds_TheProblemTheLaunchFixes()
        {
            ArenaConfig d = ArenaConfig.Defaults();
            var still = new ArenaConfig(d.Bodies, 12f, 30f, 2f, 0.18f, null, null, null, null, 130f, -20f);
            ServerSimulation server = Started(still);
            bool reached = false;
            for (int i = 0; i < 50 * 30 && !reached; i++)
            {
                server.Tick();
                reached = server.Ships[1].QueryPlanetProximity().Altitude < 2f;
            }
            Assert.IsTrue(reached, "an idle ship spawned at rest lands on the planet");
        }

        [Test]
        public void LegacyRing_StillAvailableWithoutASeparation()
        {
            var legacy = new ArenaConfig(ArenaConfig.Defaults().Bodies, 12f, 30f, 2f, 0.18f);
            Assert.AreEqual(0f, legacy.SpawnSeparation);
            ServerSimulation server = Started(legacy);
            Assert.Less(math.distance(server.Ships[1].Position, server.Ships[2].Position), 20f);
        }

        [Test]
        public void Loader_ReadsSeparation_AndOldRingKeysKeepTheOldRing()
        {
            Assert.AreEqual(ArenaConfig.DefaultSpawnSeparation, ArenaConfigLoader.Parse("{}").Value.SpawnSeparation);
            Assert.AreEqual(150f, ArenaConfigLoader.Parse("{\"spawn\":{\"separation\":150}}").Value.SpawnSeparation);
            Assert.AreEqual(0f, ArenaConfigLoader.Parse("{\"spawn\":{\"altitude\":40}}").Value.SpawnSeparation);
            Assert.AreEqual(ArenaConfig.DefaultSpawnSpeed, ArenaConfigLoader.Parse("{}").Value.SpawnSpeed);
            ArenaConfig tuned = ArenaConfigLoader.Parse("{\"spawn\":{\"speed\":2.5,\"launchDegrees\":90}}").Value;
            Assert.AreEqual(2.5f, tuned.SpawnSpeed);
            Assert.AreEqual(90f, tuned.SpawnLaunchDegrees);
            Assert.AreEqual(ArenaConfig.DefaultSpawnSeparation, tuned.SpawnSeparation, "speed alone keeps the match spawn line");
            Assert.IsFalse(ArenaConfigLoader.Parse("{\"spawn\":{\"speed\":-1}}").IsOk);
            Assert.IsFalse(ArenaConfigLoader.Parse("{\"spawn\":{\"separation\":-1}}").IsOk);
        }

        [Test]
        public void CoreAndArmor_TakeLessFromACannonRoundThanHull()
        {
            var server = new ServerSimulation(new ServerOutbox());
            Assert.AreEqual(25, server.ScaledImpactDamage(BlockTypes.Hull, 25));
            Assert.AreEqual(13, server.ScaledImpactDamage(BlockTypes.Core, 25));
            Assert.AreEqual(15, server.ScaledImpactDamage(BlockTypes.Armor, 25));
            Assert.AreEqual(1, server.ScaledImpactDamage(BlockTypes.Core, 1));
            Assert.AreEqual(0, server.ScaledImpactDamage(BlockTypes.Core, 0));
            // Hits to kill: hull 11 (255 / 25), core 20.
            Assert.AreEqual(20, (int)math.ceil(255f / server.ScaledImpactDamage(BlockTypes.Core, 25)));
        }
    }
}
