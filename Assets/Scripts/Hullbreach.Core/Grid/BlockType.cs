using System;
using Unity.Mathematics;

namespace Hullbreach.Core
{
    /// <summary>
    /// Immutable, shared, one per kind of block. Looked up by Block.TypeId.
    /// This is the "type object" half of the flyweight.
    ///
    /// UNITS: do not use SI. Steel is E = 200e9 Pa with yield 250e6 Pa, and
    /// running CG on numbers spanning 1e9 throws away float precision and
    /// wrecks conditioning. Normalize instead: plain hull has YieldStress
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
        /// for armor-like materials: brittle solids are far stronger in
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
        public const byte Fin = 5;
        /// <summary>Retro thruster: pushes the ship BACKWARD via two small
        /// side nozzles that exhaust forward. Fires on the reverse key.</summary>
        public const byte RetroThruster = 6;

        // Normalized against Hull.YieldStress = 1.0.
        //
        //   Core:     the heaviest, toughest block: it must survive whatever
        //             kills everything around it, so both stresses are the
        //             highest in the table and it is stiffer than hull.
        //   Hull:     the reference. Ductile: ordinary yield, ordinary spall.
        //   Armor:    ~2x hull mass, stiffer (denser lattice), and brittle:
        //             high SpallStress/CompressiveStress (brittle solids take
        //             compression far better than tension) but LOWER
        //             YieldStress than hull: it is meant to shatter rather
        //             than bend.
        //   Thruster: hull-like stiffness/strength, a bit heavier for the
        //             machinery packed inside.
        //   Cannon:   same idea as Thruster: hull-like structurally, a
        //             little heavier for its mechanism.
        //   Fin:      light control surface: cheap mass so placement is
        //             about leverage, not weight, and slightly weaker than
        //             hull since it is a thin surface rather than a hull
        //             plate.
        static readonly BlockType[] Table =
        {
            //             name         mass   E     nu#  yield  spall  compress
            new BlockType("Core",       3.0f,  1.5f, 0,   2.0f,  2.0f,  2.0f),
            new BlockType("Hull",       1.0f,  1.0f, 0,   1.0f,  1.0f,  1.0f),
            new BlockType("Armor",      2.0f,  1.4f, 0,   0.7f,  2.5f,  4.0f),
            new BlockType("Thruster",   1.2f,  1.0f, 0,   1.0f,  1.0f,  1.0f),
            new BlockType("Cannon",     1.3f,  1.0f, 0,   1.0f,  1.0f,  1.0f),
            new BlockType("Fin",        0.6f,  0.9f, 0,   0.9f,  0.9f,  0.9f),
            new BlockType("Retro",      0.9f,  1.0f, 0,   1.0f,  1.0f,  1.0f),
        };

        public static BlockType Get(byte typeId) => Table[typeId];

        public static int Count => Table.Length;

        /// <summary>
        /// Effective Young's modulus after upgrades and damage softening.
        /// This one scalar is the entire reason K_e = E * KHat works, so
        /// everything that changes stiffness must go through here.
        /// </summary>
        // TODO [D3]: The floor (0.05) is a placeholder curve that just keeps K
        //            non-singular; a later DamageModel may replace it with
        //            something that better matches real ductile softening.
        /// <summary>
        /// Fold in damage softening. A yielded block gets less stiff so it
        /// sheds load to its neighbors: that shedding is what makes ductile
        /// failure actually read as ductile, without needing a nonlinear
        /// solve. Floored at 5% of nominal E so the stiffness matrix never
        /// goes singular even at full damage.
        /// </summary>
        public static float EffectiveStiffness(in Block block)
        {
            float softening = math.max(0.05f, 1f - block.DamageFraction);
            return Get(block.TypeId).YoungsModulus * softening;
        }
    }
}
