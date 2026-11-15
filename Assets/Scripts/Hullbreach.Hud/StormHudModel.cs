using System;
using System.Globalization;

namespace Hullbreach.Hud
{
    // Engine-free text, tint and arrow maths for the storm HUD. Stage numbers match StormStage
    // (0 Grace, 1 Hold, 2 Shrink, 3 SuddenDeath) so this assembly needs no World reference.
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public static class StormHudModel
    {
        public const int Grace = 0, Hold = 1, Shrink = 2, SuddenDeath = 3;

        // "0:18" style clock; negative or NaN reads as 0:00 and minutes grow past 9.
        public static string Clock(float seconds)
        {
            if (float.IsNaN(seconds) || seconds < 0f) seconds = 0f;
            int total = (int)Math.Ceiling(seconds);
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", total / 60, total % 60);
        }

        // Seconds from ticks; tickRate <= 0 reads as zero.
        public static float Seconds(int ticks, float tickRate) => tickRate > 0f && ticks > 0 ? ticks / tickRate : 0f;

        // The big countdown line: time to the next shrink, or how long the shrink lasts.
        public static string CountdownLine(int stage, int ticksUntilShrink, int ticksUntilNextStage, float tickRate)
        {
            switch (stage)
            {
                case Grace:
                case Hold:
                    return "Shrink in " + Clock(Seconds(ticksUntilShrink, tickRate));
                case Shrink:
                    return "Closing " + Clock(Seconds(ticksUntilNextStage, tickRate));
                default:
                    return "NO SAFE ZONE";
            }
        }

        // Small stage caption under the countdown. phaseIndex is zero-based.
        public static string StageLine(int stage, int phaseIndex)
        {
            string ring = (phaseIndex + 1).ToString(CultureInfo.InvariantCulture);
            switch (stage)
            {
                case Grace: return "Storm: calm";
                case Hold: return "Storm: ring " + ring + " holding";
                case Shrink: return "Storm: ring " + ring + " closing";
                default: return "Storm: sudden death";
            }
        }

        // 0..1 strength of the full-screen red tint. Nothing while safe or when no storm runs; a slow
        // faint pulse in the warning band; a stronger, faster pulse outside the ring.
        public static float WarningAlpha(bool stormActive, float signedDistance, bool inWarningBand, float timeSeconds)
        {
            if (!stormActive) return 0f;
            if (signedDistance < 0f)
            {
                float pulse = 0.5f + 0.5f * (float)Math.Sin(timeSeconds * 7f);
                return 0.07f + 0.08f * pulse;
            }
            if (inWarningBand)
            {
                float pulse = 0.5f + 0.5f * (float)Math.Sin(timeSeconds * 3f);
                return 0.03f + 0.05f * pulse;
            }
            return 0f;
        }

        // The pull arrow shows only once outside the safe ring.
        public static bool ShowPullArrow(bool stormActive, float signedDistance) => stormActive && signedDistance < 0f;
    }

    // Where a screen-edge arrow sits and which way it points. Offsets are pixels from the screen
    // centre, +Y up; angle is degrees, 0 = +X, counter-clockwise.
    public readonly struct EdgeArrow
    {
        public readonly bool Visible;
        public readonly float X, Y, AngleDegrees;
        public EdgeArrow(bool visible, float x, float y, float angle) { Visible = visible; X = x; Y = y; AngleDegrees = angle; }
    }

    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public static class EdgeArrowModel
    {
        // Arrow for a target at (dx,dy) pixels from screen centre. Hidden while the target is on screen;
        // else pinned to the screen rect inset by margin, pointing at it.
        public static EdgeArrow ToTarget(float halfW, float halfH, float margin, float dx, float dy)
        {
            if (Math.Abs(dx) <= halfW && Math.Abs(dy) <= halfH) return new EdgeArrow(false, 0, 0, 0);
            return Pin(halfW, halfH, margin, dx, dy);
        }

        // Arrow along a direction (shown unless the direction is zero), pinned at the screen edge.
        public static EdgeArrow Along(float halfW, float halfH, float margin, float dirX, float dirY)
        {
            if (dirX * dirX + dirY * dirY < 1e-12f) return new EdgeArrow(false, 0, 0, 0);
            return Pin(halfW, halfH, margin, dirX, dirY);
        }

        static EdgeArrow Pin(float halfW, float halfH, float margin, float dx, float dy)
        {
            float lw = Math.Max(1f, halfW - margin), lh = Math.Max(1f, halfH - margin);
            float ax = Math.Abs(dx) / lw, ay = Math.Abs(dy) / lh;
            float k = 1f / Math.Max(ax, ay);
            float angle = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);
            return new EdgeArrow(true, dx * k, dy * k, angle);
        }
    }
}
