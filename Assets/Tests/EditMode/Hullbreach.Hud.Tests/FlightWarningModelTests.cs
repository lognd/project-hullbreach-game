using NUnit.Framework;

namespace Hullbreach.Hud.Tests
{
    public sealed class FlightWarningModelTests
    {
        [Test]
        public void Text_ByTier()
        {
            Assert.AreEqual("PULL UP 2.5s", FlightWarningModel.Text(4, false, false, 2.5f));
            Assert.AreEqual("BRAKE 1.0s", FlightWarningModel.Text(3, true, false, 1f));
            Assert.AreEqual("PULL UP", FlightWarningModel.Text(3, false, false, float.PositiveInfinity));
            Assert.AreEqual("Terrain 4.0s", FlightWarningModel.Text(2, false, false, 4f));
            Assert.AreEqual("Re-entry heat", FlightWarningModel.Text(1, false, true, 20f));
            Assert.AreEqual(string.Empty, FlightWarningModel.Text(1, false, false, 3f));
            Assert.AreEqual(string.Empty, FlightWarningModel.Text(0, false, false, float.NaN));
        }

        [Test]
        public void BlinkAndAssist()
        {
            Assert.Greater(FlightWarningModel.BlinkHz(4), FlightWarningModel.BlinkHz(3));
            Assert.AreEqual(0f, FlightWarningModel.BlinkHz(2));
            Assert.AreEqual("ASSIST OFF", FlightWarningModel.AssistText(false));
            FlightWarningModel.Colour(4, out float r, out float g, out _);
            Assert.Greater(r, g);
        }
    }
}
