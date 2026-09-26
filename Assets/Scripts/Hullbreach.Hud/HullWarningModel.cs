namespace Hullbreach.Hud
{
    // How close the most loaded block is to failing, as three bands the HUD
    // can shout about without the player reading numbers. Moved here from
    // Hullbreach.Game.DemoMode (D3/D7); DemoMode still computes it.
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public enum HullWarning
    {
        // Max ratio below 0.5 and buckling comfortably far off.
        Ok,
        // Max ratio 0.5 to 0.8: the structure is working hard.
        Strain,
        // Max ratio 0.8+, or critical load factor below 1.5: something is
        // about to come off.
        Critical,
    }

    // Pure function of the warning band and stress numbers (D3): the exact
    // headline/detail/hint/color the old DrawHullWarning drew, including the
    // CRITICAL pulse, with no UnityEngine dependency (time is passed in).
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public readonly struct HullWarningModel
    {
        public readonly string Headline;

        // Empty when ShowDetails is false (both lines were skipped for Ok before).
        public readonly string Detail;

        public readonly string Hint;

        public readonly HudColor Color;

        public readonly bool ShowDetails;

        HullWarningModel(string headline, string detail, string hint, HudColor color, bool showDetails)
        {
            Headline = headline;
            Detail = detail;
            Hint = hint;
            Color = color;
            ShowDetails = showDetails;
        }

        // Pure: no side effects, directly unit-testable (D3). Text and pulse
        // formula copied verbatim from DemoMode.DrawHullWarning.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static HullWarningModel Build(HullWarning band, float maxRatio, int criticalBlockCount,
            string worstBlockName, float unscaledTime)
        {
            string label = band switch
            {
                HullWarning.Critical => "CRITICAL",
                HullWarning.Strain => "STRAIN",
                _ => "OK",
            };
            string headline = $"Hull: {label}   (max ratio {maxRatio:0.00})";

            if (band == HullWarning.Ok)
            {
                return new HullWarningModel(headline, string.Empty, string.Empty, HudColor.WarningOkGreen, false);
            }

            string hint;
            HudColor color;
            if (band == HullWarning.Critical)
            {
                hint = worstBlockName != null && worstBlockName.Length > 0 && criticalBlockCount > 1
                    ? "brace the arm"
                    : "ease off thrust";
                // Alternate between full and dim red a few times a second so it
                // reads as an alarm, not a label.
                float pulse = 0.55f + 0.45f * System.MathF.Sin(unscaledTime * 12f);
                color = new HudColor(1f, 0.15f * pulse, 0.15f * pulse);
            }
            else
            {
                hint = "ease off thrust";
                color = HudColor.WarningStrainYellow;
            }

            string detail = criticalBlockCount > 0
                ? $"{criticalBlockCount} block(s) in the red, worst: {worstBlockName}"
                : "load factor low";

            return new HullWarningModel(headline, detail, hint, color, true);
        }
    }
}
