using System;
using Unity.Mathematics;

namespace Hullbreach.Core
{
    // Immutable, shared, one per kind of block; the "type object" half of
    // the flyweight (see docs/reference/hullbreach-core.md#blocktype).
    // frob:doc docs/reference/hullbreach-core.md#blocktype
    public readonly struct BlockType
    {
        // frob:doc docs/reference/hullbreach-core.md#blocktype
        public const float Width = 1.0f;

        // frob:doc docs/reference/hullbreach-core.md#blocktype
        public const float Height = 1.0f; 

        // frob:doc docs/reference/hullbreach-core.md#blocktype
        public readonly string Name;

        // Feeds the mass accumulators.
        // frob:doc docs/reference/hullbreach-core.md#blocktype
        public readonly float Mass;

        // K_e = E * KHat[PoissonClass].
        // frob:doc docs/reference/hullbreach-core.md#blocktype
        public readonly float YoungsModulus;

        // Index into the precomputed KHat table.
        // frob:doc docs/reference/hullbreach-core.md#blocktype
        public readonly byte PoissonClass;

        // Ductile limit, vs von Mises of the quasi-static load case.
        // frob:doc docs/reference/hullbreach-core.md#blocktype
        public readonly float YieldStress;

        // Brittle limit, vs max tensile principal stress (impulsive case).
        // frob:doc docs/reference/hullbreach-core.md#blocktype
        public readonly float SpallStress;

        // Brittle limit in compression, larger than SpallStress.
        // frob:doc docs/reference/hullbreach-core.md#blocktype
        public readonly float CompressiveStress;

        // frob:doc docs/reference/hullbreach-core.md#blocktype
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

    // The block type table. Index with Block.TypeId.
    // frob:doc docs/reference/hullbreach-core.md#blocktypes
    public static class BlockTypes
    {
        // frob:doc docs/reference/hullbreach-core.md#blocktypes
        public const byte Core = 0;
        // frob:doc docs/reference/hullbreach-core.md#blocktypes
        public const byte Hull = 1;
        // frob:doc docs/reference/hullbreach-core.md#blocktypes
        public const byte Armor = 2;
        // frob:doc docs/reference/hullbreach-core.md#blocktypes
        public const byte Thruster = 3;
        // frob:doc docs/reference/hullbreach-core.md#blocktypes
        public const byte Cannon = 4;
        // frob:doc docs/reference/hullbreach-core.md#blocktypes
        public const byte Fin = 5;
        // Pushes the ship BACKWARD via two forward-exhausting side nozzles.
        // frob:doc docs/reference/hullbreach-core.md#blocktypes
        public const byte RetroThruster = 6;

        // Balance table, normalized against Hull.YieldStress = 1.0; see
        // docs/reference/hullbreach-core.md#blocktypes for per-type rationale.
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

        // frob:doc docs/reference/hullbreach-core.md#blocktypes
        public static BlockType Get(byte typeId) => Table[typeId];

        // frob:doc docs/reference/hullbreach-core.md#blocktypes
        public static int Count => Table.Length;

        // Folds in damage softening; see docs/reference/hullbreach-core.md#blocktypes.
        // TODO [D3]: the floor (0.05) is a placeholder curve.
        // frob:doc docs/reference/hullbreach-core.md#blocktypes
        public static float EffectiveStiffness(in Block block)
        {
            float softening = math.max(0.05f, 1f - block.DamageFraction);
            return Get(block.TypeId).YoungsModulus * softening;
        }
    }
}
