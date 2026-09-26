using System.Collections.Generic;
using Hullbreach.Core;

namespace Hullbreach.Builder
{
    // One selectable palette entry (S33 criterion 1).
    // frob:doc docs/reference/hullbreach-builder.md#paletteentry
    public readonly struct PaletteEntry
    {
        // frob:doc docs/reference/hullbreach-builder.md#paletteentry
        public readonly byte TypeId;

        // Taken straight from BlockTypes.
        // frob:doc docs/reference/hullbreach-builder.md#paletteentry
        public readonly string Name;

        // Shown so a build's total mass is predictable before placing it.
        // frob:doc docs/reference/hullbreach-builder.md#paletteentry
        public readonly float Mass;

        // Not physics, so it lives in Builder rather than Core.
        // frob:doc docs/reference/hullbreach-builder.md#paletteentry
        public readonly int Cost;

        // True when the block has no facing, so the two-click flow commits
        // on the first click (S30).
        // frob:doc docs/reference/hullbreach-builder.md#paletteentry
        public readonly bool Symmetric;

        // frob:doc docs/reference/hullbreach-builder.md#paletteentry
        public PaletteEntry(byte typeId, string name, float mass, int cost, bool symmetric)
        {
            TypeId = typeId;
            Name = name;
            Mass = mass;
            Cost = cost;
            Symmetric = symmetric;
        }
    }

    // Enumerates every entry in the BlockTypes table with the builder-facing
    // cost and symmetry that Core has no reason to know about.
    // frob:doc docs/reference/hullbreach-builder.md#blockpalette
    public static class BlockPalette
    {
        // Kept here, not in Core: cost is build economy, not physics.
        static readonly int[] Cost =
        {
            0, // Core
            1, // Hull
            3, // Armor
            4, // Thruster
            5, // Cannon
            2, // Fin
            3, // RetroThruster
        };

        // Thruster/RetroThruster push a fixed ship-local direction, so
        // only Cannon and Fin need the second (orienting) click.
        // frob:doc docs/reference/hullbreach-builder.md#blockpalette
        public static bool IsSymmetric(byte typeId)
            => typeId != BlockTypes.Cannon && typeId != BlockTypes.Fin;

        // frob:doc docs/reference/hullbreach-builder.md#blockpalette
        public static IEnumerable<PaletteEntry> All()
        {
            for (byte typeId = 0; typeId < BlockTypes.Count; typeId++)
            {
                var type = BlockTypes.Get(typeId);
                yield return new PaletteEntry(typeId, type.Name, type.Mass, Cost[typeId], IsSymmetric(typeId));
            }
        }
    }
}
