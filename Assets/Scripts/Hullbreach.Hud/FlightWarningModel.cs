using System;
using System.Globalization;

namespace Hullbreach.Hud
{
    // Engine-free text and colour for the planet-impact warning and the flight-assist indicator.
    // Severity uses the ImpactSeverity byte values (0 None, 1 Safe, 2 Caution, 3 Danger, 4 Catastrophic).
    // frob:doc docs/design/ui-port.md#feel-layer
    public static class FlightWarningModel
    {
        public const string ControlsHint = "W/S thrust   A/D steer   Space fire   Shift: assist off   M: map";

        // Empty when nothing needs saying.
        public static string Text(int severity, bool mustBrake, bool heating, float timeToImpact)
        {
            string tti = float.IsInfinity(timeToImpact) || float.IsNaN(timeToImpact) || timeToImpact > 9.5f
                ? string.Empty : " " + timeToImpact.ToString("0.0", CultureInfo.InvariantCulture) + "s";
            if (severity >= 4) return "PULL UP" + tti;
            if (severity == 3) return (mustBrake ? "BRAKE" : "PULL UP") + tti;
            if (severity == 2) return "Terrain" + tti;
            if (heating) return "Re-entry heat";
            return string.Empty;
        }

        // RGB 0..1 for the warning text.
        public static void Colour(int severity, out float r, out float g, out float b)
        {
            if (severity >= 3) { r = 1f; g = 0.25f; b = 0.2f; }
            else if (severity == 2) { r = 1f; g = 0.75f; b = 0.25f; }
            else { r = 1f; g = 0.6f; b = 0.3f; }
        }

        // Blink rate in Hz: faster as it gets worse; 0 means steady.
        public static float BlinkHz(int severity) => severity >= 4 ? 5f : severity == 3 ? 3f : 0f;

        public static string AssistText(bool assistActive) => assistActive ? "ASSIST ON" : "ASSIST OFF";
    }
}
