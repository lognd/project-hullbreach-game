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
    /// GRID CONVENTION -- everything downstream depends on this:
    ///   Block (x, y) occupies the unit square [x, x+1] x [y, y+1].
    ///   Its center is therefore at (x + 0.5, y + 0.5).
    /// </summary>
    public static class BlockKey
    {
        /// <summary>Inclusive lower bound on either axis.</summary>
        public const int Min = -128;

        /// <summary>Inclusive upper bound on either axis.</summary>
        public const int Max = 127;

        // TODO [A1]: Pack x and y into one int so Unpack recovers them exactly
        //            for every coordinate in [Min, Max]. Bias each axis by +128
        //            into 0..255, then shift one of them left by 8.
        public static int Pack(int x, int y)
            => throw new NotImplementedException();

        // TODO [A1]: Exact inverse of Pack.
        public static void Unpack(int key, out int x, out int y)
            => throw new NotImplementedException();

        // TODO [A1]: The four 4-connected neighbors of a key, written into
        //            `into` (length 4). Connectivity and placement validity
        //            both need this, so it lives in one place.
        public static void Neighbors(int key, int[] into)
            => throw new NotImplementedException();

        /// <summary>True when the coordinate fits in the packed range.</summary>
        public static bool InRange(int x, int y)
            => x >= Min && x <= Max && y >= Min && y <= Max;
    }
}
