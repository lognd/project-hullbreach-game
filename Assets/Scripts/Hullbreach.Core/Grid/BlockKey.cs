using System;

namespace Hullbreach.Core
{
    // Packs a signed 2D grid coordinate into a single int (see
    // docs/reference/hullbreach-core.md#blockkey for the grid convention).
    // frob:doc docs/reference/hullbreach-core.md#blockkey
    public static class BlockKey
    {
        // frob:doc docs/reference/hullbreach-core.md#blockkey
        public const int Min = -128;

        // frob:doc docs/reference/hullbreach-core.md#blockkey
        public const int Max = 127;

        // Bias each axis by -Min into 0..255 to avoid sign-extension issues.
        // frob:doc docs/reference/hullbreach-core.md#blockkey
        public static int Pack(int x, int y)
        {
            int bx = x - Min;
            int by = y - Min;
            return (bx << 8) | by;
        }

        // frob:doc docs/reference/hullbreach-core.md#blockkey
        public static void Unpack(int key, out int x, out int y)
        {
            int bx = (key >> 8) & 0xFF;
            int by = key & 0xFF;
            x = bx + Min;
            y = by + Min;
        }

        // The four 4-connected neighbors of a key, in +x,-x,+y,-y order.
        // frob:doc docs/reference/hullbreach-core.md#blockkey
        public static void Neighbors(int key, int[] into)
        {
            Unpack(key, out int x, out int y);
            into[0] = Pack(x + 1, y);
            into[1] = Pack(x - 1, y);
            into[2] = Pack(x, y + 1);
            into[3] = Pack(x, y - 1);
        }

        // frob:doc docs/reference/hullbreach-core.md#blockkey
        public static bool InRange(int x, int y)
            => x >= Min && x <= Max && y >= Min && y <= Max;
    }
}
