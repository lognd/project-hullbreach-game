namespace Hullbreach.Core
{
    /// <summary>
    /// One block as stored in the grid. Deliberately a small readonly struct:
    ///
    ///   - no heap allocation, so no GC pressure and no frame hitches
    ///   - stored inline in arrays, so iteration walks cache lines
    ///   - blittable, so Burst can compile over it
    ///
    /// Everything shared by all blocks of a kind (mass, stiffness, strength)
    /// lives in <see cref="BlockType"/> and is looked up by TypeId. That is the
    /// flyweight split, and it is why this is not an abstract base class with a
    /// subclass per block kind: a managed class here would cost a heap object
    /// and a virtual call in the innermost loop of the FE assembly, and would
    /// lock the whole simulation out of Burst.
    /// </summary>
    public readonly struct Block
    {
        /// <summary>Index into the BlockType table.</summary>
        public readonly byte TypeId;

        /// <summary>Upgrade bits. Interpretation belongs to BlockType.</summary>
        public readonly byte Modifiers;

        /// <summary>Accumulated damage, quantized 0..255 across the range 0..1.</summary>
        public readonly byte Damage;

        public Block(byte typeId, byte modifiers = 0, byte damage = 0)
        {
            TypeId = typeId;
            Modifiers = modifiers;
            Damage = damage;
        }

        /// <summary>Damage as a 0..1 fraction.</summary>
        public float DamageFraction => Damage / 255f;

        public Block WithDamage(byte damage) => new Block(TypeId, Modifiers, damage);

        public Block WithModifiers(byte modifiers) => new Block(TypeId, modifiers, Damage);
    }
}
