using NUnit.Framework;

namespace Hullbreach.Hud.Tests
{
    public sealed class PlanetWarningModelTests
    {
        const float Safe = 4f;

        // A ship high above the surface, not near anything: the baseline for the cases below.
        static PlanetWarning Eval(float altitude = 200f, float closing = 0f, float tangential = 0f, float tti = float.PositiveInfinity,
            float impact = 0f, float stop = float.PositiveInfinity, float net = 5f, int severity = 0, bool brake = false, bool heat = false)
            => PlanetWarningModel.Evaluate(altitude, closing, tangential, tti, impact, Safe, stop, net, severity, brake, heat);

        [Test]
        public void FarAway_SaysNothing()
        {
            var w = Eval();
            Assert.AreEqual(0, w.Level);
            Assert.AreEqual(string.Empty, w.Text);
            Assert.IsFalse(w.ShowArrow);
            Assert.AreEqual(0f, w.EdgeAlpha);
            Assert.AreEqual(0f, w.BeepInterval);
        }

        [Test]
        public void FallingTowardAPlanet_WarnsAtTenSecondsNotJustTheShipsSix()
        {
            // 9 s out: the ship's own window (6 s) says None, the model already says Terrain with an arrow.
            var w = Eval(altitude: 300f, closing: 30f, tti: 9f, impact: 60f, stop: 20f, severity: 0);
            Assert.AreEqual(2, w.Level);
            StringAssert.StartsWith("Terrain", w.Text);
            Assert.IsTrue(w.ShowArrow);
            Assert.Greater(w.EdgeAlpha, 0f);

            var late = Eval(altitude: 300f, closing: 30f, tti: 11f, impact: 60f, stop: 20f, severity: 0);
            Assert.AreEqual(0, late.Level, "more than ten seconds out and able to stop is quiet");
        }

        [Test]
        public void CannotStopInTime_EscalatesToBrakeBeforeTheShipWindowOpens()
        {
            var w = Eval(altitude: 100f, closing: 20f, tti: 20f, impact: 40f, stop: 90f, severity: 0);
            Assert.AreEqual(3, w.Level);
            StringAssert.StartsWith("BRAKE", w.Text);
            Assert.Greater(w.BeepInterval, 0f);
        }

        [Test]
        public void SafeLandingSpeed_DoesNotWarnEarly()
        {
            var w = Eval(altitude: 20f, closing: 2f, tti: 6f, impact: 3f, stop: 1f, severity: 1);
            Assert.AreEqual(1, w.Level);
            Assert.AreEqual(string.Empty, w.Text);
        }

        [Test]
        public void ShipSeverity_IsNeverLowered()
        {
            var w = Eval(altitude: 10f, closing: 40f, tti: 0.5f, impact: 50f, stop: 400f, severity: 4);
            Assert.AreEqual(4, w.Level);
            StringAssert.StartsWith("PULL UP", w.Text);
            Assert.Greater(w.BeepInterval, 0f);
            Assert.Less(w.BeepInterval, 0.7f);
        }

        [Test]
        public void Edge_GrowsWithLevel()
        {
            float l2 = Eval(altitude: 300f, closing: 30f, tti: 9f, impact: 60f, stop: 20f).EdgeAlpha;
            float l3 = Eval(altitude: 100f, closing: 20f, tti: 20f, impact: 40f, stop: 90f).EdgeAlpha;
            float l4 = Eval(altitude: 10f, closing: 40f, tti: 0.5f, impact: 50f, stop: 400f, severity: 4).EdgeAlpha;
            Assert.Less(l2, l3);
            Assert.Less(l3, l4);
        }

        [Test]
        public void LowAndTooWeakToClimb_IsAGravityTrap()
        {
            var w = Eval(altitude: 1f, closing: 0f, net: -2f);
            Assert.IsTrue(w.Trapped);
            Assert.AreEqual(PlanetWarningModel.TrapText, w.Text);
            Assert.IsTrue(w.ShowArrow);
            Assert.GreaterOrEqual(w.Level, 3);
            Assert.Greater(w.BeepInterval, 0f);
        }

        [Test]
        public void LowButAbleToClimb_IsNotATrap_JustALandedHintAtTheSurface()
        {
            var high = Eval(altitude: 8f, closing: 0f, net: 4f);
            Assert.IsFalse(high.Trapped);
            Assert.AreEqual(string.Empty, high.Text);

            var landed = Eval(altitude: 0.5f, closing: 0f, net: 4f);
            Assert.IsFalse(landed.Trapped);
            Assert.AreEqual(PlanetWarningModel.LandedText, landed.Text);
            Assert.IsTrue(landed.ShowArrow);
            Assert.AreEqual(0f, landed.BeepInterval, "a landed hint is silent");
        }

        [Test]
        public void ClimbingAwayFast_IsNotATrapEvenWithWeakThrust()
        {
            Assert.IsFalse(Eval(altitude: 10f, closing: -8f, net: -1f).Trapped);
        }

        [Test]
        public void ReentryHeat_IsStillReported()
        {
            var w = Eval(altitude: 400f, closing: 0f, heat: true, severity: 1);
            Assert.AreEqual("Re-entry heat", w.Text);
        }

        [Test]
        public void ArrowPointsAwayFromThePlanet()
        {
            Assert.AreEqual(0f, PlanetWarningModel.ArrowAngleDegrees(0f, 1f, 0f), 1e-3f, "normal up: arrow up");
            Assert.AreEqual(-90f, PlanetWarningModel.ArrowAngleDegrees(1f, 0f, 0f), 1e-3f, "normal right: clockwise quarter turn");
            Assert.AreEqual(90f, PlanetWarningModel.ArrowAngleDegrees(-1f, 0f, 0f), 1e-3f);
            Assert.AreEqual(-30f, PlanetWarningModel.ArrowAngleDegrees(0f, 1f, 30f), 1e-3f, "camera roll is subtracted");
        }

        // Runs the tracker and the model together the way FlightHudView does, once per frame.
        static PlanetWarning Frame(PlanetTrapTracker t, float dt, float altitude, float closing, float tangential, float net)
        {
            bool allowed = t.Update(dt, altitude, closing, tangential, net);
            return PlanetWarningModel.Evaluate(altitude, closing, tangential, float.PositiveInfinity, 0f, Safe, float.PositiveInfinity, net, 0, false, false, allowed);
        }

        [Test]
        public void RestingShip_StaysQuiet_ForANinetySecondLanding()
        {
            var t = new PlanetTrapTracker();
            for (int i = 0; i < 90 * 60; i++)
            {
                var w = Frame(t, 1f / 60f, 0.5f, 0f, 0f, -2f);
                Assert.IsFalse(w.Trapped);
                Assert.AreNotEqual(PlanetWarningModel.TrapText, w.Text);
                Assert.AreEqual(0f, w.BeepInterval);
            }
            Assert.AreEqual(PlanetWarningModel.LandedText, Frame(t, 1f / 60f, 0.5f, 0f, 0f, -2f).Text);
        }

        [Test]
        public void SinkingShip_Alerts()
        {
            var t = new PlanetTrapTracker();
            var w = Frame(t, 1f / 60f, 6f, 1.0f, 0f, -2f);
            Assert.IsTrue(w.Trapped);
            Assert.AreEqual(PlanetWarningModel.TrapText, w.Text);
            Assert.Greater(w.BeepInterval, 0f);
        }

        [Test]
        public void Alert_TimesOut_ThenFallsBackToTheSilentSurfaceHint()
        {
            var t = new PlanetTrapTracker();
            PlanetWarning w = default;
            for (int i = 0; i < 3 * 60; i++) w = Frame(t, 1f / 60f, 1f, 0.9f, 0f, -2f);
            Assert.IsTrue(w.Trapped, "still alerting at 3 s");
            for (int i = 0; i < 2 * 60; i++) w = Frame(t, 1f / 60f, 1f, 0.9f, 0f, -2f);
            Assert.IsFalse(w.Trapped, "silent after 4 s");
            Assert.AreEqual(PlanetWarningModel.LandedText, w.Text);
            Assert.AreEqual(0f, w.BeepInterval);
            Assert.IsTrue(t.Expired);
        }

        [Test]
        public void Alert_ReArms_AfterClimbingClear()
        {
            var t = new PlanetTrapTracker();
            for (int i = 0; i < 5 * 60; i++) Frame(t, 1f / 60f, 1f, 1.0f, 0f, -2f);
            Assert.IsTrue(t.Expired);
            // Hovering just above the trap band does not re-arm; climbing past the hysteresis band does.
            Frame(t, 1f / 60f, PlanetWarningModel.TrapAltitude + 1f, 0f, 0f, -2f);
            Assert.IsTrue(t.Expired);
            Frame(t, 1f / 60f, PlanetWarningModel.TrapAltitude + PlanetWarningModel.RearmAltitudeMargin + 1f, 0f, 0f, -2f);
            Assert.IsFalse(t.Expired);
            Assert.IsTrue(Frame(t, 1f / 60f, 5f, 1.0f, 0f, -2f).Trapped, "a new descent alerts again");
        }

        [Test]
        public void Alert_ReArms_WhenThrustVsGravityChanges()
        {
            var t = new PlanetTrapTracker();
            for (int i = 0; i < 5 * 60; i++) Frame(t, 1f / 60f, 1f, 1.0f, 0f, -2f);
            Assert.IsTrue(t.Expired);
            Assert.IsTrue(Frame(t, 1f / 60f, 1f, 1.0f, 0f, -3f).Trapped, "weaker thrust is a new situation");
        }
    }
}
