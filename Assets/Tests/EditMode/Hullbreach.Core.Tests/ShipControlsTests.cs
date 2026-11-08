using NUnit.Framework;
using Hullbreach.Core;

namespace Hullbreach.Core.Tests
{
    // One key map, worded once, shown offline and online.
    public class ShipControlsTests
    {
        [Test]
        public void FlightLines_NameEveryFlightKey()
        {
            string all = ShipControls.ThrustSteerLine + " " + ShipControls.OnlineFireBuildLine;
            foreach (var key in new[] { "W/S", "A/D", "Space", "Tab" })
                StringAssert.Contains(key, all);
        }

        [Test]
        public void ThrustSteerLine_IsTheOfflineHudWording()
        {
            Assert.AreEqual("W/S or Up/Down: thrust/reverse   A/D or Left/Right: steer", ShipControls.ThrustSteerLine);
        }

        [Test]
        public void Text_IsAscii()
        {
            foreach (char c in ShipControls.ThrustSteerLine + ShipControls.OnlineFireBuildLine)
                Assert.Less(c, 128);
        }
    }
}
