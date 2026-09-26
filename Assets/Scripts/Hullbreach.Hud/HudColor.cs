namespace Hullbreach.Hud
{
    /// <summary>
    /// Engine-free RGBA color (D3): the HUD's palette lives here as plain
    /// floats, not <c>UnityEngine.Color</c>, so this assembly stays testable
    /// without a Unity license. Views convert to <c>UnityEngine.Color</c> at
    /// the last possible moment.
    /// </summary>
    public readonly struct HudColor
    {
        /// <summary>Red channel, 0..1.</summary>
        public readonly float R;

        /// <summary>Green channel, 0..1.</summary>
        public readonly float G;

        /// <summary>Blue channel, 0..1.</summary>
        public readonly float B;

        /// <summary>Alpha channel, 0..1.</summary>
        public readonly float A;

        /// <summary>Builds a color from explicit channels; alpha defaults to opaque.</summary>
        public HudColor(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        /// <summary>Forward thrust bar fill, copied verbatim from DemoMode's old literal.</summary>
        public static readonly HudColor ThrustRed = new HudColor(0.9f, 0.2f, 0.15f);

        /// <summary>Reverse thrust bar fill, copied verbatim from DemoMode's old literal.</summary>
        public static readonly HudColor ReverseGreen = new HudColor(0.2f, 0.85f, 0.3f);

        /// <summary>Steer bar fill (centered, signed), copied verbatim from DemoMode's old literal.</summary>
        public static readonly HudColor SteerWhite = new HudColor(0.6f, 0.6f, 0.6f);

        /// <summary>Bar track background, copied verbatim from DemoMode's old literal.</summary>
        public static readonly HudColor TrackDark = new HudColor(0.1f, 0.1f, 0.12f, 0.9f);

        /// <summary>Hull warning "OK" band color, copied verbatim from DemoMode's old literal.</summary>
        public static readonly HudColor WarningOkGreen = new HudColor(0.3f, 1f, 0.4f);

        /// <summary>Hull warning "STRAIN" band color, copied verbatim from DemoMode's old literal.</summary>
        public static readonly HudColor WarningStrainYellow = new HudColor(1f, 0.85f, 0.2f);

        /// <summary>Hull warning "CRITICAL" band base color (before the pulse), copied verbatim from DemoMode's old literal.</summary>
        public static readonly HudColor WarningCriticalRed = new HudColor(1f, 0.15f, 0.15f);
    }
}
