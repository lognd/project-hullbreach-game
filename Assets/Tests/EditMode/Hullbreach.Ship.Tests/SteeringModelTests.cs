using NUnit.Framework;
using Hullbreach.Ship;

namespace Hullbreach.Ship.Tests
{
    /// <summary>S39 criterion 3: fins must be felt without thrust, and lever
    /// arm placement must still matter.</summary>
    public class SteeringModelTests
    {
        [Test]
        public void Torque_IsNonzeroWithZeroThrust()
        {
            float torque = SteeringModel.Torque(1f, currentThrust: 0f, finLeverArm: 1f, inertia: 1f);
            Assert.AreNotEqual(0f, torque,
                "reaction-wheel fins must produce torque with the engine off");
        }

        [Test]
        public void Torque_ScalesWithLeverArm()
        {
            float near = SteeringModel.Torque(1f, 0f, finLeverArm: 1f, inertia: 1f);
            float far = SteeringModel.Torque(1f, 0f, finLeverArm: 3f, inertia: 1f);

            Assert.AreEqual(3f * near, far, 1e-4f,
                "torque must scale linearly with the fins' distance from the CoM");
        }

        [Test]
        public void Torque_GetsAModestBonusFromThrust()
        {
            float noThrust = SteeringModel.Torque(1f, 0f, 1f, 1f);
            float fullThrust = SteeringModel.Torque(1f, 1f, 1f, 1f);

            Assert.Greater(fullThrust, noThrust, "thrust should add authority, not remove it");
            Assert.AreEqual(1.25f, fullThrust / noThrust, 1e-4f,
                "the design calls for a +25% bonus at full thrust, not a multiplicative requirement");
        }

        [Test]
        public void Torque_ZeroSteerAxis_IsZero()
        {
            Assert.AreEqual(0f, SteeringModel.Torque(0f, 1f, 5f, 1f));
        }
    }
}
