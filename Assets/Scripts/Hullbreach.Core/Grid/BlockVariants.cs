namespace Hullbreach.Core
{
    // Decodes bits 4-7 of Block.Modifiers into a per-type "variant" id; see
    // docs/reference/hullbreach-core.md#blockvariants.
    // frob:doc docs/reference/hullbreach-core.md#blockvariants
    public static class BlockVariants
    {
        // frob:doc docs/reference/hullbreach-core.md#blockvariants
        public const byte Mask = 0xF0;

        const int Shift = 4;

        // 0 means the type's base/default behaviour.
        // frob:doc docs/reference/hullbreach-core.md#blockvariants
        public static byte Get(byte modifiers) => (byte)((modifiers & Mask) >> Shift);

        // Preserves facing and ramp-upgrade bits untouched.
        // frob:doc docs/reference/hullbreach-core.md#blockvariants
        public static byte With(byte modifiers, byte variant)
        {
            byte low = (byte)(modifiers & ~Mask);
            byte high = (byte)((variant << Shift) & Mask);
            return (byte)(low | high);
        }
    }
}
