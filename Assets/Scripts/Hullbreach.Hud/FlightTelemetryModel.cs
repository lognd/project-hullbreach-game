namespace Hullbreach.Hud
{
    // One labelled bar's text + clamped fill + colors, shared shape for
    // thrust, reverse and steer (D3): the view just copies these fields.
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public readonly struct ChannelBarValue
    {
        // Full label + percent line, e.g. "Thrust  45%" or "Steer   -10%".
        public readonly string Label;

        // 0..1 for thrust/reverse; -1..1 center-origin for steer.
        public readonly float Fill;

        public readonly bool Centered;

        public readonly HudColor FillColor;

        public readonly HudColor TrackColor;

        ChannelBarValue(string label, float fill, bool centered, HudColor fillColor, HudColor trackColor)
        {
            Label = label;
            Fill = fill;
            Centered = centered;
            FillColor = fillColor;
            TrackColor = trackColor;
        }

        // Clamp then format, matching DrawChannelBar's order (clamp before percent text).
        internal static ChannelBarValue Uncentered(string label, float value01, HudColor fillColor)
        {
            float clamped = value01 < 0f ? 0f : value01 > 1f ? 1f : value01;
            return new ChannelBarValue($"{label} {clamped * 100f:0}%", clamped, false, fillColor, HudColor.TrackDark);
        }

        // Clamp then format, matching DrawSteerBar's order and sign format.
        internal static ChannelBarValue Steer(string label, float value)
        {
            float clamped = value < -1f ? -1f : value > 1f ? 1f : value;
            return new ChannelBarValue($"{label}{clamped * 100f:+0;-0;0}%", clamped, true, HudColor.SteerWhite, HudColor.TrackDark);
        }
    }

    // Pure function of ship telemetry (D3), no UnityEngine dependency.
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public readonly struct FlightTelemetryModel
    {
        public readonly string SpeedLine;

        public readonly ChannelBarValue Thrust;

        public readonly ChannelBarValue Reverse;

        public readonly ChannelBarValue Steer;

        FlightTelemetryModel(string speedLine, ChannelBarValue thrust, ChannelBarValue reverse, ChannelBarValue steer)
        {
            SpeedLine = speedLine;
            Thrust = thrust;
            Reverse = reverse;
            Steer = steer;
        }

        // Pure: no side effects, directly unit-testable (D3). Format strings
        // copied verbatim from DemoMode.DrawStatusPanel/DrawChannelBar/DrawSteerBar.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static FlightTelemetryModel Build(float velocityX, float velocityY, float angularVelocity,
            float forwardThrottleMean, float reverseThrottleMean, float steerThrottleMean)
        {
            float speed = System.MathF.Sqrt(velocityX * velocityX + velocityY * velocityY);
            float absAngular = angularVelocity < 0f ? -angularVelocity : angularVelocity;
            string speedLine = $"Speed: {speed:0.0}   Angular speed: {absAngular:0.00}";

            var thrust = ChannelBarValue.Uncentered("Thrust ", forwardThrottleMean, HudColor.ThrustRed);
            var reverse = ChannelBarValue.Uncentered("Reverse", reverseThrottleMean, HudColor.ReverseGreen);
            var steer = ChannelBarValue.Steer("Steer   ", steerThrottleMean);

            return new FlightTelemetryModel(speedLine, thrust, reverse, steer);
        }
    }
}
