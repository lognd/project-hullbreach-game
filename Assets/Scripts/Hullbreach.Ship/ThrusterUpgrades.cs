namespace Hullbreach.Ship
{
    // Decodes Modifiers bits 2-3 (mask 0b1100) into a throttle ramp rate:
    // thrusters, retro thrusters and fins all ramp their control value
    // toward a target rather than snapping to it, and all three share this
    // encoding so one block-modifier byte carries both facing (low 2 bits)
    // and ramp level (next 2 bits).
    // frob:doc docs/reference/hullbreach-ship.md#thrusterupgrades
    public static class ThrusterUpgrades
    {
        // frob:doc docs/reference/hullbreach-ship.md#thrusterupgrades
        public const byte Mask = 0b1100;

        // Shift to bring the ramp-upgrade field down to 0..3.
        const int Shift = 2;

        // Seconds to go from zero to full throttle at each upgrade level
        // 0..3. Tunable; level 3 is a near-instant reaction thruster, level
        // 0 is the sluggish stock part.
        static readonly float[] SecondsToFull = { 1.0f, 0.6f, 0.35f, 0.15f };

        // frob:doc docs/reference/hullbreach-ship.md#thrusterupgrades
        public static float RampRate(byte modifiers)
        {
            int level = (modifiers & Mask) >> Shift;
            return 1f / SecondsToFull[level];
        }
    }
}
