using System.Collections.Generic;
using Hullbreach.Core;

namespace Hullbreach.Builder
{
    /// <summary>One selectable palette entry (S33 criterion 1).</summary>
    public readonly struct PaletteEntry
    {
        /// <summary>Index into the BlockTypes table.</summary>
        public readonly byte TypeId;

        /// <summary>Display name, taken straight from BlockTypes.</summary>
        public readonly string Name;

        /// <summary>Mass of one block of this type, shown so a build's total mass is predictable before placing it.</summary>
        public readonly float Mass;

        /// <summary>Builder economy cost. Not physics, so it lives in Builder rather than Core.</summary>
        public readonly int Cost;

        /// <summary>True when the block has no facing, so the two-click flow commits on the first click (S30).</summary>
        public readonly bool Symmetric;

        public PaletteEntry(byte typeId, string name, float mass, int cost, bool symmetric)
        {
            TypeId = typeId;
            Name = name;
            Mass = mass;
            Cost = cost;
            Symmetric = symmetric;
        }
    }

    /// <summary>
    /// Enumerates every entry in the BlockTypes table with the builder-facing
    /// cost and symmetry that Core has no reason to know about.
    /// </summary>
    public static class BlockPalette
    {
        /// <summary>
        /// Per-type builder cost. Kept here, not in Core, because cost is
        /// build economy rather than physics -- Core only knows mass and
        /// structural properties.
        /// </summary>
        static readonly int[] Cost =
        {
            0, // Core
            1, // Hull
            3, // Armor
            4, // Thruster
            5, // Cannon
            2, // Fin
        };

        /// <summary>
        /// True when a block type has no facing to orient. Thruster, Cannon
        /// and Fin point somewhere and so require the second click; every
        /// other type commits on the first click.
        /// </summary>
        public static bool IsSymmetric(byte typeId)
            => typeId != BlockTypes.Thruster && typeId != BlockTypes.Cannon && typeId != BlockTypes.Fin;

        /// <summary>Every palette entry, in BlockTypes table order.</summary>
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
