namespace Hullbreach.Ship
{
    // Decodes Modifiers bits 2-3 (mask 0b1100) into a throttle ramp rate,
    // shared by thrusters, retro thrusters and fins.
    // frob:doc docs/reference/hullbreach-ship.md#thrusterupgrades
    public static class ThrusterUpgrades
    {
        // frob:doc docs/reference/hullbreach-ship.md#thrusterupgrades
        public const byte Mask = 0b1100;

        // Shift to bring the ramp-upgrade field down to 0..3.
        const int Shift = 2;

        // Seconds to full throttle per upgrade level 0..3; level 3 is
        // near-instant, level 0 is the sluggish stock part.
        static readonly float[] SecondsToFull = { 1.0f, 0.6f, 0.35f, 0.15f };

        // frob:doc docs/reference/hullbreach-ship.md#thrusterupgrades
        public static float RampRate(byte modifiers)
        {
            int level = (modifiers & Mask) >> Shift;
            return 1f / SecondsToFull[level];
        }
    }
}
