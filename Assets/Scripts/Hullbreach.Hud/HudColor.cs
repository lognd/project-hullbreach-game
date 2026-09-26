namespace Hullbreach.Hud
{
    // Engine-free RGBA color (D3): plain floats, not UnityEngine.Color, so
    // this assembly compiles with no engine reference.
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public readonly struct HudColor
    {
        // Red channel, 0..1.
        public readonly float R;

        // Green channel, 0..1.
        public readonly float G;

        // Blue channel, 0..1.
        public readonly float B;

        // Alpha channel, 0..1.
        public readonly float A;

        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public HudColor(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        // Copied verbatim from DemoMode's old literal.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static readonly HudColor ThrustRed = new HudColor(0.9f, 0.2f, 0.15f);

        // Copied verbatim from DemoMode's old literal.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static readonly HudColor ReverseGreen = new HudColor(0.2f, 0.85f, 0.3f);

        // Copied verbatim from DemoMode's old literal.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static readonly HudColor SteerWhite = new HudColor(0.6f, 0.6f, 0.6f);

        // Copied verbatim from DemoMode's old literal.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static readonly HudColor TrackDark = new HudColor(0.1f, 0.1f, 0.12f, 0.9f);

        // Copied verbatim from DemoMode's old literal.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static readonly HudColor WarningOkGreen = new HudColor(0.3f, 1f, 0.4f);

        // Copied verbatim from DemoMode's old literal.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static readonly HudColor WarningStrainYellow = new HudColor(1f, 0.85f, 0.2f);

        // Copied verbatim from DemoMode's old literal, before the CRITICAL pulse is applied.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static readonly HudColor WarningCriticalRed = new HudColor(1f, 0.15f, 0.15f);

        // DrawSteerBar's center-tick mark; same value as SteerWhite today but kept
        // separate since the two draw calls mean different things.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static readonly HudColor CenterTickGrey = new HudColor(0.6f, 0.6f, 0.6f);
    }
}
