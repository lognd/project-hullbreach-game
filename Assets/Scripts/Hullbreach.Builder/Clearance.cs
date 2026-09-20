using System.Collections.Generic;
using Hullbreach.Core;

namespace Hullbreach.Builder
{
    /// <summary>
    /// Per-type "keep this cell empty" / "must be attached here" geometry for
    /// directional and exhaust-bearing blocks. Lives apart from PlacementRules
    /// so both "is my own footprint clear" and "am I standing in someone
    /// else's footprint" can share the exact same cell computation.
    /// </summary>
    public static class Clearance
    {
        /// <summary>
        /// Lists, into `cells` (cleared first), every cell that must stay
        /// empty for a block of `typeId`/`modifiers` sitting at `key`:
        /// Thruster reserves its ship-local -y (exhaust) neighbor, Cannon and
        /// Fin reserve the cell Facing.Ahead of them, and RetroThruster
        /// reserves its +x and -x neighbors. Plain blocks (Core/Hull/Armor)
        /// reserve nothing, so `cells` comes back empty. A reserved direction
        /// that falls outside BlockKey's range is simply omitted -- there is
        /// no cell there to ever be occupied, so it is vacuously satisfied.
        /// Always returns true; the bool return exists so a caller can read
        /// this as "the reservation set was computed" without special-casing
        /// plain types.
        /// </summary>
        public static bool TryReservedCells(int key, byte typeId, byte modifiers, List<int> cells)
        {
            cells.Clear();
            BlockKey.Unpack(key, out int x, out int y);

            switch (typeId)
            {
                case BlockTypes.Thruster:
                    // Fixed ship-local -y exhaust, independent of modifiers:
                    // the thruster has no facing of its own.
                    AddIfInRange(cells, x, y - 1);
                    break;

                case BlockTypes.Cannon:
                case BlockTypes.Fin:
                    {
                        int ahead = Facing.Ahead(key, modifiers);
                        if (ahead != -1) cells.Add(ahead);
                    }
                    break;

                case BlockTypes.RetroThruster:
                    AddIfInRange(cells, x + 1, y);
                    AddIfInRange(cells, x - 1, y);
                    break;
            }

            return true;
        }

        /// <summary>
        /// True when `typeId` requires an anchoring block on some fixed side
        /// of it, with `anchorKey` set to that cell (only Fin, whose anchor is
        /// Facing.Behind -- the hull it mounts on). False, with `anchorKey`
        /// set to -1, for every other type. When the anchor direction itself
        /// falls outside BlockKey's range, `anchorKey` is -1 even though the
        /// return value is true, so the caller sees "there is nowhere for the
        /// required hull to be" and refuses the placement.
        /// </summary>
        public static bool RequiredAnchor(int key, byte typeId, byte modifiers, out int anchorKey)
        {
            if (typeId == BlockTypes.Fin)
            {
                anchorKey = Facing.Behind(key, modifiers);
                return true;
            }

            anchorKey = -1;
            return false;
        }

        static void AddIfInRange(List<int> cells, int x, int y)
        {
            if (BlockKey.InRange(x, y)) cells.Add(BlockKey.Pack(x, y));
        }
    }
}
