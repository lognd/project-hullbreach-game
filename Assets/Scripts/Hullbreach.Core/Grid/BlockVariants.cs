namespace Hullbreach.Core
{
    /// <summary>
    /// Decodes bits 4-7 of Block.Modifiers into a per-type "variant" id.
    /// Variant 0 is always the base behaviour for a type (e.g. plain
    /// Cannon); nonzero variants are alternate behaviours registered against
    /// the same TypeId (e.g. the gravity gun is Cannon variant 1). Lives in
    /// Core, alongside Facing (bits 0-1) and ThrusterUpgrades (bits 2-3),
    /// since builder placement code needs to read/write variant bits without
    /// depending on Hullbreach.Ship.
    /// </summary>
    public static class BlockVariants
    {
        /// <summary>Bit mask isolating the variant field within Modifiers.</summary>
        public const byte Mask = 0xF0;

        /// <summary>Shift to bring the variant field down to 0..15.</summary>
        const int Shift = 4;

        /// <summary>Variant id (0..15) encoded in `modifiers`. 0 means the
        /// type's base/default behaviour.</summary>
        public static byte Get(byte modifiers) => (byte)((modifiers & Mask) >> Shift);

        /// <summary>Returns `modifiers` with its variant field replaced by
        /// `variant` (only the low 4 bits of `variant` are used), preserving
        /// facing and ramp-upgrade bits untouched.</summary>
        public static byte With(byte modifiers, byte variant)
        {
            byte low = (byte)(modifiers & ~Mask);
            byte high = (byte)((variant << Shift) & Mask);
            return (byte)(low | high);
        }
    }
}
