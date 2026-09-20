using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Ship;

namespace Hullbreach.Ship.Tests
{
    /// <summary>S39 handling criteria, exercised against the plain-C# sim so
    /// they run without a scene and stay meaningful once the headless server
    /// reuses the same ShipBody.</summary>
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
            ship.Step(new ShipInput(true, 0f, false), 1f / 60f);

            Assert.Greater(math.length(ship.LastLinearAcceleration), 0f,
                "an off-center thruster still pushes the whole ship");
            Assert.AreNotEqual(0f, ship.LastAngularAcceleration,
                "a thruster not on the CoM must produce torque -- S39 criterion 1");
        }

        [Test]
        public void SymmetricThrusterPair_ProducesNoNetAngularAcceleration()
        {
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            ship.Grid.TryAdd(BlockKey.Pack(-1, 0), new Block(BlockTypes.Thruster));
            ship.Grid.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Thruster));

            ship.Step(new ShipInput(true, 0f, false), 1f / 60f);

            Assert.AreEqual(0f, ship.LastAngularAcceleration, Tol,
                "a symmetric thruster pair about the CoM must cancel torque");
            Assert.Greater(math.length(ship.LastLinearAcceleration), 0f);
        }

        [Test]
        public void DoublingMass_HalvesLinearAcceleration()
        {
            // a = F/M falls straight out of Step; doubling Grid.Mass.Total
            // (rather than fighting the discrete set of block masses to land
            // on exactly 2x) isolates that relationship precisely.
            var light = OneOffCenterThruster();
            light.Step(new ShipInput(true, 0f, false), 1f / 60f);
            float lightAccel = math.length(light.LastLinearAcceleration);

            var heavy = OneOffCenterThruster();
            var heavyMass = heavy.Grid.Mass;
            heavyMass.Total *= 2f;
            heavy.Grid.Mass = heavyMass;

            heavy.Step(new ShipInput(true, 0f, false), 1f / 60f);
            float heavyAccel = math.length(heavy.LastLinearAcceleration);

            float ratio = heavyAccel / lightAccel;
            Assert.AreEqual(0.5f, ratio, 0.01f,
                "doubling mass must halve linear acceleration for the same thrust");
        }

        [Test]
        public void Step_WithNoBlocks_DoesNothing()
        {
            var ship = new ShipBody();
            ship.Step(new ShipInput(true, 1f, true), 1f / 60f);

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
    }
}
