namespace Hullbreach.Ship
{
    /// <summary>
    /// Decodes Modifiers bits 2-3 (mask 0b1100) into a throttle ramp rate for
    /// thrusters, retro thrusters and fins alike -- all three ramp their
    /// control value toward a target rather than snapping to it, and all
    /// three share the same upgrade encoding so one block-modifier byte can
    /// carry both facing (low 2 bits) and ramp level (next 2 bits).
    /// </summary>
    public static class ThrusterUpgrades
    {
        /// <summary>Bit mask isolating the ramp-upgrade field within Modifiers.</summary>
        public const byte Mask = 0b1100;

        /// <summary>Shift to bring the ramp-upgrade field down to 0..3.</summary>
        const int Shift = 2;

        /// <summary>Seconds to go from zero to full throttle at each upgrade
        /// level 0..3. Tunable; level 3 is a near-instant reaction thruster,
        /// level 0 is the sluggish stock part.</summary>
        static readonly float[] SecondsToFull = { 1.0f, 0.6f, 0.35f, 0.15f };

        /// <summary>Throttle change per second implied by the ramp-upgrade
        /// bits of `modifiers`. The same rate is used ramping up and down.</summary>
        public static float RampRate(byte modifiers)
        {
            int level = (modifiers & Mask) >> Shift;
            return 1f / SecondsToFull[level];
        }
    }
}
