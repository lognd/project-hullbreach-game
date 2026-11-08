using System;

namespace Hullbreach.Hud
{
    // Engine-free stress ramp (S37-2) with a colorblind-safe alternative.
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public static class StressPalette
    {
        // Standard ramp stops: green, yellow, red (ShipRenderer's original look).
        static readonly HudColor StdLow = new HudColor(0f, 1f, 0f);
        static readonly HudColor StdMid = new HudColor(1f, 1f, 0f);
        static readonly HudColor StdHigh = new HudColor(1f, 0f, 0f);

        // Okabe-Ito blue, yellow and vermillion: separable under the common color-vision deficiencies.
        static readonly HudColor CbLow = new HudColor(0f, 0.447f, 0.698f);
        static readonly HudColor CbMid = new HudColor(0.941f, 0.894f, 0.259f);
        static readonly HudColor CbHigh = new HudColor(0.835f, 0.369f, 0f);

        // Color for a stress ratio (clamped to 0..1); colorblind swaps in the Okabe-Ito ramp.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static HudColor Sample(float ratio, bool colorblind)
        {
            ratio = Math.Min(1f, Math.Max(0f, ratio));
            var low = colorblind ? CbLow : StdLow;
            var mid = colorblind ? CbMid : StdMid;
            var high = colorblind ? CbHigh : StdHigh;
            return ratio < 0.5f ? Lerp(low, mid, ratio * 2f) : Lerp(mid, high, (ratio - 0.5f) * 2f);
        }

        static HudColor Lerp(HudColor a, HudColor b, float t) =>
            new HudColor(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, 1f);
    }
}
