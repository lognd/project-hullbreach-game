using Unity.Mathematics;

namespace Hullbreach.Core
{
    /// <summary>
    /// Decodes the low 2 bits of Block.Modifiers into a ship-local facing.
    /// Lives in Core (rather than Ship) so builder placement rules (which
    /// need to know what is "ahead of" or "behind" a directional block)
    /// can use the same encoding without depending on Hullbreach.Ship.
    ///
    /// Encoding: 0 = +y ("up"), 1 = +x, 2 = -y, 3 = -x, all in ship-local
    /// space before Rotation is applied.
    /// </summary>
    public static class Facing
    {
        /// <summary>Bit mask isolating the facing field within Modifiers.</summary>
        public const byte Mask = 0b11;

        /// <summary>Integer grid step for the given modifiers byte. Only the
        /// low 2 bits are consulted; higher bits are reserved for other
        /// upgrades and ignored here.</summary>
        public static void Step(byte modifiers, out int dx, out int dy)
        {
            switch (modifiers & Mask)
            {
                case 0: dx = 0; dy = 1; break;
                case 1: dx = 1; dy = 0; break;
                case 2: dx = 0; dy = -1; break;
                default: dx = -1; dy = 0; break;
            }
        }

        /// <summary>Unit ship-local direction for the given modifiers byte.
        /// Same encoding as Step, as a float2.</summary>
        public static float2 Direction(byte modifiers)
        {
            switch (modifiers & Mask)
            {
                case 0: return new float2(0f, 1f);
                case 1: return new float2(1f, 0f);
                case 2: return new float2(0f, -1f);
                default: return new float2(-1f, 0f);
            }
        }

        /// <summary>The reverse facing, ((f + 2) &amp; 3), preserving any
        /// higher bits used by other upgrades.</summary>
        public static byte Opposite(byte modifiers)
        {
            byte high = (byte)(modifiers & ~Mask);
            byte facing = (byte)((modifiers & Mask) + 2 & Mask);
            return (byte)(high | facing);
        }

        /// <summary>Direction rotated +90 degrees CCW from Direction, e.g.
        /// facing +y gives -x. Used to lay blocks out perpendicular to a
        /// directional block's facing.</summary>
        public static float2 Perpendicular(byte modifiers)
        {
            float2 d = Direction(modifiers);
            return new float2(-d.y, d.x);
        }

        /// <summary>Packed key of the cell one grid step ahead of `key` in
        /// the facing direction, or -1 if that cell falls outside
        /// BlockKey's range. Caller is expected to have already checked
        /// BlockKey.InRange on `key` itself.</summary>
        public static int Ahead(int key, byte modifiers)
        {
            BlockKey.Unpack(key, out int x, out int y);
            Step(modifiers, out int dx, out int dy);
            int nx = x + dx;
            int ny = y + dy;
            return BlockKey.InRange(nx, ny) ? BlockKey.Pack(nx, ny) : -1;
        }

        /// <summary>Packed key of the cell one grid step behind `key`, i.e.
        /// opposite the facing direction, or -1 if out of range.</summary>
        public static int Behind(int key, byte modifiers)
            => Ahead(key, Opposite(modifiers));
    }
}
