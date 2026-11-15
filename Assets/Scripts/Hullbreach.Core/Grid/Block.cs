namespace Hullbreach.Core
{
    // Deliberately a small readonly struct (flyweight; see
    // docs/reference/hullbreach-core.md#block for why).
    // frob:doc docs/reference/hullbreach-core.md#block
    public readonly struct Block
    {
        // frob:doc docs/reference/hullbreach-core.md#block
        public readonly byte TypeId;

        // Interpretation belongs to BlockType.
        // frob:doc docs/reference/hullbreach-core.md#block
        public readonly byte Modifiers;

        // Quantized 0..255 across the range 0..1.
        // frob:doc docs/reference/hullbreach-core.md#block
        public readonly byte Damage;

        // Bit 0 = welded (S44): the builder may not remove it. Kept out of
        // Modifiers because that byte is already fully allocated.
        // frob:doc docs/reference/hullbreach-core.md#block
        public readonly byte Flags;

        // frob:doc docs/reference/hullbreach-core.md#block
        public const byte FlagWelded = 1;

        // frob:doc docs/reference/hullbreach-core.md#block
        public Block(byte typeId, byte modifiers = 0, byte damage = 0, byte flags = 0)
        {
            TypeId = typeId;
            Modifiers = modifiers;
            Damage = damage;
            Flags = flags;
        }

        // frob:doc docs/reference/hullbreach-core.md#block
        public bool IsWelded => (Flags & FlagWelded) != 0;

        // frob:doc docs/reference/hullbreach-core.md#block
        public Block WithWelded(bool welded) =>
            new Block(TypeId, Modifiers, Damage, (byte)(welded ? Flags | FlagWelded : Flags & ~FlagWelded));

        // frob:doc docs/reference/hullbreach-core.md#block
        public float DamageFraction => Damage / 255f;

        // frob:doc docs/reference/hullbreach-core.md#block
        public Block WithDamage(byte damage) => new Block(TypeId, Modifiers, damage, Flags);

        // frob:doc docs/reference/hullbreach-core.md#block
        public Block WithModifiers(byte modifiers) => new Block(TypeId, modifiers, Damage, Flags);
    }
}
