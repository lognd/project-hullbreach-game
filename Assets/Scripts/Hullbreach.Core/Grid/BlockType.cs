using System;

namespace Hullbreach.Core
{
    /// <summary>
    /// Immutable, shared, one per kind of block. Looked up by Block.TypeId.
    /// This is the "type object" half of the flyweight.
    ///
    /// UNITS: do not use SI. Steel is E = 200e9 Pa with yield 250e6 Pa, and
    /// running CG on numbers spanning 1e9 throws away float precision and
    /// wrecks conditioning. Normalize instead -- plain hull has YieldStress
    /// 1.0 and everything else is relative to it. The solver behaves better
    /// and the numbers stay readable to whoever balances the game.
    /// </summary>
    public readonly struct BlockType
    {
        /// <summary>Width of a single block. (All blocks should be the same size). </summary>
        public const float Width = 1.0f;
        /// <summary>Height of a single block. (All blocks should be the same size).</summary>
        public const float Height = 1.0f; 

        public readonly string Name;

        /// <summary>Mass of one block. Feeds the mass accumulators.</summary>
        public readonly float Mass;

        /// <summary>Young's modulus. Factors straight out of the element
        /// stiffness, since K_e = E * KHat[PoissonClass].</summary>
        public readonly float YoungsModulus;

        /// <summary>Index into the precomputed KHat table. Poisson's ratio is
        /// quantized into a few classes because K is not linear in nu, so a
        /// continuous nu would cost you the precomputation entirely.</summary>
        public readonly byte PoissonClass;

        /// <summary>Ductile limit. Compared against von Mises of the
        /// quasi-static load case.</summary>
        public readonly float YieldStress;

        /// <summary>Brittle limit. Compared against max TENSILE principal
        /// stress of the impulsive load case.</summary>
        public readonly float SpallStress;

        /// <summary>Brittle limit in compression. Much larger than SpallStress
        /// for armor-like materials -- brittle solids are far stronger in
        /// compression than in tension.</summary>
        public readonly float CompressiveStress;

        public BlockType(string name, float mass, float youngsModulus, byte poissonClass,
                         float yieldStress, float spallStress, float compressiveStress)
        {
            Name = name;
            Mass = mass;
            YoungsModulus = youngsModulus;
            PoissonClass = poissonClass;
            YieldStress = yieldStress;
            SpallStress = spallStress;
            CompressiveStress = compressiveStress;
        }
    }

    /// <summary>The block type table. Index with Block.TypeId.</summary>
    public static class BlockTypes
    {
        public const byte Core = 0;
        public const byte Hull = 1;
        public const byte Armor = 2;
        public const byte Thruster = 3;
        public const byte Cannon = 4;

        // TODO [A1]: Put real numbers here once there is something to balance
        //            against. Keep Hull.YieldStress at 1.0 as the reference.
        //            Armor wants high SpallStress and lowish YieldStress;
        //            hull wants the reverse. S33 is the palette the player sees.
        static readonly BlockType[] Table =
        {
            //             name         mass   E     nu#  yield  spall  compress
            new BlockType("Core",       1f,    1f,   0,   1f,    1f,    1f),
            new BlockType("Hull",       1f,    1f,   0,   1f,    1f,    1f),
            new BlockType("Armor",     1f,    1f,   0,   1f,    1f,    1f),
            new BlockType("Thruster",   1f,    1f,   0,   1f,    1f,    1f),
            new BlockType("Cannon",     1f,    1f,   0,   1f,    1f,    1f),
        };

        public static BlockType Get(byte typeId) => Table[typeId];

        public static int Count => Table.Length;

        /// <summary>
        /// Effective Young's modulus after upgrades and damage softening.
        /// This one scalar is the entire reason K_e = E * KHat works, so
        /// everything that changes stiffness must go through here.
        /// </summary>
        // TODO [D3]: Fold in damage softening. A yielded block should get less
        //            stiff so it sheds load to its neighbors -- that shedding
        //            is what makes ductile failure actually read as ductile,
        //            without needing a nonlinear solve.
        public static float EffectiveStiffness(in Block block)
            => throw new NotImplementedException();
    }
}
