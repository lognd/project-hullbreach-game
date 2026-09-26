using Hullbreach.Hud;
using NUnit.Framework;

namespace Hullbreach.Hud.Tests
{
    // Covers every public member of FlightTelemetryModel/ChannelBarValue,
    // including clamping and the zero/negative steer formats (D7).
    public sealed class FlightTelemetryModelTests
    {
        [Test]
        public void SpeedLine_CombinesVelocityMagnitudeAndAbsAngularSpeed()
        {
            var model = FlightTelemetryModel.Build(3f, 4f, -2f, 0f, 0f, 0f);
            Assert.AreEqual("Speed: 5.0   Angular speed: 2.00", model.SpeedLine);
        }

        [Test]
        public void SpeedLine_TakesAbsoluteValueOfAngularVelocity()
        {
            var positive = FlightTelemetryModel.Build(0f, 0f, 1.5f, 0f, 0f, 0f);
            var negative = FlightTelemetryModel.Build(0f, 0f, -1.5f, 0f, 0f, 0f);
            Assert.AreEqual(positive.SpeedLine, negative.SpeedLine);
            StringAssert.Contains("Angular speed: 1.50", positive.SpeedLine);
        }

        [Test]
        public void Thrust_LabelAndFill_MidRange()
        {
            var model = FlightTelemetryModel.Build(0f, 0f, 0f, 0.5f, 0f, 0f);
            Assert.AreEqual("Thrust  50%", model.Thrust.Label);
            Assert.AreEqual(0.5f, model.Thrust.Fill);
            Assert.IsFalse(model.Thrust.Centered);
            Assert.AreEqual(HudColor.ThrustRed.R, model.Thrust.FillColor.R);
        }

        [Test]
        public void Thrust_ClampsAboveOne()
        {
            var model = FlightTelemetryModel.Build(0f, 0f, 0f, 1.5f, 0f, 0f);
            Assert.AreEqual(1f, model.Thrust.Fill);
            Assert.AreEqual("Thrust  100%", model.Thrust.Label);
        }

        [Test]
        public void Thrust_ClampsBelowZero()
        {
            var model = FlightTelemetryModel.Build(0f, 0f, 0f, -0.5f, 0f, 0f);
            Assert.AreEqual(0f, model.Thrust.Fill);
            Assert.AreEqual("Thrust  0%", model.Thrust.Label);
        }

        [Test]
        public void Reverse_LabelAndFill_MidRange()
        {
            var model = FlightTelemetryModel.Build(0f, 0f, 0f, 0f, 0.25f, 0f);
            Assert.AreEqual("Reverse 25%", model.Reverse.Label);
            Assert.AreEqual(0.25f, model.Reverse.Fill);
            Assert.IsFalse(model.Reverse.Centered);
            Assert.AreEqual(HudColor.ReverseGreen.G, model.Reverse.FillColor.G);
        }

        [Test]
        public void Reverse_ClampsAboveOneAndBelowZero()
        {
            var above = FlightTelemetryModel.Build(0f, 0f, 0f, 0f, 2f, 0f);
            var below = FlightTelemetryModel.Build(0f, 0f, 0f, 0f, -2f, 0f);
            Assert.AreEqual(1f, above.Reverse.Fill);
            Assert.AreEqual(0f, below.Reverse.Fill);
        }

        [Test]
        public void Steer_PositiveValueUsesPlusSign()
        {
            var model = FlightTelemetryModel.Build(0f, 0f, 0f, 0f, 0f, 0.5f);
            Assert.AreEqual("Steer   +50%", model.Steer.Label);
            Assert.AreEqual(0.5f, model.Steer.Fill);
            Assert.IsTrue(model.Steer.Centered);
        }

        [Test]
        public void Steer_NegativeValueUsesMinusSign()
        {
            var model = FlightTelemetryModel.Build(0f, 0f, 0f, 0f, 0f, -0.5f);
            Assert.AreEqual("Steer   -50%", model.Steer.Label);
            Assert.AreEqual(-0.5f, model.Steer.Fill);
        }

        [Test]
        public void Steer_ZeroValueUsesNoSign()
        {
            var model = FlightTelemetryModel.Build(0f, 0f, 0f, 0f, 0f, 0f);
            Assert.AreEqual("Steer   0%", model.Steer.Label);
            Assert.AreEqual(0f, model.Steer.Fill);
        }

        [Test]
        public void Steer_ClampsToPlusOrMinusOne()
        {
            var above = FlightTelemetryModel.Build(0f, 0f, 0f, 0f, 0f, 2f);
            var below = FlightTelemetryModel.Build(0f, 0f, 0f, 0f, 0f, -2f);
            Assert.AreEqual(1f, above.Steer.Fill);
            Assert.AreEqual(-1f, below.Steer.Fill);
            Assert.AreEqual("Steer   +100%", above.Steer.Label);
            Assert.AreEqual("Steer   -100%", below.Steer.Label);
        }

        [Test]
        public void Steer_UsesSteerWhiteFillAndDarkTrack()
        {
            var model = FlightTelemetryModel.Build(0f, 0f, 0f, 0f, 0f, 0f);
            Assert.AreEqual(HudColor.SteerWhite.R, model.Steer.FillColor.R);
            Assert.AreEqual(1f, HudColor.SteerWhite.G);
            Assert.AreEqual(HudColor.TrackDark.A, model.Steer.TrackColor.A);
        }

        [Test]
        public void Thrust_And_Reverse_UseDarkTrack()
        {
            var model = FlightTelemetryModel.Build(0f, 0f, 0f, 0f, 0f, 0f);
            Assert.AreEqual(HudColor.TrackDark.A, model.Thrust.TrackColor.A);
            Assert.AreEqual(HudColor.TrackDark.A, model.Reverse.TrackColor.A);
        }

        [Test]
        public void CenterTickGrey_MatchesDrawSteerBarsOldLiteral()
        {
            Assert.AreEqual(0.6f, HudColor.CenterTickGrey.R);
            Assert.AreEqual(0.6f, HudColor.CenterTickGrey.G);
            Assert.AreEqual(0.6f, HudColor.CenterTickGrey.B);
        }
    }
}
