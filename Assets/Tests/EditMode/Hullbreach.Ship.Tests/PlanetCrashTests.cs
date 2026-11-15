using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Ship;
using Hullbreach.World;

namespace Hullbreach.Ship.Tests
{
    // Planet crash rules: safe-landing threshold, damage only where the ship touches, the core spared
    // unless the hit is catastrophic, no tunnelling, no jitter, no energy gain, the atmosphere band,
    // the HUD proximity query and stable orbits.
    public class PlanetCrashTests
    {
        const float Dt = 1f / 50f;
        const float PlanetRadius = 10f;

        static GravityField Field(float mu = 300f, float radius = PlanetRadius, float restitution = 0.5f)
        {
            var field = new GravityField();
            field.Add(new GravityBody(float2.zero, mu, radius, restitution));
            return field;
        }

        // A vertical column. coreY picks which block is the core; the others are hull. Local +y is up in the world.
        static ShipBody Column(GravityField field, int height, int coreY)
        {
            var ship = new ShipBody { Gravity = field };
            for (int y = 0; y < height; y++)
                ship.Grid.TryAdd(BlockKey.Pack(0, y), new Block(y == coreY ? BlockTypes.Core : BlockTypes.Hull, 0));
            ship.RebuildDerivedViews();
            return ship;
        }

        // Puts the lowest block `gap` above the contact shell, directly above the planet, moving straight down at `speed`.
        static void DropFrom(ShipBody ship, float gap, float speed)
        {
            ship.Rotation = 0f;
            ship.Position = new float2(-0.5f, PlanetRadius + ship.ContactClearance + gap - 0.5f + 0.5f);
            ship.Velocity = new float2(0f, -speed);
        }

        static int DamageAt(ShipBody ship, int y)
        {
            Assert.IsTrue(ship.Grid.TryGet(BlockKey.Pack(0, y), out Block b), "block " + y + " exists");
            return b.Damage;
        }

        static void Run(ShipBody ship, int ticks)
        {
            for (int i = 0; i < ticks; i++) ship.Step(default, Dt);
        }

        [Test]
        public void EffectiveRestitution_FadesFromFullAtSafeSpeedToNoneAtCatastrophic()
        {
            var cfg = PlanetCrashConfig.Default();
            Assert.AreEqual(0.5f, cfg.EffectiveRestitution(0.5f, cfg.SafeLandingSpeed), 1e-5f);
            Assert.AreEqual(0.25f, cfg.EffectiveRestitution(0.5f, (cfg.SafeLandingSpeed + cfg.CatastrophicSpeed) * 0.5f), 1e-5f);
            Assert.AreEqual(0f, cfg.EffectiveRestitution(0.5f, cfg.CatastrophicSpeed));
            Assert.AreEqual(0f, cfg.EffectiveRestitution(0.5f, 99f));
        }

        [Test]
        public void VeryHardLanding_DeadStopsWithRealDamage_AndDoesNotEscapeThePlanet()
        {
            // A 3x3 slab landing at 35 u/s on a Demo-sized planet (mu 900, r 18, restitution 0.2) used to rebound
            // faster than the escape speed and leave the arena with every block intact.
            var field = new GravityField();
            field.Add(new GravityBody(new float2(0f, -60f), 900f, 18f, 0.2f));
            var ship = new ShipBody { Gravity = field };
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    ship.Grid.TryAdd(BlockKey.Pack(x, y), new Block(x == 0 && y == 0 ? BlockTypes.Core : BlockTypes.Hull, 0));
            ship.RebuildDerivedViews();
            ship.Position = new float2(0f, -36f);
            ship.Velocity = new float2(0f, -35f);
            float maxUp = 0f;
            for (int i = 0; i < 1000; i++) { ship.Step(default, Dt); maxUp = math.max(maxUp, ship.Velocity.y); }
            Assert.Less(maxUp, 3f, "no meaningful rebound");
            Assert.Less(ship.Position.y, 0f, "still near the planet");
            int total = 0;
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    if (ship.Grid.TryGet(BlockKey.Pack(x, y), out Block b)) total += b.Damage;
            Assert.Greater(total, 300, "a 35 u/s crash does real damage");
        }

        [Test]
        public void GentleTouchdown_BelowTheSafeSpeed_DamagesNothing()
        {
            var ship = Column(Field(mu: 1f), 3, coreY: 0);
            DropFrom(ship, 0.1f, 3.5f);
            Run(ship, 100);
            for (int y = 0; y < 3; y++) Assert.AreEqual(0, DamageAt(ship, y), "block " + y);
        }

        [Test]
        public void HardLanding_DamagesOnlyTheTouchingBlock_AndScalesWithSpeed()
        {
            int Hit(float speed)
            {
                var ship = Column(Field(mu: 1f), 4, coreY: 3);
                DropFrom(ship, 0.1f, speed);
                Run(ship, 40);
                Assert.AreEqual(0, DamageAt(ship, 1), "block above the contact is spared");
                Assert.AreEqual(0, DamageAt(ship, 3), "core on the far side is spared");
                return DamageAt(ship, 0);
            }
            int slow = Hit(8f), fast = Hit(14f);
            Assert.Greater(slow, 0);
            Assert.Greater(fast, slow, "faster means more damage");
        }

        [Test]
        public void ModerateHit_OnTheCore_NeverKills_AndIsCapped()
        {
            var cfg = PlanetCrashConfig.Default();
            var ship = Column(Field(mu: 1f), 2, coreY: 0);
            DropFrom(ship, 0.1f, cfg.CatastrophicSpeed - 2f);
            Run(ship, 40);
            int core = DamageAt(ship, 0);
            Assert.Greater(core, 0);
            Assert.LessOrEqual(core, (int)cfg.CoreMaxDamagePerImpact, "one non catastrophic impact is capped");
        }

        [Test]
        public void CatastrophicHit_OnTheCore_Kills_ButOnlyWhenTheCoreIsOnTheContactSide()
        {
            var cfg = PlanetCrashConfig.Default();
            var onSide = Column(Field(mu: 1f), 2, coreY: 0);
            DropFrom(onSide, 0.1f, cfg.CatastrophicSpeed + 10f);
            Run(onSide, 40);
            Assert.AreEqual(255, DamageAt(onSide, 0), "core touching the surface at catastrophic speed");

            var farSide = Column(Field(mu: 1f), 3, coreY: 2);
            DropFrom(farSide, 0.1f, cfg.CatastrophicSpeed + 10f);
            Run(farSide, 40);
            Assert.AreEqual(0, DamageAt(farSide, 2), "core away from the contact is never touched");
        }

        [Test]
        public void Graze_AtHighTangentialSpeed_IsNotAnInstantKill()
        {
            var ship = Column(Field(mu: 1f), 2, coreY: 0);
            // Slide along the surface at speed 60 with a hair of downward speed.
            ship.Position = new float2(-0.5f, PlanetRadius + ship.ContactClearance - 0.4f);
            ship.Velocity = new float2(60f, -0.5f);
            Run(ship, 5);
            Assert.AreEqual(0, DamageAt(ship, 0), "the core takes nothing from a graze");
            Assert.Less(DamageAt(ship, 1), 20, "air friction at most scuffs the leading block");
        }

        [Test]
        public void VeryFastShip_NeverTunnelsThroughASmallPlanet()
        {
            // 1000 u/s is 20 units per tick: more than the planet is wide.
            float FinalY(int maxSubsteps)
            {
                var ship = Column(Field(mu: 1f, radius: 3f), 1, coreY: 0);
                var cfg = ship.PlanetCrash;
                cfg.MaxSubsteps = maxSubsteps;
                ship.PlanetCrash = cfg;
                ship.Position = new float2(-0.5f, 14f);
                ship.Velocity = new float2(0f, -1000f);
                Run(ship, 30);
                return ship.LocalToWorld(BlockGrid.CenterOf(BlockKey.Pack(0, 0))).y;
            }
            Assert.Greater(FinalY(16), 0f, "swept: the block stays on the side it came from");
            Assert.Less(FinalY(1), 0f, "negative control: one hop per tick skips straight through (this test can fail)");
        }

        [Test]
        public void Bounce_NeverGainsEnergy_AndSettlesWithoutBuzzing()
        {
            var field = Field(mu: 100f, restitution: 0.9f);   // asks for more bounce than the cap allows
            var ship = Column(field, 1, coreY: 0);
            DropFrom(ship, 6f, 0f);
            float startEnergy = Energy(ship, field);
            float worst = float.MinValue;
            for (int i = 0; i < 1500; i++)
            {
                ship.Step(default, Dt);
                worst = math.max(worst, Energy(ship, field) - startEnergy);
            }
            Assert.LessOrEqual(worst, 0.5f, "mechanical energy never rises above the drop");

            // After 30 s it lies still: no jitter, and the ship stays on the surface.
            float2 p0 = ship.Position;
            float maxSpeed = 0f, maxMove = 0f;
            for (int i = 0; i < 200; i++)
            {
                ship.Step(default, Dt);
                maxSpeed = math.max(maxSpeed, math.length(ship.Velocity));
                maxMove = math.max(maxMove, math.length(ship.Position - p0));
            }
            Assert.Less(maxSpeed, 0.1f, "resting speed");
            Assert.Less(maxMove, 0.02f, "resting drift");
            Assert.AreEqual(0, DamageAt(ship, 0));
        }

        static float Energy(ShipBody ship, GravityField field)
        {
            float2 c = ship.LocalToWorld(ship.Grid.Mass.CenterOfMass);
            float mu = 100f;
            float r = math.max(math.length(c), 1f);
            return 0.5f * math.lengthsq(ship.Velocity) - mu / r;
        }

        [Test]
        public void SameInputs_GiveBitIdenticalResults()
        {
            ShipBody Make()
            {
                var field = Field(mu: 300f);
                var s = Column(field, 3, coreY: 1);
                s.Position = new float2(0f, 20f);
                s.Velocity = new float2(7f, -5f);
                s.AngularVelocity = 0.4f;
                return s;
            }
            var a = Make(); var b = Make();
            for (int i = 0; i < 1000; i++)
            {
                a.Step(new ShipInput(0.5f, 0.2f, false), Dt);
                b.Step(new ShipInput(0.5f, 0.2f, false), Dt);
            }
            Assert.AreEqual(a.Position.x, b.Position.x);
            Assert.AreEqual(a.Position.y, b.Position.y);
            Assert.AreEqual(a.Rotation, b.Rotation);
            Assert.AreEqual(DamageAt(a, 0), DamageAt(b, 0));
        }

        [Test]
        public void Atmosphere_SlowsAFastShip_AndHeatsTheLeadingBlocksButNotTheCore()
        {
            // A big planet keeps a long flat run inside the band; negligible gravity so only the air acts.
            var field = Field(mu: 1f, radius: 200f);
            var ship = new ShipBody { Gravity = field };
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core, 0));
            for (int x = 1; x <= 3; x++) ship.Grid.TryAdd(BlockKey.Pack(x, 0), new Block(BlockTypes.Hull, 0));
            ship.RebuildDerivedViews();
            ship.Position = new float2(0f, 200f + 5f);
            ship.Velocity = new float2(40f, 0f);
            Run(ship, 25);
            Assert.Less(ship.Velocity.x, 40f - 3f, "drag took speed off");
            Assert.Greater(ship.Velocity.x, 8f, "never below the safe speed");
            Assert.Greater(DamageAt(ship, 0, 3), 0, "the leading block heated");
            Assert.AreEqual(0, DamageAt(ship, 0, 1), "blocks behind the front are shielded");
            Assert.AreEqual(0, DamageAt(ship, 0, 0), "the core is never heated");
        }

        static int DamageAt(ShipBody ship, int y, int x)
        {
            Assert.IsTrue(ship.Grid.TryGet(BlockKey.Pack(x, y), out Block b));
            return b.Damage;
        }

        [Test]
        public void Atmosphere_LeavesSlowShipsAndHighAltitudeAlone()
        {
            var slow = Column(Field(mu: 1f), 2, coreY: 0);
            slow.Position = new float2(-0.5f, PlanetRadius + 2f);
            slow.Velocity = new float2(0f, 6f);
            Run(slow, 25);
            Assert.AreEqual(6f, slow.Velocity.y, 2e-2f, "under the safe speed there is no drag");

            var high = Column(Field(mu: 1f), 2, coreY: 0);
            high.Position = new float2(-0.5f, PlanetRadius + 40f);
            high.Velocity = new float2(0f, 30f);
            Run(high, 25);
            Assert.AreEqual(30f, high.Velocity.y, 2e-2f, "above the band there is no drag");
            Assert.AreEqual(0, DamageAt(high, 1));
        }

        [Test]
        public void Proximity_ReportsTimeToImpactSeverityAndSafety()
        {
            var field = Field(mu: 1f);   // near zero gravity, so the textbook gap / speed holds
            var ship = Column(field, 1, coreY: 0);
            ship.Position = new float2(-0.5f, 0f);
            ship.Position = new float2(-0.5f, PlanetRadius + ship.ContactClearance + 30f);
            ship.Velocity = new float2(0f, -20f);

            var p = ship.QueryPlanetProximity();
            Assert.IsTrue(p.HasBody);
            Assert.AreEqual(30f, p.Altitude, 0.6f, "gap to the surface");
            Assert.AreEqual(20f, p.ClosingSpeed, 0.01f);
            Assert.AreEqual(1.5f, p.TimeToImpact, 0.1f);
            Assert.AreEqual(ImpactSeverity.Danger, p.Severity);
            Assert.IsFalse(p.IsSafe);

            ship.Velocity = new float2(0f, 2f);
            p = ship.QueryPlanetProximity();
            Assert.AreEqual(ImpactSeverity.None, p.Severity, "rising: no warning");
            Assert.IsTrue(p.IsSafe);
            Assert.IsTrue(float.IsPositiveInfinity(p.TimeToImpact));

            ship.Velocity = new float2(0f, -3f);
            ship.Position = new float2(-0.5f, PlanetRadius + ship.ContactClearance + 4f);
            p = ship.QueryPlanetProximity();
            Assert.AreEqual(ImpactSeverity.Safe, p.Severity, "a slow approach is a safe landing");
            Assert.IsTrue(p.IsSafe);

            ship.Velocity = new float2(0f, -50f);
            p = ship.QueryPlanetProximity();
            Assert.AreEqual(ImpactSeverity.Catastrophic, p.Severity);
        }

        [Test]
        public void Proximity_WithoutAPlanet_ReportsNothing()
        {
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core, 0));
            ship.RebuildDerivedViews();
            var p = ship.QueryPlanetProximity();
            Assert.IsFalse(p.HasBody);
            Assert.IsTrue(p.IsSafe);
        }

        [Test]
        public void CircularOrbit_InsideTheAtmosphereBand_StaysStableAndUndamaged()
        {
            var field = new GravityField();
            var body = new GravityBody(float2.zero, 900f, 18f, 0.2f);
            field.Add(body);
            var ship = Column(field, 1, coreY: 0);
            // Low orbit, 6 units above the surface: well inside the 9 unit atmosphere band.
            float2 start = new float2(0.5f, 18f + 6.5f);
            ship.Position = start - new float2(0.5f, 0.5f);
            Assert.IsTrue(OrbitHelper.TryCircularOrbitVelocity(field, 0, start, out var v));
            ship.Velocity = v;
            float r0 = math.length(ship.LocalToWorld(ship.Grid.Mass.CenterOfMass));
            float worst = 0f;
            for (int i = 0; i < 3000; i++)
            {
                ship.Step(default, Dt);
                float r = math.length(ship.LocalToWorld(ship.Grid.Mass.CenterOfMass));
                worst = math.max(worst, math.abs(r - r0) / r0);
            }
            Assert.Less(worst, 0.05f, "radius deviation over 60 s");
            Assert.AreEqual(0, DamageAt(ship, 0));
        }
    }
}
