using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Ship;

namespace Hullbreach.Ship.Tests
{
    // Numeric bounds on how the ship feels: spin settles, steer answers within a few ticks, thrust bites fast and
    // tails off slower, the brake stops without reversing, a fire press survives a short cooldown. Each test logs
    // the old (Legacy) number next to the new one for the before/after table.
    public class FlightFeelTests
    {
        const float Dt = 1f / 50f;

        // The 9 block starter ship of the online demo: two thrusters, one retro, two fins, a cannon.
        static ShipBody Starter(FlightTuning tuning, bool retro = true)
        {
            var ship = new ShipBody { Tuning = tuning };
            void Add(int x, int y, byte type, byte mods = 0) => ship.Grid.TryAdd(BlockKey.Pack(x, y), new Block(type, mods));
            Add(0, 0, BlockTypes.Core); Add(0, 1, BlockTypes.Hull); Add(0, -1, BlockTypes.Hull);
            Add(-1, -1, BlockTypes.Thruster); Add(1, -1, BlockTypes.Thruster); if (retro) Add(0, -2, BlockTypes.RetroThruster);
            Add(0, 2, BlockTypes.Cannon); Add(-1, 1, BlockTypes.Fin, 3); Add(1, 1, BlockTypes.Fin, 1);
            ship.RebuildDerivedViews();
            return ship;
        }

        // A long, heavy hull with the same parts: much more inertia than the starter.
        static ShipBody Heavy(FlightTuning tuning)
        {
            var ship = Starter(tuning);
            for (int x = -6; x <= 6; x++)
                if (x != 0) ship.Grid.TryAdd(BlockKey.Pack(x, 0), new Block(BlockTypes.Hull));
            ship.RebuildDerivedViews();
            return ship;
        }

        static int TicksUntil(ShipBody ship, ShipInput input, System.Func<ShipBody, bool> done, int max = 2000)
        {
            for (int i = 1; i <= max; i++)
            {
                ship.Step(input, Dt);
                if (done(ship)) return i;
            }
            return -1;
        }

        [Test]
        public void Defaults_AreValid_AndLegacyIsTheOldPhysics()
        {
            Assert.IsNull(FlightTuning.Default().Validate());
            Assert.IsNull(FlightTuning.Legacy().Validate());
            Assert.IsFalse(FlightTuning.Legacy().AssistEnabled);
            var bad = FlightTuning.Default();
            bad.FireBufferSeconds = 2f;
            Assert.IsNotNull(bad.Validate());
        }

        [Test]
        public void SpinSettlesQuicklyWithoutOvershoot_WhenSteerIsReleased()
        {
            var old = Starter(FlightTuning.Legacy());
            old.AngularVelocity = 1f;
            int oldTicks = TicksUntil(old, default, s => math.abs(s.AngularVelocity) < 0.05f, 500);

            var ship = Starter(FlightTuning.Default());
            ship.AngularVelocity = 1f;
            float lowest = 1f;
            int ticks = TicksUntil(ship, default, s => { lowest = math.min(lowest, s.AngularVelocity); return math.abs(s.AngularVelocity) < 0.05f; }, 500);
            TestContext.Out.WriteLine($"null a 1 rad/s spin: before {(oldTicks < 0 ? "never (>10 s)" : oldTicks * Dt + " s")}, after {ticks * Dt:F2} s, overshoot {-lowest:F3} rad/s");
            Assert.AreEqual(-1, oldTicks, "legacy physics never stops the spin");
            Assert.Greater(ticks, 0);
            Assert.LessOrEqual(ticks * Dt, 1.0f, "settles within one second");
            Assert.Greater(lowest, -0.05f, "no overshoot past zero");
        }

        [Test]
        public void FullSteer_ReachesTheRateCapFast_AndStaysUnderIt_ThenHoldsHeadingOnRelease()
        {
            var ship = Starter(FlightTuning.Default());
            float cap = ship.Tuning.MaxRateFor(ship.Grid.Mass.InertiaAboutCenterOfMass);
            int first = -1, toOne = -1;
            float peak = 0f;
            for (int i = 1; i <= 100; i++)
            {
                ship.Step(new ShipInput(0f, -1f, false), Dt);
                float w = math.abs(ship.AngularVelocity);
                peak = math.max(peak, w);
                if (first < 0 && w > 0.05f) first = i;
                if (toOne < 0 && w >= 1f) toOne = i;
            }
            TestContext.Out.WriteLine($"steer latency: before 20 ticks, after {first} ticks; to 1 rad/s {toOne * Dt:F2} s; peak {peak:F2} rad/s (cap {cap:F2})");
            Assert.LessOrEqual(first, 3, "the first response is within 3 ticks (60 ms)");
            Assert.LessOrEqual(toOne * Dt, 0.8f);
            Assert.LessOrEqual(peak, cap * 1.1f, "never beyond the rate cap");
            Assert.GreaterOrEqual(peak, cap * 0.9f);

            // Release: the spin dies and the heading is held, no endless rotation.
            int stop = TicksUntil(ship, default, s => math.abs(s.AngularVelocity) < 0.02f, 500);
            Assert.Greater(stop, 0);
            Assert.LessOrEqual(stop * Dt, 1.5f);
            float held = ship.Rotation;
            for (int i = 0; i < 100; i++) ship.Step(default, Dt);
            Assert.AreEqual(held, ship.Rotation, 0.05f, "heading holds once the spin is gone");
        }

        [Test]
        public void ATap_LeavesNoResidualSpin()
        {
            var old = Starter(FlightTuning.Legacy());
            var ship = Starter(FlightTuning.Default());
            for (int i = 0; i < 10; i++) { old.Step(new ShipInput(0f, 1f, false), Dt); ship.Step(new ShipInput(0f, 1f, false), Dt); }
            for (int i = 0; i < 200; i++) { old.Step(default, Dt); ship.Step(default, Dt); }
            TestContext.Out.WriteLine($"0.2 s tap: residual spin before {old.AngularVelocity:F3} rad/s, after {ship.AngularVelocity:F4} rad/s");
            Assert.Greater(math.abs(old.AngularVelocity), 0.02f, "legacy keeps spinning");
            Assert.Less(math.abs(ship.AngularVelocity), 0.01f);
        }

        [Test]
        public void HeavierShips_TurnSlower_ButNeverBelowTheFloor()
        {
            var tuning = FlightTuning.Default();
            float light = tuning.MaxRateFor(Starter(tuning).Grid.Mass.InertiaAboutCenterOfMass);
            var big = Heavy(tuning);
            float heavy = tuning.MaxRateFor(big.Grid.Mass.InertiaAboutCenterOfMass);
            TestContext.Out.WriteLine($"rate cap: starter {light:F2} rad/s, 13 wide hull {heavy:F2} rad/s (inertia {big.Grid.Mass.InertiaAboutCenterOfMass:F0})");
            Assert.Less(heavy, light);
            Assert.GreaterOrEqual(heavy, tuning.MinRate);
            Assert.LessOrEqual(light, tuning.MaxRate);
            float peak = 0f;
            for (int i = 0; i < 400; i++) { big.Step(new ShipInput(0f, 1f, false), Dt); peak = math.max(peak, math.abs(big.AngularVelocity)); }
            Assert.LessOrEqual(peak, heavy * 1.1f);
            Assert.Greater(peak, heavy * 0.5f, "a heavy ship still turns");
        }

        [Test]
        public void HoldingAssistOff_GivesRawSpin()
        {
            var ship = Starter(FlightTuning.Default());
            ship.AngularVelocity = 1f;
            for (int i = 0; i < 100; i++) ship.Step(new ShipInput(0f, 0f, false, assistOff: true), Dt);
            Assert.AreEqual(1f, ship.AngularVelocity, 0.02f, "no gyro, no damping: the spin stays");
            Assert.IsFalse(ship.AssistActive);
            ship.Step(default, Dt);
            Assert.IsTrue(ship.AssistActive, "letting go of the key brings the gyro back");
        }

        [Test]
        public void AssistIsStableAtOtherTickRates()
        {
            foreach (float rate in new[] { 30f, 60f, 120f })
            {
                float dt = 1f / rate;
                var ship = Starter(FlightTuning.Default());
                ship.AngularVelocity = 2f;
                float lowest = 2f;
                for (int i = 0; i < (int)(rate * 3f); i++) { ship.Step(default, dt); lowest = math.min(lowest, ship.AngularVelocity); }
                Assert.Greater(lowest, -0.05f, $"{rate} Hz overshoots");
                Assert.Less(math.abs(ship.AngularVelocity), 0.02f, $"{rate} Hz does not settle");
            }
        }

        [Test]
        public void Throttle_BitesFast_AndTailsOffSlower()
        {
            int Key(ShipBody s) => s.ThrusterKeys[0];
            var old = Starter(FlightTuning.Legacy());
            int oldOn = TicksUntil(old, new ShipInput(1f, 0f, false), s => s.Throttle(Key(s)) >= 0.9f);
            var ship = Starter(FlightTuning.Default());
            int on = TicksUntil(ship, new ShipInput(1f, 0f, false), s => s.Throttle(Key(s)) >= 0.9f);
            int off = TicksUntil(ship, default, s => s.Throttle(Key(s)) <= 0.1f);
            TestContext.Out.WriteLine($"thrust to 90%: before {oldOn} ticks, after {on} ticks; off to 10%: before {oldOn}, after {off} ticks");
            Assert.LessOrEqual(on * Dt, 0.45f, "on within 0.45 s");
            Assert.Less(on, oldOn);
            Assert.Greater(off, on, "off is slower than on");
            Assert.LessOrEqual(off, on * 2, "but only slightly slower");
            Assert.GreaterOrEqual((float)off / on, 1.2f);
        }

        [Test]
        public void Brake_FromTwentyUnitsPerSecond_StopsInAboutTwoSeconds()
        {
            var ship = Starter(FlightTuning.Default());
            ship.Velocity = new float2(20f, 0f);
            int ticks = TicksUntil(ship, new ShipInput(-1f, 0f, false), s => s.Velocity.x <= 0.2f, 2000);
            TestContext.Out.WriteLine($"full brake from 20 u/s: {ticks * Dt:F2} s to stop");
            Assert.Greater(ticks, 0);
            Assert.LessOrEqual(ticks * Dt, 2.5f, "a full brake from 20 u/s stops in about 2 s");
        }

        [Test]
        public void Brake_StopsTheShipWithoutReversingIt()
        {
            // Sliding sideways: the retro thruster pushes along the nose, so only the assist acts on x.
            var old = Starter(FlightTuning.Legacy());
            old.Velocity = new float2(3f, 0f);
            for (int i = 0; i < 300; i++) old.Step(new ShipInput(-1f, 0f, false), Dt);

            var ship = Starter(FlightTuning.Default());
            ship.Velocity = new float2(3f, 0f);
            float lowestX = 3f;
            int ticks = TicksUntil(ship, new ShipInput(-1f, 0f, false), s => { lowestX = math.min(lowestX, s.Velocity.x); return s.Velocity.x <= 0.001f; }, 2000);
            TestContext.Out.WriteLine($"brake from 3 u/s sideways: before vx {old.Velocity.x:F2} after 6 s, after {ticks * Dt:F2} s to stop, lowest vx {lowestX:F3}");
            Assert.AreEqual(3f, old.Velocity.x, 1e-3f, "legacy S does nothing sideways");
            Assert.Greater(ticks, 0);
            Assert.LessOrEqual(ticks * Dt, 5f);
            Assert.GreaterOrEqual(lowestX, -0.001f, "never reverses");
            for (int i = 0; i < 100; i++) ship.Step(new ShipInput(-1f, 0f, false), Dt);
            Assert.AreEqual(0f, ship.Velocity.x, 1e-3f, "stays stopped while S is held");
        }

        [Test]
        public void Brake_NeedsARetroThruster_AndIsRelativeToTheReferenceVelocity()
        {
            var ship = Starter(FlightTuning.Default(), retro: false);
            ship.Velocity = new float2(3f, 0f);
            for (int i = 0; i < 100; i++) ship.Step(new ShipInput(-1f, 0f, false), Dt);
            Assert.AreEqual(3f, ship.Velocity.x, 1e-3f, "no retro, no brake");

            var moving = Starter(FlightTuning.Default());
            moving.BrakeReferenceVelocity = new float2(2f, 0f);
            moving.Velocity = new float2(2f, 0f);
            for (int i = 0; i < 50; i++) moving.Step(new ShipInput(-1f, 0f, false), Dt);
            Assert.AreEqual(2f, moving.Velocity.x, 1e-3f, "matching the reference body leaves nothing to kill");
        }

        [Test]
        public void FirePressDuringCooldown_StillFiresWhenTheCannonIsReady()
        {
            int Shots(FlightTuning t, int pressTick)
            {
                var ship = Starter(t);
                int shots = 0;
                for (int i = 0; i < 40; i++)
                {
                    bool press = i == 0 || i == pressTick;
                    ship.Step(new ShipInput(0f, 0f, press), Dt);
                    shots += ship.PendingShots.Count;
                    ship.PendingShots.Clear();
                }
                return shots;
            }
            // Cooldown is 0.35 s = 17.5 ticks. A press 12 ticks in has 0.11 s left: inside the 0.15 s buffer.
            int before = Shots(FlightTuning.Legacy(), 12);
            int after = Shots(FlightTuning.Default(), 12);
            // A press 3 ticks in has 0.29 s left: too early, the buffer expires and the press is lost.
            int tooEarly = Shots(FlightTuning.Default(), 3);
            TestContext.Out.WriteLine($"press 0.24 s after a shot: shots before {before}, after {after}; press 0.06 s after: {tooEarly}");
            Assert.AreEqual(1, before, "legacy loses the press");
            Assert.AreEqual(2, after);
            Assert.AreEqual(1, tooEarly, "the buffer is short, not a queue");
        }

        [Test]
        public void OneHeldPress_NeverFiresTwice()
        {
            var ship = Starter(FlightTuning.Default());
            ship.Step(new ShipInput(0f, 0f, true), Dt);
            int shots = ship.PendingShots.Count;
            ship.PendingShots.Clear();
            for (int i = 0; i < 8; i++) { ship.Step(default, Dt); shots += ship.PendingShots.Count; ship.PendingShots.Clear(); }
            Assert.AreEqual(1, shots, "a shot clears the buffer");
        }

        [Test]
        public void SameInputs_GiveBitIdenticalResults()
        {
            ShipBody Run()
            {
                var ship = Starter(FlightTuning.Default());
                for (int i = 0; i < 1000; i++)
                {
                    float steer = math.sin(i * 0.05f);
                    float thrust = i % 200 < 120 ? 1f : (i % 200 < 160 ? -1f : 0f);
                    ship.Step(new ShipInput(thrust, steer, i % 37 == 0, i % 211 < 20), Dt);
                    ship.PendingShots.Clear();
                }
                return ship;
            }
            var a = Run();
            var b = Run();
            Assert.AreEqual(a.Position.x, b.Position.x);
            Assert.AreEqual(a.Position.y, b.Position.y);
            Assert.AreEqual(a.Rotation, b.Rotation);
            Assert.AreEqual(a.AngularVelocity, b.AngularVelocity);
        }
    }
}
