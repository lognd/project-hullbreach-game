using System.Collections.Generic;
using Hullbreach.Core;

namespace Hullbreach.Builder
{
    // Why a placement was accepted or refused; every non-Ok member names
    // the specific rule so the HUD can explain it, not just flash red.
    // frob:doc docs/reference/hullbreach-builder.md#placementverdict
    public enum PlacementVerdict
    {
        Ok,
        OutOfRange,
        Occupied,
        NotAdjacent,
        NeedsEmptyGrid,
        CoreAlreadyPlaced,
        BlocksExhaust,
        BlocksMuzzle,
        BlocksFin,
        FinNeedsHull,
        InsideReservedCell,
    }

    // Placement and removal validity for the two-click builder (S30, S31, S32).
    // frob:doc docs/reference/hullbreach-builder.md#placementrules
    public static class PlacementRules
    {
        static readonly int[] Neighbors = new int[4];
        static readonly List<int> ReservedScratch = new List<int>();

        // Range/empty/adjacency only, never accepts an empty grid (S32).
        // frob:doc docs/reference/hullbreach-builder.md#placementrules
        public static bool CanPlace(BlockGrid grid, int key)
        {
            if (grid.Count == 0) return false;
            return CanPlaceCommon(grid, key);
        }

        // Adds S32's core rule (empty grid takes only a core).
        // frob:doc docs/reference/hullbreach-builder.md#placementrules
        public static bool CanPlace(BlockGrid grid, int key, byte typeId)
            => CanPlace(grid, key, typeId, 0, out _);

        // Full check: range, core-seeding, occupancy, adjacency, clearance;
        // see docs/reference/hullbreach-builder.md#placementrules.
        // frob:doc docs/reference/hullbreach-builder.md#placementrules
        public static bool CanPlace(BlockGrid grid, int key, byte typeId, byte modifiers, out PlacementVerdict why)
        {
            BlockKey.Unpack(key, out int x, out int y);
            if (!BlockKey.InRange(x, y))
            {
                why = PlacementVerdict.OutOfRange;
                return false;
            }

            bool isCore = typeId == BlockTypes.Core;

            if (grid.Count == 0)
            {
                // Only a core may seed an empty grid, and it may go anywhere
                // in range: there is nothing yet to be adjacent to.
                if (!isCore)
                {
                    why = PlacementVerdict.NeedsEmptyGrid;
                    return false;
                }
                why = PlacementVerdict.Ok;
                return true;
            }

            // A second core is never allowed once the grid is non-empty.
            if (isCore)
            {
                why = PlacementVerdict.CoreAlreadyPlaced;
                return false;
            }

            if (grid.Contains(key))
            {
                why = PlacementVerdict.Occupied;
                return false;
            }

            BlockKey.Neighbors(key, Neighbors);
            bool adjacent = false;
            for (int i = 0; i < 4; i++)
            {
                if (grid.Contains(Neighbors[i])) { adjacent = true; break; }
            }
            if (!adjacent)
            {
                why = PlacementVerdict.NotAdjacent;
                return false;
            }

            // The new block must not land inside an EXISTING block's reserved
            // cell; every relation is one orthogonal step, so 4 neighbors suffice.
            for (int i = 0; i < 4; i++)
            {
                int neighborKey = Neighbors[i];
                if (!grid.TryGet(neighborKey, out Block neighborBlock)) continue;

                Clearance.TryReservedCells(neighborKey, neighborBlock.TypeId, neighborBlock.Modifiers, ReservedScratch);
                if (ReservedScratch.Contains(key))
                {
                    why = PlacementVerdict.InsideReservedCell;
                    return false;
                }
            }

            // The new block's own reserved cells (its exhaust, muzzle or
            // clear-ahead space) must be empty.
            Clearance.TryReservedCells(key, typeId, modifiers, ReservedScratch);
            for (int i = 0; i < ReservedScratch.Count; i++)
            {
                if (grid.Contains(ReservedScratch[i]))
                {
                    why = typeId switch
                    {
                        BlockTypes.Cannon => PlacementVerdict.BlocksMuzzle,
                        BlockTypes.Fin => PlacementVerdict.BlocksFin,
                        _ => PlacementVerdict.BlocksExhaust,
                    };
                    return false;
                }
            }

            // A Fin must be mounted on a non-Fin block behind it.
            if (Clearance.RequiredAnchor(key, typeId, modifiers, out int anchorKey))
            {
                bool hasHull = anchorKey != -1
                            && grid.TryGet(anchorKey, out Block anchorBlock)
                            && anchorBlock.TypeId != BlockTypes.Fin;
                if (!hasHull)
                {
                    why = PlacementVerdict.FinNeedsHull;
                    return false;
                }
            }

            why = PlacementVerdict.Ok;
            return true;
        }

        // Shared range/empty/adjacency check for a non-empty grid.
        static bool CanPlaceCommon(BlockGrid grid, int key)
        {
            BlockKey.Unpack(key, out int x, out int y);
            if (!BlockKey.InRange(x, y)) return false;
            if (grid.Contains(key)) return false;

            BlockKey.Neighbors(key, Neighbors);
            for (int i = 0; i < 4; i++)
            {
                if (grid.Contains(Neighbors[i])) return true;
            }
            return false;
        }

        // Present and not the core. DECISION: a removal that strands other
        // blocks is ALLOWED (see Detach), not refused.
        // frob:doc docs/reference/hullbreach-builder.md#placementrules
        public static bool CanRemove(BlockGrid grid, int key)
        {
            if (!grid.Contains(key)) return false;
            if (grid.CoreKey.HasValue && key == grid.CoreKey.Value) return false;
            return true;
        }

        // Removes `key` plus anything stranded per the detach rule; uses
        // Articulation as a fast path to skip the flood fill when possible.
        // frob:doc docs/reference/hullbreach-builder.md#placementrules
        public static bool Detach(BlockGrid grid, int key, List<int> removed)
        {
            removed.Clear();
            if (!CanRemove(grid, key)) return false;

            var cuts = new HashSet<int>();
            Articulation.Compute(grid, cuts);
            bool isCut = cuts.Contains(key);

            grid.TryRemove(key);
            removed.Add(key);

            if (isCut)
            {
                var detached = new List<int>();
                Connectivity.FindDetached(grid, detached);
                foreach (var strandedKey in detached)
                {
                    grid.TryRemove(strandedKey);
                    removed.Add(strandedKey);
                }
            }

            return true;
        }
    }
}
