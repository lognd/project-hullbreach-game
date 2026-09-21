using System;

namespace Hullbreach.Core
{
    /// <summary>
    /// Packs a signed 2D grid coordinate into a single int.
    ///
    /// Why a packed int and not (int,int): ValueTuple hashing goes through
    /// per-field EqualityComparer&lt;T&gt;.Default and is measurably slower. The
    /// packed int is also exactly what goes on the wire, so the network
    /// identity and the dictionary key end up being the same thing.
    ///
    /// GRID CONVENTION: everything downstream depends on this:
    ///   Block (x, y) occupies the unit square [x, x+1] x [y, y+1].
    ///   Its center is therefore at (x + 0.5, y + 0.5).
    /// </summary>
    public static class BlockKey
    {
        /// <summary>Inclusive lower bound on either axis.</summary>
        public const int Min = -128;

        /// <summary>Inclusive upper bound on either axis.</summary>
        public const int Max = 127;

        /// <summary>
        /// Bias each axis by -Min (128) into 0..255, then pack y into the low
        /// byte and x into the next byte up. Biasing avoids sign-extension
        /// issues when reconstructing negative coordinates from raw bits.
        /// </summary>
        public static int Pack(int x, int y)
        {
            int bx = x - Min;
            int by = y - Min;
            return (bx << 8) | by;
        }

        /// <summary>Exact inverse of Pack: undo the shift, then undo the bias.</summary>
        public static void Unpack(int key, out int x, out int y)
        {
            int bx = (key >> 8) & 0xFF;
            int by = key & 0xFF;
            x = bx + Min;
            y = by + Min;
        }

        /// <summary>
        /// The four 4-connected neighbors of a key, written into `into`
        /// (length 4), in +x,-x,+y,-y order. Connectivity and placement
        /// validity both need this, so it lives in one place.
        /// </summary>
        public static void Neighbors(int key, int[] into)
        {
            Unpack(key, out int x, out int y);
            into[0] = Pack(x + 1, y);
            into[1] = Pack(x - 1, y);
            into[2] = Pack(x, y + 1);
            into[3] = Pack(x, y - 1);
        }

        /// <summary>True when the coordinate fits in the packed range.</summary>
        public static bool InRange(int x, int y)
            => x >= Min && x <= Max && y >= Min && y <= Max;
    }
}
