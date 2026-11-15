using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Ship;
using Hullbreach.World;

namespace Hullbreach.World.Tests
{
    // Planets as terrain: with the default arena bodies and the starter ship, a low orbit is reachable and
    // safe, a ship can lift off, and escape is possible. Numbers are written to the test log.
    public class PlanetOrbitAnalysisTests
    {
        const float Dt = 1f / 50f;

        // The 9 block starter ship of the online demo: two thrusters, one retro, two fins, a cannon.
        static ShipBody StarterShip(GravityField field)
        {
            var ship = new ShipBody { Gravity = field };
            void Add(int x, int y, byte type, byte mods = 0) => ship.Grid.TryAdd(BlockKey.Pack(x, y), new Block(type, mods));
            Add(0, 0, BlockTypes.Core); Add(0, 1, BlockTypes.Hull); Add(0, -1, BlockTypes.Hull);
            Add(-1, -1, BlockTypes.Thruster); Add(1, -1, BlockTypes.Thruster); Add(0, -2, BlockTypes.RetroThruster);
            Add(0, 2, BlockTypes.Cannon); Add(-1, 1, BlockTypes.Fin, 3); Add(1, 1, BlockTypes.Fin, 1);
            ship.RebuildDerivedViews();
            return ship;
        }

        static GravityField DefaultArena()
        {
            var field = new GravityField();
            field.Add(new GravityBody(new float2(0f, -60f), 900f, 18f, 0.2f));
            field.Add(new GravityBody(new float2(45f, 20f), 120f, 6f, 0.2f));
            return field;
        }

        [Test]
        public void StableLowOrbit_IsSlowerThanAnyAtmosphereEffect_ForEveryDefaultBody()
        {
            var field = DefaultArena();
            var cfg = PlanetCrashConfig.Default();
            for (int i = 0; i < field.PermanentCount; i++)
            {
                field.TryGetPermanent(i, out var body);
                float lowest = body.Radius + 3f;   // three units above the surface
                Assert.IsTrue(OrbitHelper.TryCircularOrbitVelocity(field, i, body.Position + new float2(0f, lowest), out var v));
                float speed = math.length(v);
                TestContext.Out.WriteLine($"body {i}: mu {body.Mu} radius {body.Radius}: circular speed at altitude 3 = {speed:F2} u/s " +
                    $"(atmosphere safe {cfg.AtmosphereSafeSpeed})");
                Assert.Less(speed, cfg.AtmosphereSafeSpeed, "a circular orbit never meets drag or heating");
                Assert.Greater(speed, 1f);
            }
        }

        [Test]
        public void StarterShip_CanLiftOffFromEveryBody()
        {
            var field = DefaultArena();
            var ship = StarterShip(field);
            float accel = 2f * ship.ThrustPerBlock / ship.Grid.Mass.Total;
            for (int i = 0; i < field.PermanentCount; i++)
            {
                field.TryGetPermanent(i, out var body);
                float surfaceGravity = GravityField.AccelerationMagnitude(body, math.max(body.Radius, body.SoftRadius));
                TestContext.Out.WriteLine($"body {i}: peak pull {surfaceGravity:F2} u/s2 vs ship thrust {accel:F2} u/s2");
                Assert.Greater(accel, surfaceGravity * 1.2f, "the thrusters beat gravity by at least 20 percent");
            }
        }

        [Test]
        public void StarterShip_CanEscapeFromALowOrbit_WithPrograde_ThrustAndNeverTouchesDown()
        {
            var field = DefaultArena();
            var ship = StarterShip(field);
            var body = default(GravityBody);
            field.TryGetPermanent(0, out body);
            float2 start = body.Position + new float2(0f, body.Radius + 8f);
            Assert.IsTrue(OrbitHelper.TryCircularOrbitVelocity(field, 0, start, out var v));
            ship.Position = start;
            ship.Velocity = v;
            float minAltitude = float.MaxValue;
            float escapeTime = -1f;
            for (int i = 0; i < 50 * 60; i++)
            {
                // Point the nose along the velocity: a prograde burn.
                float2 dir = math.normalizesafe(ship.Velocity);
                ship.Rotation = math.atan2(dir.y, dir.x) - math.PI * 0.5f;
                ship.AngularVelocity = 0f;
                ship.Step(new ShipInput(1f, 0f, false), Dt);
                float2 com = ship.LocalToWorld(ship.Grid.Mass.CenterOfMass);
                float r = math.length(com - body.Position);
                minAltitude = math.min(minAltitude, r - body.Radius);
                float energy = 0.5f * math.lengthsq(ship.Velocity) - body.Mu / r;
                if (energy > 0f) { escapeTime = i * Dt; break; }
            }
            TestContext.Out.WriteLine($"prograde burn from a circular orbit at altitude 8: escape energy after {escapeTime:F1} s, lowest altitude {minAltitude:F1}");
            Assert.Greater(escapeTime, 0f, "reaches escape energy within a minute");
            Assert.Less(escapeTime, 40f);
            Assert.Greater(minAltitude, 1f, "a prograde burn never dips into the planet");
            for (int y = -1; y <= 1; y++) { ship.Grid.TryGet(BlockKey.Pack(0, y), out var b); Assert.AreEqual(0, b.Damage); }
        }

        [Test]
        public void StarterShip_CanBrakeIntoALandingSlowEnoughToBeSafe()
        {
            // From 45 units up, falling and thrusting hard against the fall: the retro-less stack
            // slows to below the safe landing speed with thrust to spare.
            var field = DefaultArena();
            field.TryGetPermanent(0, out var body);
            var ship = StarterShip(field);
            ship.Position = body.Position + new float2(0f, body.Radius + 45f);
            // Nose up: the main thrusters push away from the planet.
            ship.Velocity = new float2(0f, -5f);
            float worstClosing = 0f;
            for (int i = 0; i < 50 * 20; i++)
            {
                var p = ship.QueryPlanetProximity();
                // Burn when the HUD query says the stop distance is nearly the whole gap.
                bool burn = p.MustBrake;
                ship.Step(new ShipInput(burn ? 1f : 0f, 0f, false), Dt);
                worstClosing = math.max(worstClosing, ship.ContactsThisStep.Count > 0 ? ship.ContactsThisStep[0].speed : 0f);
            }
            TestContext.Out.WriteLine($"suicide burn landing: worst contact speed {worstClosing:F2} u/s (safe {ship.PlanetCrash.SafeLandingSpeed})");
            Assert.Less(worstClosing, ship.PlanetCrash.SafeLandingSpeed + 1.5f);
            ship.Grid.TryGet(BlockKey.Pack(0, 0), out var core);
            Assert.AreEqual(0, core.Damage);
        }
    }
}
