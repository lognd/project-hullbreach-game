using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Ship;

namespace Hullbreach.Ship.Tests
{
    // S39 handling criteria, exercised against the plain-C# sim so they
    // run without a scene.
    public class ShipBodyTests
    {
        const float Tol = 1e-3f;

        static ShipBody OneOffCenterThruster()
        {
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            // Off to one side, facing up (default modifiers = 0).
            ship.Grid.TryAdd(BlockKey.Pack(2, 0), new Block(BlockTypes.Thruster));
            return ship;
        }

        [Test]
        public void OffCenterThruster_ProducesLinearAndAngularAcceleration()
        {
            var ship = OneOffCenterThruster();
            ship.Step(new ShipInput(1f, 0f, false), 1f / 60f);

            Assert.Greater(math.length(ship.LastLinearAcceleration), 0f,
                "an off-center thruster still pushes the whole ship");
            Assert.AreNotEqual(0f, ship.LastAngularAcceleration,
                "a thruster not on the CoM must produce torque: S39 criterion 1");
        }

        [Test]
        public void SymmetricThrusterPair_ProducesNoNetAngularAcceleration()
        {
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            ship.Grid.TryAdd(BlockKey.Pack(-1, 0), new Block(BlockTypes.Thruster));
            ship.Grid.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Thruster));

            ship.Step(new ShipInput(1f, 0f, false), 1f / 60f);

            Assert.AreEqual(0f, ship.LastAngularAcceleration, Tol,
                "a symmetric thruster pair about the CoM must cancel torque");
            Assert.Greater(math.length(ship.LastLinearAcceleration), 0f);
        }

        [Test]
        public void DoublingMass_HalvesLinearAcceleration()
        {
            // a = F/M falls straight out of Step; doubling Grid.Mass.Total
            // isolates that relationship precisely.
            var light = OneOffCenterThruster();
            light.Step(new ShipInput(1f, 0f, false), 1f / 60f);
            float lightAccel = math.length(light.LastLinearAcceleration);

            var heavy = OneOffCenterThruster();
            var heavyMass = heavy.Grid.Mass;
            heavyMass.Total *= 2f;
            heavy.Grid.Mass = heavyMass;

            heavy.Step(new ShipInput(1f, 0f, false), 1f / 60f);
            float heavyAccel = math.length(heavy.LastLinearAcceleration);

            float ratio = heavyAccel / lightAccel;
            Assert.AreEqual(0.5f, ratio, 0.01f,
                "doubling mass must halve linear acceleration for the same thrust");
        }

        [Test]
        public void Step_WithNoBlocks_DoesNothing()
        {
            var ship = new ShipBody();
            ship.Step(new ShipInput(1f, 1f, true), 1f / 60f);

            Assert.AreEqual(float2.zero, ship.Position);
            Assert.AreEqual(0f, ship.Rotation);
            Assert.AreEqual(float2.zero, ship.Velocity);
            Assert.AreEqual(0f, ship.AngularVelocity);
        }

        [Test]
        public void RebuildDerivedViews_FindsThrustersAndClearsDirty()
        {
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            int t1 = BlockKey.Pack(1, 0);
            int t2 = BlockKey.Pack(-1, 0);
            ship.Grid.TryAdd(t1, new Block(BlockTypes.Thruster));
            ship.Grid.TryAdd(t2, new Block(BlockTypes.Thruster));

            ship.RebuildDerivedViews();

            Assert.AreEqual(2, ship.ThrusterKeys.Length);
            CollectionAssert.AreEquivalent(new[] { t1, t2 }, ship.ThrusterKeys);
            Assert.IsFalse(ship.Grid.TopologyDirty);
        }

        [Test]
        public void ForwardThrottle_RampsUpOverTime_RatherThanJumping()
        {
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            int key = BlockKey.Pack(0, 1);
            ship.Grid.TryAdd(key, new Block(BlockTypes.Thruster)); // level 0: 1s to full

            ship.Step(new ShipInput(1f, 0f, false), 1f / 60f);
            Assert.Less(ship.Throttle(key), 0.1f,
                "one 60Hz tick at level-0 ramp must not already be near full throttle");

            for (int i = 0; i < 120; i++) ship.Step(new ShipInput(1f, 0f, false), 1f / 60f);
            Assert.AreEqual(1f, ship.Throttle(key), 1e-3f,
                "2 seconds is well past the level-0 ramp time of 1 second");
        }

        [Test]
        public void ForwardThrottle_RampRate_DependsOnUpgradeBits()
        {
            byte level0 = 0;                 // 1.0s to full
            byte level3 = 0b1100;             // 0.15s to full, facing bits still 0 (+y)

            var slow = new ShipBody();
            slow.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            int slowKey = BlockKey.Pack(0, 1);
            slow.Grid.TryAdd(slowKey, new Block(BlockTypes.Thruster, level0));

            var fast = new ShipBody();
            fast.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            int fastKey = BlockKey.Pack(0, 1);
            fast.Grid.TryAdd(fastKey, new Block(BlockTypes.Thruster, level3));

            slow.Step(new ShipInput(1f, 0f, false), 1f / 60f);
            fast.Step(new ShipInput(1f, 0f, false), 1f / 60f);

            Assert.Greater(fast.Throttle(fastKey), slow.Throttle(slowKey),
                "a higher upgrade level must ramp throttle faster");
        }

        [Test]
        public void RetroThruster_PushesNegativeY_OnNegativeThrustAxis()
        {
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            int retro = BlockKey.Pack(0, -1);
            ship.Grid.TryAdd(retro, new Block(BlockTypes.RetroThruster, 0b1100)); // fast ramp

            ship.Step(new ShipInput(-1f, 0f, false), 1f / 60f);

            Assert.Less(ship.LastLinearAcceleration.y, 0f,
                "a retro thruster on ThrustAxis = -1 must push the ship toward -y");
        }

        [Test]
        public void RetroThruster_DoesNothing_OnPositiveThrustAxis()
        {
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            int retro = BlockKey.Pack(0, -1);
            ship.Grid.TryAdd(retro, new Block(BlockTypes.RetroThruster));

            ship.Step(new ShipInput(1f, 0f, false), 1f / 60f);

            Assert.AreEqual(0f, ship.Throttle(retro), "retro must not ramp toward the forward axis");
            Assert.AreEqual(float2.zero, ship.LastLinearAcceleration);
        }

        [Test]
        public void FinPair_TorqueMatchesSteerSign_WithNoNetLinearForce()
        {
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            // Facing = +x (bits 01) so Perpendicular is +/-y, giving a
            // lever arm; upper bits 11 = level-3 (fast) ramp.
            ship.Grid.TryAdd(BlockKey.Pack(2, 0), new Block(BlockTypes.Fin, 0b1101));
            ship.Grid.TryAdd(BlockKey.Pack(-2, 0), new Block(BlockTypes.Fin, 0b1101));

            // Fast ramp (level 3) so a handful of steps reach near-full authority.
            for (int i = 0; i < 30; i++) ship.Step(new ShipInput(0f, 1f, false), 1f / 60f);

            Assert.Greater(ship.LastAngularAcceleration, 0f,
                "steer = +1 must produce torque with the sign of the requested turn");
            Assert.AreEqual(0f, ship.LastLinearAcceleration.y, 1e-3f,
                "a symmetric fin pair must be a pure couple: no net linear force");
        }

        [Test]
        public void NoFins_SteeringProducesZeroAngularAcceleration()
        {
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));

            ship.Step(new ShipInput(0f, 1f, false), 1f / 60f);

            Assert.AreEqual(0f, ship.LastAngularAcceleration,
                "a ship with no fins cannot steer, regardless of steer input");
        }

        [Test]
        public void SteerThrottle_RampsUp_ThenFadesAfterRelease()
        {
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            int fin = BlockKey.Pack(2, 0);
            ship.Grid.TryAdd(fin, new Block(BlockTypes.Fin)); // level 0: 1s to full

            for (int i = 0; i < 60; i++) ship.Step(new ShipInput(0f, 1f, false), 1f / 60f);
            float held = ship.SteerThrottle(fin);
            Assert.AreEqual(1f, held, 1e-3f, "1 second at level-0 ramp reaches full steer throttle");

            ship.Step(new ShipInput(0f, 0f, false), 1f / 60f);
            float afterRelease = ship.SteerThrottle(fin);
            Assert.Less(afterRelease, held);
            Assert.Greater(afterRelease, 0f,
                "releasing steer must fade the throttle out over time, not cut it instantly");
        }

        [Test]
        public void Cannon_FiresShotAppliesRecoilAndRespectsCooldown()
        {
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            ship.Grid.TryAdd(BlockKey.Pack(0, 1), new Block(BlockTypes.Cannon)); // facing +y

            ship.Step(new ShipInput(0f, 0f, true), 1f / 60f);

            Assert.AreEqual(1, ship.PendingShots.Count);
            var shot = ship.PendingShots[0];
            Assert.AreEqual(0f, shot.WorldDirection.x, 1e-4f);
            Assert.Greater(shot.WorldDirection.y, 0f);
            Assert.Less(ship.Velocity.y, 0f, "recoil must push opposite the shot direction");

            ship.PendingShots.Clear();
            ship.Step(new ShipInput(0f, 0f, true), 1f / 60f);
            Assert.AreEqual(0, ship.PendingShots.Count,
                "a second FirePressed within the cooldown window must not fire again");
        }

        [Test]
        public void ApplyImpulseAtWorldPoint_OffCenter_ChangesVelocityAndAngularVelocity()
        {
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            ship.Grid.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Hull));
            ship.RebuildDerivedViews();

            ship.ApplyImpulseAtWorldPoint(new float2(5f, 0.5f), new float2(0f, 1f));

            Assert.Greater(ship.Velocity.y, 0f);
            Assert.AreNotEqual(0f, ship.AngularVelocity,
                "an impulse off the center of mass must also change angular velocity");
        }

        [Test]
        public void ApplyDamageAtWorldPoint_HitsRightBlockAndSaturates()
        {
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            int hullKey = BlockKey.Pack(0, 1);
            ship.Grid.TryAdd(hullKey, new Block(BlockTypes.Hull));

            bool hit = ship.ApplyDamageAtWorldPoint(new float2(0.5f, 1.5f), 200, out int key);
            Assert.IsTrue(hit);
            Assert.AreEqual(hullKey, key);
            ship.Grid.TryGet(hullKey, out var block);
            Assert.AreEqual(200, block.Damage);

            ship.ApplyDamageAtWorldPoint(new float2(0.5f, 1.5f), 200, out _);
            ship.Grid.TryGet(hullKey, out block);
            Assert.AreEqual(255, block.Damage, "damage must saturate at 255, not wrap or overflow");
        }

        [Test]
        public void ForwardAndReverseThrottle_RampIndependently()
        {
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            int fwd = BlockKey.Pack(0, 1);
            int retro = BlockKey.Pack(0, -1);
            ship.Grid.TryAdd(fwd, new Block(BlockTypes.Thruster, 0b1100));       // fast ramp
            ship.Grid.TryAdd(retro, new Block(BlockTypes.RetroThruster, 0b0000)); // slow ramp

            for (int i = 0; i < 15; i++) ship.Step(new ShipInput(1f, 0f, false), 1f / 60f);
            Assert.AreEqual(1f, ship.Throttle(fwd), 1e-3f, "fast forward thruster should be at full throttle");
            Assert.AreEqual(0f, ship.Throttle(retro), "reverse channel target was 0 the whole time");

            // Switch to reverse: forward ramps back down at ITS OWN rate while
            // retro ramps up at ITS OWN (slower) rate, independently.
            ship.Step(new ShipInput(-1f, 0f, false), 1f / 60f);
            Assert.Less(ship.Throttle(fwd), 1f, "forward must start ramping down once released");
            Assert.Greater(ship.Throttle(retro), 0f, "retro must start ramping up independently");
            Assert.Less(ship.Throttle(retro), 0.2f, "retro's own (slower) rate must not jump to full in one tick");
        }

        // frob:tests Assets/Scripts/Hullbreach.Ship/ShipBody.cs::ShipBody.AppliedForcesThisStep
        [Test]
        public void Step_RecordsOneAppliedForcePerThruster()
        {
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            ship.Grid.TryAdd(BlockKey.Pack(-1, -1), new Block(BlockTypes.Thruster));
            ship.Grid.TryAdd(BlockKey.Pack(1, -1), new Block(BlockTypes.Thruster));

            // Ramp fully up first so throttle is not zero (a zero-throttle
            // thruster records no entry: see StepThrusters).
            for (int i = 0; i < 60; i++) ship.Step(new ShipInput(1f, 0f, false), 1f / 60f);

            Assert.AreEqual(2, ship.AppliedForcesThisStep.Count,
                "one AppliedForcesThisStep entry per fully-throttled thruster");
            foreach (var (_, force) in ship.AppliedForcesThisStep)
            {
                Assert.Greater(force.y, 0f, "a forward thruster pushes ship-local +y");
                Assert.AreEqual(0f, force.x, Tol, "a forward thruster has no fixed facing sideways component");
            }
        }
    }
}
