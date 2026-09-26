using System.Collections.Generic;
using Hullbreach.Core;

namespace Hullbreach.Builder
{
    // Per-type "keep this cell empty" / "must be attached here" geometry;
    // see docs/reference/hullbreach-builder.md#clearance.
    // frob:doc docs/reference/hullbreach-builder.md#clearance
    public static class Clearance
    {
        // Lists cells that must stay empty for a block at `key`; see
        // docs/reference/hullbreach-builder.md#clearance for per-type rules.
        // frob:doc docs/reference/hullbreach-builder.md#clearance
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

        // True when `typeId` needs an anchor block (only Fin); see
        // docs/reference/hullbreach-builder.md#clearance.
        // frob:doc docs/reference/hullbreach-builder.md#clearance
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
