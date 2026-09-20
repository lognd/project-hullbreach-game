using NUnit.Framework;
using Hullbreach.Ship;

namespace Hullbreach.Ship.Tests
{
    /// <summary>Locks down the modifier-bits-to-ramp-rate table shared by
    /// thrusters, retro thrusters and fins.</summary>
    public class ThrusterUpgradesTests
    {
        [Test]
        public void Level0_RampsToFullInOneSecond()
        {
            Assert.AreEqual(1f, ThrusterUpgrades.RampRate(0), 1e-4f);
        }

        [Test]
        public void HigherLevels_RampFaster()
        {
            float level0 = ThrusterUpgrades.RampRate(0b0000);
            float level1 = ThrusterUpgrades.RampRate(0b0100);
            float level2 = ThrusterUpgrades.RampRate(0b1000);
            float level3 = ThrusterUpgrades.RampRate(0b1100);

            Assert.Less(level0, level1);
            Assert.Less(level1, level2);
            Assert.Less(level2, level3);
        }

        [Test]
        public void OnlyBits2And3AreConsulted()
        {
            // Facing bits (0-1) must not perturb the ramp rate.
            Assert.AreEqual(ThrusterUpgrades.RampRate(0b0000), ThrusterUpgrades.RampRate(0b0011));
            Assert.AreEqual(ThrusterUpgrades.RampRate(0b1100), ThrusterUpgrades.RampRate(0b1111));
        }
    }
}
