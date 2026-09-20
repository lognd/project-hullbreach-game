using System.Collections.Generic;
using Hullbreach.Core;

namespace Hullbreach.Builder
{
    /// <summary>Placement and removal validity for the two-click builder (S30, S31, S32).</summary>
    public static class PlacementRules
    {
        static readonly int[] Neighbors = new int[4];

        /// <summary>
        /// A cell is valid for a NON-CORE block when it is in range, empty, and
        /// 4-adjacent to an existing block. This overload never accepts an empty
        /// grid (S32: exactly one core, and it must be the first block placed),
        /// so callers placing the very first block must go through the
        /// type-aware overload.
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
        /// falls back to the ordinary adjacency rule.
        /// </summary>
        public static bool CanPlace(BlockGrid grid, int key, byte typeId)
        {
            bool isCore = typeId == BlockTypes.Core;

            if (grid.Count == 0)
            {
                // Only a core may seed an empty grid, and it may go anywhere
                // in range -- there is nothing yet to be adjacent to.
                if (!isCore) return false;
                BlockKey.Unpack(key, out int x, out int y);
                return BlockKey.InRange(x, y);
            }

            // A second core is never allowed once the grid is non-empty.
            if (isCore) return false;

            return CanPlaceCommon(grid, key);
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
        /// blocks is ALLOWED, not refused -- the stranded blocks detach along
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
