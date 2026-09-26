using Unity.Mathematics;

namespace Hullbreach.Core
{
    // Decodes the low 2 bits of Block.Modifiers into a ship-local facing;
    // see docs/reference/hullbreach-core.md#facing for the encoding.
    // frob:doc docs/reference/hullbreach-core.md#facing
    public static class Facing
    {
        // frob:doc docs/reference/hullbreach-core.md#facing
        public const byte Mask = 0b11;

        // Only the low 2 bits are consulted; higher bits are ignored.
        // frob:doc docs/reference/hullbreach-core.md#facing
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

        // Same encoding as Step, as a float2.
        // frob:doc docs/reference/hullbreach-core.md#facing
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

        // The reverse facing, ((f + 2) & 3), preserving other upgrade bits.
        // frob:doc docs/reference/hullbreach-core.md#facing
        public static byte Opposite(byte modifiers)
        {
            byte high = (byte)(modifiers & ~Mask);
            byte facing = (byte)((modifiers & Mask) + 2 & Mask);
            return (byte)(high | facing);
        }

        // Rotated +90 degrees CCW from Direction, e.g. facing +y gives -x.
        // frob:doc docs/reference/hullbreach-core.md#facing
        public static float2 Perpendicular(byte modifiers)
        {
            float2 d = Direction(modifiers);
            return new float2(-d.y, d.x);
        }

        // -1 if that cell falls outside BlockKey's range.
        // frob:doc docs/reference/hullbreach-core.md#facing
        public static int Ahead(int key, byte modifiers)
        {
            BlockKey.Unpack(key, out int x, out int y);
            Step(modifiers, out int dx, out int dy);
            int nx = x + dx;
            int ny = y + dy;
            return BlockKey.InRange(nx, ny) ? BlockKey.Pack(nx, ny) : -1;
        }

        // Opposite the facing direction, or -1 if out of range.
        // frob:doc docs/reference/hullbreach-core.md#facing
        public static int Behind(int key, byte modifiers)
            => Ahead(key, Opposite(modifiers));
    }
}
