using NUnit.Framework;

namespace Hullbreach.Hud.Tests
{
    public sealed class MinimapAndStormHudTests
    {
        [Test]
        public void View_MapsCentreAndEdges()
        {
            var v = new MinimapView(10f, -5f, 100f, 80f);
            v.ToMap(10f, -5f, out float x, out float y);
            Assert.AreEqual(0f, x, 1e-4f); Assert.AreEqual(0f, y, 1e-4f);
            v.ToMap(110f, -5f, out x, out y);
            Assert.AreEqual(80f, x, 1e-4f); Assert.AreEqual(0f, y, 1e-4f);
            v.ToMap(10f, 45f, out x, out y);
            Assert.AreEqual(40f, y, 1e-4f); // +Y is up
        }

        [Test]
        public void View_ClampsOutsidePointsToEdgeWithMargin()
        {
            var v = new MinimapView(0f, 0f, 100f, 80f);
            bool inside = v.ToMapClamped(400f, 0f, 6f, out float x, out float y);
            Assert.IsFalse(inside);
            Assert.AreEqual(74f, x, 1e-3f); Assert.AreEqual(0f, y, 1e-3f);
            Assert.IsTrue(v.ToMapClamped(10f, 10f, 6f, out x, out y));
        }

        [Test]
        public void View_PixelToWorld_InvertsToMap()
        {
            var v = MinimapView.Fit(5f, 7f, 100f, 64f);
            v.ToMap(35f, 20f, out float mx, out float my);
            int px = (int)(mx + 64f), py = (int)(64f - my);
            v.PixelToWorld(px, py, 128, out float wx, out float wy);
            Assert.AreEqual(35f, wx, 2f); Assert.AreEqual(20f, wy, 2f);
        }

        [Test]
        public void View_DegenerateRadiusDoesNotDivideByZero()
        {
            var v = new MinimapView(0, 0, 0f, 50f);
            Assert.IsFalse(float.IsInfinity(v.Scale) || float.IsNaN(v.Scale));
        }

        [Test]
        public void Raster_TintsOutsideRingRed_AndMarksPlanetAndRing()
        {
            const int size = 64;
            var view = MinimapView.Fit(0f, 0f, 100f, size * 0.5f, 1f);
            var buf = MinimapRaster.Allocate(size);
            var planets = new[] { new MinimapDisc(0f, 0f, 20f, 200, 100, 50) };
            MinimapRaster.Render(buf, size, view, planets, 0f, 0f, 60f, 0f, 0f, 30f);
            int c = (size / 2 * size + size / 2) * 4;
            Assert.AreEqual(200, buf[c]);
            int py = size / 2;
            int px = size / 2 + (int)(80f * view.Scale);
            int o = (py * size + px) * 4;
            Assert.Greater(buf[o], buf[o + 2] * 2); // danger tint: red dominates
            Assert.AreEqual(0, buf[3]);             // corner is outside the round map
            int rx = size / 2 + (int)(60f * view.Scale);
            Assert.AreEqual(255, buf[(py * size + rx) * 4 + 2]); // ring pixel is white
        }

        [Test]
        public void Raster_NoStorm_HasNoTint()
        {
            const int size = 32;
            var view = MinimapView.Fit(0, 0, 100f, 16f, 1f);
            var buf = MinimapRaster.Allocate(size);
            MinimapRaster.Render(buf, size, view, null, 0, 0, 0f, 0, 0, 0f);
            Assert.Less(buf[(16 * size + 28) * 4], 40);
        }

        [Test]
        public void FlipRows_SwapsTopAndBottom()
        {
            var b = MinimapRaster.Allocate(2);
            b[0] = 1; b[8] = 9;
            MinimapRaster.FlipRows(b, 2);
            Assert.AreEqual(9, b[0]); Assert.AreEqual(1, b[8]);
        }

        [Test]
        public void Clock_FormatsMinutesAndRoundsUp()
        {
            Assert.AreEqual("0:18", StormHudModel.Clock(17.2f));
            Assert.AreEqual("1:05", StormHudModel.Clock(65f));
            Assert.AreEqual("0:00", StormHudModel.Clock(-3f));
            Assert.AreEqual("0:00", StormHudModel.Clock(float.NaN));
        }

        [Test]
        public void CountdownLine_ByStage()
        {
            Assert.AreEqual("Shrink in 0:18", StormHudModel.CountdownLine(StormHudModel.Hold, 900, 1200, 50f));
            Assert.AreEqual("Shrink in 0:30", StormHudModel.CountdownLine(StormHudModel.Grace, 1500, 1500, 50f));
            Assert.AreEqual("Closing 0:05", StormHudModel.CountdownLine(StormHudModel.Shrink, 0, 250, 50f));
            Assert.AreEqual("NO SAFE ZONE", StormHudModel.CountdownLine(StormHudModel.SuddenDeath, 0, 0, 50f));
            Assert.AreEqual("Shrink in 0:00", StormHudModel.CountdownLine(StormHudModel.Hold, 10, 10, 0f));
        }

        [Test]
        public void StageLine_NamesRing()
        {
            Assert.AreEqual("Storm: calm", StormHudModel.StageLine(0, 0));
            Assert.AreEqual("Storm: ring 2 holding", StormHudModel.StageLine(1, 1));
            Assert.AreEqual("Storm: ring 3 closing", StormHudModel.StageLine(2, 2));
            Assert.AreEqual("Storm: sudden death", StormHudModel.StageLine(3, 5));
        }

        [Test]
        public void WarningAlpha_ZeroWhenSafe_StrongerOutside()
        {
            Assert.AreEqual(0f, StormHudModel.WarningAlpha(false, -5f, true, 1f));
            Assert.AreEqual(0f, StormHudModel.WarningAlpha(true, 50f, false, 1f));
            float band = StormHudModel.WarningAlpha(true, 2f, true, 1f);
            float outside = StormHudModel.WarningAlpha(true, -2f, false, 1f);
            Assert.Greater(band, 0f);
            Assert.Greater(outside, band);
            Assert.LessOrEqual(outside, 0.3f);
        }

        [Test]
        public void ShowPullArrow_OnlyOutside()
        {
            Assert.IsTrue(StormHudModel.ShowPullArrow(true, -0.1f));
            Assert.IsFalse(StormHudModel.ShowPullArrow(true, 3f));
            Assert.IsFalse(StormHudModel.ShowPullArrow(false, -3f));
        }

        [Test]
        public void EdgeArrow_HiddenOnScreen_PinnedOffScreen()
        {
            Assert.IsFalse(EdgeArrowModel.ToTarget(960, 540, 40, 100, -50).Visible);
            var right = EdgeArrowModel.ToTarget(960, 540, 40, 2000, 0);
            Assert.IsTrue(right.Visible);
            Assert.AreEqual(920f, right.X, 1e-3f); Assert.AreEqual(0f, right.Y, 1e-3f); Assert.AreEqual(0f, right.AngleDegrees, 1e-3f);
            var up = EdgeArrowModel.ToTarget(960, 540, 40, 0, 3000);
            Assert.AreEqual(500f, up.Y, 1e-3f); Assert.AreEqual(90f, up.AngleDegrees, 1e-3f);
            var diag = EdgeArrowModel.ToTarget(960, 540, 40, 5000, 5000);
            Assert.LessOrEqual(diag.X, 920.01f); Assert.LessOrEqual(diag.Y, 500.01f);
            Assert.AreEqual(45f, diag.AngleDegrees, 1e-3f);
        }

        [Test]
        public void EdgeArrow_Along_ShownUnlessZero()
        {
            var a = EdgeArrowModel.Along(960, 540, 40, -1f, 0f);
            Assert.IsTrue(a.Visible); Assert.AreEqual(-920f, a.X, 1e-3f);
            Assert.AreEqual(180f, System.Math.Abs(a.AngleDegrees), 1e-3f);
            Assert.IsFalse(EdgeArrowModel.Along(960, 540, 40, 0f, 0f).Visible);
        }
    }
}
