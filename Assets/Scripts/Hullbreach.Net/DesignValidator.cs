using System.Collections.Generic;
using Hullbreach.Core;

namespace Hullbreach.Net
{
    // Why a client-supplied design was refused; Ok means it may be built.
    // frob:doc docs/reference/hullbreach-net.md#designvalidator
    public enum DesignVerdict
    {
        Ok,
        NoBlocks,
        TooManyBlocks,
        UnknownBlockType,
        DuplicateCell,
        NeedsExactlyOneCore,
        Disconnected,
    }

    // The server never trusts a client's ShipSnapshot: a design is checked
    // here before any ship is built from it (INV-003).
    // frob:doc docs/reference/hullbreach-net.md#designvalidator
    // frob:invariant INV-003
    public static class DesignValidator
    {
        // Spawn speed ceiling in world units per second; faster claims are scaled down.
        // frob:doc docs/reference/hullbreach-net.md#designvalidator
        public const float MaxSpawnSpeed = 50f;

        // Checks block count, types, cells, the single core and connectivity.
        // frob:doc docs/reference/hullbreach-net.md#designvalidator
        public static DesignVerdict ValidateDesign(ShipSnapshot design)
        {
            var blocks = design.Blocks;
            if (blocks == null || blocks.Length == 0) return DesignVerdict.NoBlocks;
            if (blocks.Length > ShipSnapshot.MaxBlocks) return DesignVerdict.TooManyBlocks;

            var grid = new BlockGrid();
            int cores = 0;
            foreach (var b in blocks)
            {
                if (b.TypeId >= BlockTypes.Count) return DesignVerdict.UnknownBlockType;
                if (b.TypeId == BlockTypes.Core) cores++;
                // Damage is deliberately not carried over: a new ship starts intact.
                if (!grid.TryAdd(BlockKey.Pack(b.X, b.Y), new Block(b.TypeId, b.Mods, 0)))
                    return cores > 1 && b.TypeId == BlockTypes.Core
                        ? DesignVerdict.NeedsExactlyOneCore
                        : DesignVerdict.DuplicateCell;
            }
            if (cores != 1) return DesignVerdict.NeedsExactlyOneCore;

            var stranded = new List<int>();
            Connectivity.FindDetached(grid, stranded);
            return stranded.Count == 0 ? DesignVerdict.Ok : DesignVerdict.Disconnected;
        }
    }
}
