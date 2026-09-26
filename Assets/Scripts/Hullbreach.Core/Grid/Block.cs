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

        // frob:doc docs/reference/hullbreach-core.md#block
        public Block(byte typeId, byte modifiers = 0, byte damage = 0)
        {
            TypeId = typeId;
            Modifiers = modifiers;
            Damage = damage;
        }

        // frob:doc docs/reference/hullbreach-core.md#block
        public float DamageFraction => Damage / 255f;

        // frob:doc docs/reference/hullbreach-core.md#block
        public Block WithDamage(byte damage) => new Block(TypeId, Modifiers, damage);

        // frob:doc docs/reference/hullbreach-core.md#block
        public Block WithModifiers(byte modifiers) => new Block(TypeId, modifiers, Damage);
    }
}
