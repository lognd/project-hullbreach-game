using Unity.Mathematics;
using Hullbreach.Core;

namespace Hullbreach.Ship
{
    /// <summary>
    /// Decodes the low 2 bits of Block.Modifiers into a ship-local facing for
    /// thrusters and cannons. Kept in one place so every system that cares
    /// which way a directional block points (thrust application, muzzle
    /// direction, gizmo drawing) agrees on the same encoding.
    ///
    /// Encoding: 0 = +y ("up"), 1 = +x, 2 = -y, 3 = -x, all in ship-local space
    /// before Rotation is applied.
    /// </summary>
    public static class BlockFacing
    {
        /// <summary>Bit mask isolating the facing field within Modifiers.</summary>
        public const byte Mask = Hullbreach.Core.Facing.Mask;

        /// <summary>
        /// Thin alias for Hullbreach.Core.Facing.Direction, kept here so
        /// existing Ship-side callers (thrust application, muzzle direction,
        /// gizmo drawing) do not need to change their using directives.
        /// </summary>
        public static float2 FromModifiers(byte modifiers)
            => Hullbreach.Core.Facing.Direction(modifiers);
    }
}
