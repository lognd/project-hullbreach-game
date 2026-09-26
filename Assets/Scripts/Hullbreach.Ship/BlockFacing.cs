using Unity.Mathematics;
using Hullbreach.Core;

namespace Hullbreach.Ship
{
    // Decodes the low 2 bits of Block.Modifiers into a ship-local facing:
    // 0 = +y ("up"), 1 = +x, 2 = -y, 3 = -x, before Rotation is applied.
    // frob:doc docs/reference/hullbreach-ship.md#blockfacing
    public static class BlockFacing
    {
        // frob:doc docs/reference/hullbreach-ship.md#blockfacing
        public const byte Mask = Hullbreach.Core.Facing.Mask;

        // Thin alias for Hullbreach.Core.Facing.Direction, kept here so
        // existing Ship-side callers do not need to change their using directives.
        // frob:doc docs/reference/hullbreach-ship.md#blockfacing
        public static float2 FromModifiers(byte modifiers)
            => Hullbreach.Core.Facing.Direction(modifiers);
    }
}
