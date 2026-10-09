using System;
using System.Collections.Generic;
using Hullbreach.Core;

namespace Hullbreach.Builder
{
    // One block of a saved design; same fields as the wire's SnapshotBlock,
    // but ints so a hand-edited out-of-range value can be reported, not wrapped.
    // frob:doc docs/reference/hullbreach-builder.md#designblock
    public readonly struct DesignBlock
    {
        // frob:doc docs/reference/hullbreach-builder.md#designblock
        public readonly int X;

        // frob:doc docs/reference/hullbreach-builder.md#designblock
        public readonly int Y;

        // frob:doc docs/reference/hullbreach-builder.md#designblock
        public readonly int TypeId;

        // frob:doc docs/reference/hullbreach-builder.md#designblock
        public readonly int Modifiers;

        // A saved design is pristine, so anything but 0 is reported on load.
        // frob:doc docs/reference/hullbreach-builder.md#designblock
        public readonly int Damage;

        // frob:doc docs/reference/hullbreach-builder.md#designblock
        public DesignBlock(int x, int y, int typeId, int modifiers, int damage)
        {
            X = x;
            Y = y;
            TypeId = typeId;
            Modifiers = modifiers;
            Damage = damage;
        }
    }

    // A named, ordered list of blocks that may or may not satisfy PlacementRules;
    // ShipDesignValidator says which. File format: docs/ship-design-format.md.
    // frob:doc docs/reference/hullbreach-builder.md#shipdesign
    public sealed class ShipDesign
    {
        // The only format version this build writes and the newest it reads.
        // frob:doc docs/reference/hullbreach-builder.md#shipdesign
        public const int CurrentVersion = 1;

        // frob:doc docs/reference/hullbreach-builder.md#shipdesign
        public string Name { get; }

        // frob:doc docs/reference/hullbreach-builder.md#shipdesign
        public IReadOnlyList<DesignBlock> Blocks { get; }

        // frob:doc docs/reference/hullbreach-builder.md#shipdesign
        public ShipDesign(string name, IReadOnlyList<DesignBlock> blocks)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
        }

        // Captures a grid in sorted-key order so equal grids give equal files.
        // frob:doc docs/reference/hullbreach-builder.md#shipdesign
        public static ShipDesign FromGrid(string name, BlockGrid grid)
        {
            var blocks = new List<DesignBlock>(grid.Count);
            foreach (int key in grid.SortedKeys.ToArray())
            {
                grid.TryGet(key, out Block block);
                BlockKey.Unpack(key, out int x, out int y);
                blocks.Add(new DesignBlock(x, y, block.TypeId, block.Modifiers, block.Damage));
            }
            return new ShipDesign(name, blocks);
        }

        // Rebuilds the grid only when the design is fully valid; never repairs one.
        // frob:doc docs/reference/hullbreach-builder.md#shipdesign
        public bool TryBuildGrid(out BlockGrid grid)
        {
            grid = new BlockGrid();
            var problems = new List<DesignProblem>();
            ShipDesignValidator.Replay(this, grid, problems);
            if (problems.Count == 0) return true;
            grid = null;
            return false;
        }
    }
}
