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
        // Kept in Builder, not Core: cost is build economy, not physics (see BlockCosts).
        static readonly BlockCosts Costs = BlockCosts.Default();

        // Thruster/RetroThruster push a fixed ship-local direction, so
        // only Cannon and Fin need the second (orienting) click.
        // frob:doc docs/reference/hullbreach-builder.md#blockpalette
        public static bool IsSymmetric(byte typeId)
            => typeId != BlockTypes.Cannon && typeId != BlockTypes.Fin;

        // frob:doc docs/reference/hullbreach-builder.md#blockpalette
        public static IEnumerable<PaletteEntry> All()
        {
            // Adding a BlockTypes entry without a cost row must fail loudly, not index past the table.
            if (Costs.Validate() != null)
                throw new System.InvalidOperationException("BlockCosts must have one entry per BlockTypes entry");

            for (byte typeId = 0; typeId < BlockTypes.Count; typeId++)
            {
                var type = BlockTypes.Get(typeId);
                yield return new PaletteEntry(typeId, type.Name, type.Mass, Costs.CostOf(typeId), IsSymmetric(typeId));
            }
        }
    }
}
