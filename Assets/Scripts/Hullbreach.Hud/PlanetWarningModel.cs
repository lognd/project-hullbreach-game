using System;

namespace Hullbreach.Hud
{
    // What the planet warning shows this frame.
    // frob:doc docs/design/ux-gx.md#planet-warning
    public readonly struct PlanetWarning
    {
        // 0 none, 1 notice, 2 caution, 3 danger, 4 catastrophic (same scale as ImpactSeverity).
        public readonly int Level;
        // Headline; empty when nothing needs saying.
        public readonly string Text;
        // Draw the arrow pointing away from the planet.
        public readonly bool ShowArrow;
        // The ship sits low and its thrusters cannot beat gravity.
        public readonly bool Trapped;
        // 0..1 strength of the red-orange edge tint.
        public readonly float EdgeAlpha;
        // Seconds between warning beeps; 0 means silent.
        public readonly float BeepInterval;

        public PlanetWarning(int level, string text, bool showArrow, bool trapped, float edgeAlpha, float beepInterval)
        { Level = level; Text = text; ShowArrow = showArrow; Trapped = trapped; EdgeAlpha = edgeAlpha; BeepInterval = beepInterval; }
    }

    // Engine-free decisions behind the planet-crash warning: how early it speaks, whether the ship is stuck in a
    // gravity well, how strong the edge tint is and which way the "pull up" arrow points. The ship supplies the
    // numbers (PlanetProximity); FlightHudView draws what comes back.
    // frob:doc docs/design/ux-gx.md#planet-warning
    public static class PlanetWarningModel
    {
        // The warning starts this many seconds before a damaging touchdown (the ship's own window is shorter).
        public const float EarlySeconds = 10f;
        // ...or as soon as the stopping distance, with this much margin, is as long as the gap left.
        public const float StopMargin = 1.5f;
        // Braking becomes mandatory (BRAKE) when the stopping distance with this margin reaches the gap.
        public const float BrakeMargin = 1.3f;
        // Below this gap the ship counts as low for the gravity trap check.
        public const float TrapAltitude = 12f;
        // A ship this low and not climbing is sitting on the surface.
        public const float LandedAltitude = 2f;
        // Thruster acceleration beyond gravity (units/s^2) under which the ship cannot climb away.
        public const float MinClimbAccel = 0.5f;
        // Closing speed below which the ship is not really falling.
        public const float SlowClosing = 1.5f;
        public const string TrapText = "GRAVITY TRAP - THRUST AWAY";
        public const string LandedText = "On the surface - thrust away to lift off";

        // altitude: gap to the surface; closing: speed toward it (negative while rising); tangential: speed along it;
        // timeToImpact and predictedImpact: touchdown time and closing speed at touchdown; safeSpeed: the harmless
        // landing speed; stopDistance: distance full thrust needs to cancel the closing speed (+Infinity when it
        // cannot); netClimbAccel: thruster acceleration minus gravity (negative: the ship cannot lift off);
        // severity/mustBrake/heating: the ship's own classification.
        // frob:doc docs/design/ux-gx.md#planet-warning
        public static PlanetWarning Evaluate(float altitude, float closing, float tangential, float timeToImpact,
            float predictedImpact, float safeSpeed, float stopDistance, float netClimbAccel,
            int severity, bool mustBrake, bool heating)
        {
            int level = Math.Max(0, Math.Min(4, severity));
            bool brake = mustBrake;

            // Speak earlier than the ship's own window when the crash is already unavoidable-ish or soon.
            if (closing > 0.25f && predictedImpact > safeSpeed && level < 3)
            {
                bool soon = timeToImpact <= EarlySeconds;
                bool cannotStop = stopDistance * StopMargin >= altitude;
                if (soon || cannotStop)
                {
                    level = Math.Max(level, 2);
                    if (stopDistance * BrakeMargin >= altitude) { level = 3; brake = true; }
                }
            }

            bool low = altitude <= TrapAltitude && closing < SlowClosing && closing > -2f * SlowClosing && netClimbAccel <= MinClimbAccel && !float.IsNaN(netClimbAccel);
            bool trapped = low;
            bool landed = !trapped && altitude <= LandedAltitude && closing < 1f && tangential < 6f;
            if (trapped) level = Math.Max(level, 3);

            string text = trapped ? TrapText
                : level >= 2 || (level == 1 && heating) ? FlightWarningModel.Text(level, brake, heating, timeToImpact)
                : landed ? LandedText
                : FlightWarningModel.Text(level, brake, heating, timeToImpact);

            bool arrow = trapped || landed || level >= 2;
            float edge = trapped ? 0.4f : level >= 4 ? 0.45f : level == 3 ? 0.32f : level == 2 ? 0.15f : 0f;
            float beep = level >= 4 ? 0.35f : level == 3 ? 0.7f : trapped ? 1.0f : 0f;
            return new PlanetWarning(level, text, arrow, trapped, edge, beep);
        }

        // Rotation in degrees (counter-clockwise) for an arrow sprite that points up at 0, so that it points along the
        // world direction (nx, ny) away from the planet, given the camera's roll in degrees.
        // frob:doc docs/design/ux-gx.md#planet-warning
        public static float ArrowAngleDegrees(float nx, float ny, float cameraRollDegrees)
        {
            return (float)(Math.Atan2(ny, nx) * 180.0 / Math.PI) - 90f - cameraRollDegrees;
        }
    }
}
