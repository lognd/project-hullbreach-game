namespace Hullbreach.Hud
{
    // Engine-free text and fill for the mid-match build cooldown readout (D3), so a refused click is never a mystery.
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public static class BuildCooldownModel
    {
        // Seconds still to wait, from the time since the last placement; never negative.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static float Remaining(float secondsSinceLastPlace, float cooldownSeconds)
        {
            if (float.IsNaN(secondsSinceLastPlace) || cooldownSeconds <= 0f) return 0f;
            float left = cooldownSeconds - secondsSinceLastPlace;
            return left > 0f ? left : 0f;
        }

        // One HUD line: "Build ready" or "Build cooldown 0.8s".
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static string Line(float remaining) =>
            remaining <= 0f ? "Build ready" : System.FormattableString.Invariant($"Build cooldown {remaining:0.0}s");

        // 0 when ready up to 1 right after a placement, for a drain bar.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static float Fill(float remaining, float cooldownSeconds)
        {
            if (cooldownSeconds <= 0f || remaining <= 0f) return 0f;
            float fill = remaining / cooldownSeconds;
            return fill > 1f ? 1f : fill;
        }
    }
}
