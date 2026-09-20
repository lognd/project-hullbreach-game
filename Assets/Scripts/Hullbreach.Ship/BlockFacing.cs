using Unity.Mathematics;

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
        public const byte Mask = 0b11;

        /// <summary>Unit ship-local direction for the given modifiers byte.
        /// Only the low 2 bits are consulted; higher bits are reserved for
        /// other upgrades and ignored here.</summary>
        public static float2 FromModifiers(byte modifiers)
        {
            switch (modifiers & Mask)
            {
                case 0: return new float2(0f, 1f);
                case 1: return new float2(1f, 0f);
                case 2: return new float2(0f, -1f);
                default: return new float2(-1f, 0f);
            }
        }
    }
}
