using Hullbreach.Hud;
using NUnit.Framework;

namespace Hullbreach.Hud.Tests
{
    // S37-2: the standard ramp keeps ShipRenderer's old look; the colorblind ramp
    // avoids a red/green pairing and stays readable by luminance.
    public sealed class StressPaletteTests
    {
        static float Luma(HudColor c) => 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;

        [Test]
        public void Standard_StopsAreGreenYellowRed()
        {
            var low = StressPalette.Sample(0f, false);
            var mid = StressPalette.Sample(0.5f, false);
            var high = StressPalette.Sample(1f, false);
            Assert.AreEqual(0f, low.R, 1e-5f);
            Assert.AreEqual(1f, low.G, 1e-5f);
            Assert.AreEqual(1f, mid.R, 1e-5f);
            Assert.AreEqual(1f, mid.G, 1e-5f);
            Assert.AreEqual(1f, high.R, 1e-5f);
            Assert.AreEqual(0f, high.G, 1e-5f);
        }

        [Test]
        public void Ratio_IsClamped()
        {
            Assert.AreEqual(StressPalette.Sample(0f, true).B, StressPalette.Sample(-3f, true).B);
            Assert.AreEqual(StressPalette.Sample(1f, true).R, StressPalette.Sample(9f, true).R);
        }

        [Test]
        public void Colorblind_LowIsBlueHighIsOrange()
        {
            var low = StressPalette.Sample(0f, true);
            var high = StressPalette.Sample(1f, true);
            Assert.Greater(low.B, low.R);
            Assert.Greater(high.R, high.B);
        }

        [Test]
        public void Colorblind_MidIsBrighterThanBothEnds()
        {
            float lo = Luma(StressPalette.Sample(0f, true));
            float mid = Luma(StressPalette.Sample(0.5f, true));
            float hi = Luma(StressPalette.Sample(1f, true));
            Assert.Greater(mid, lo);
            Assert.Greater(mid, hi);
            Assert.Greater(System.Math.Abs(lo - hi), 0.05f);
        }

        [Test]
        public void Colorblind_NoStopIsGreenDominant()
        {
            // Green against red is the pairing red-green deficiencies cannot separate.
            foreach (float r in new[] { 0f, 0.5f, 1f })
            {
                var c = StressPalette.Sample(r, true);
                Assert.IsFalse(c.G > c.R && c.G > c.B, "green dominant at " + r);
            }
        }

        [Test]
        public void Colorblind_DiffersFromStandard()
        {
            Assert.AreNotEqual(StressPalette.Sample(0f, false).G, StressPalette.Sample(0f, true).G);
        }
    }
}
