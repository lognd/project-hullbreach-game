using System.Collections.Generic;
using Hullbreach.Core;

namespace Hullbreach.Builder
{
    /// <summary>
    /// Why a placement was accepted or refused (S30, S31, S32, and the
    /// exhaust/muzzle/fin clearance rules). Ok is the only accepting value;
    /// every other member names the specific rule that refused the cell so
    /// the HUD can explain it instead of just flashing red.
    /// </summary>
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

    /// <summary>Placement and removal validity for the two-click builder (S30, S31, S32).</summary>
    public static class PlacementRules
    {
        static readonly int[] Neighbors = new int[4];
        static readonly List<int> ReservedScratch = new List<int>();

        /// <summary>
        /// A cell is valid for a NON-CORE block when it is in range, empty, and
        /// 4-adjacent to an existing block. This overload never accepts an empty
        /// grid (S32: exactly one core, and it must be the first block placed),
        /// so callers placing the very first block must go through the
        /// type-aware overload. Thin wrapper over the full rule for callers
        /// that only care about adjacency, not clearance.
        /// </summary>
        public static bool CanPlace(BlockGrid grid, int key)
        {
            if (grid.Count == 0) return false;
            return CanPlaceCommon(grid, key);
        }

        /// <summary>
        /// Type-aware placement check that encodes S32's core rule: an empty
        /// grid may ONLY accept a core, and a core may ONLY be placed into an
        /// empty grid (there is exactly one core, ever). Every other type
        /// falls back to the ordinary adjacency rule. Thin wrapper over the
        /// full rule with modifiers = 0 and the verdict discarded.
        /// </summary>
        public static bool CanPlace(BlockGrid grid, int key, byte typeId)
            => CanPlace(grid, key, typeId, 0, out _);

        /// <summary>
        /// Full placement check: range, the core-seeding rule, occupancy,
        /// adjacency, then clearance: the new block's own reserved cells
        /// (Clearance.TryReservedCells) must be empty, the new block must not
        /// sit inside any EXISTING block's reserved cell, and a Fin's anchor
        /// cell (Clearance.RequiredAnchor) must hold a non-Fin block. `why`
        /// names exactly which rule decided the outcome.
        /// </summary>
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
            // cell (e.g. directly behind a thruster, ahead of a cannon or
            // fin, or beside a retro thruster). Every reserved-cell relation
            // is exactly one orthogonal step, so the 4-connected neighbors of
            // `key` are the complete set of cells that could possibly reserve
            // it, so there is no need to walk the whole grid.
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

        /// <summary>Shared range/empty/adjacency check for a non-empty grid.</summary>
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

        /// <summary>
        /// A block can be removed when it is present and is not the core.
        /// DECISION (S31's open question): a removal that would strand other
        /// blocks is ALLOWED, not refused: the stranded blocks detach along
        /// with it (see <see cref="Detach"/>). This keeps single-click removal
        /// always available instead of silently failing near a bottleneck.
        /// </summary>
        public static bool CanRemove(BlockGrid grid, int key)
        {
            if (!grid.Contains(key)) return false;
            if (grid.CoreKey.HasValue && key == grid.CoreKey.Value) return false;
            return true;
        }

        /// <summary>
        /// Remove `key` and, per the detach rule, anything that becomes
        /// unreachable from the core as a result. Every removed key (the
        /// requested one plus any stranded ones) is appended to `removed`.
        /// Uses Articulation as a fast path: if `key` is not an articulation
        /// point, removing it cannot disconnect anything, so the flood fill
        /// is skipped entirely. Returns false without mutating the grid when
        /// `key` cannot be removed (missing or the core).
        /// </summary>
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
