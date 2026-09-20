using System;

namespace Hullbreach.Net
{
    /// <summary>Fixed-point packing for the per-tick state.</summary>
    public static class Quantization
    {
        /// <summary>Position resolution: 1/256 of a world unit.</summary>
        public const float PositionScale = 256f;

        /// <summary>Largest position a short can carry at this resolution.
        /// The arena (S41) must fit inside +-PositionLimit or the packing
        /// saturates and a ship near the edge stops moving on the wire.</summary>
        public const float PositionLimit = short.MaxValue / PositionScale;

        /// <summary>
        /// Round to nearest and saturate. Rounding (not truncating) makes the
        /// round trip idempotent: Unpack(Pack(x)) lands exactly on a lattice
        /// point, and packing that again returns the same short, so repeated
        /// quantization never crawls.
        /// </summary>
        public static short PackPosition(float v)
        {
            float scaled = (float)Math.Round(v * PositionScale, MidpointRounding.AwayFromZero);
            if (scaled > short.MaxValue) return short.MaxValue;
            if (scaled < short.MinValue) return short.MinValue;
            return (short)scaled;
        }

        public static float UnpackPosition(short v) => v / PositionScale;

        const double Tau = 2.0 * Math.PI;

        /// <summary>
        /// Angle as a fraction of a full turn in 16 bits. The cast to ushort
        /// wraps, which is exactly modular arithmetic on the circle, so any
        /// radian value (negative, many turns) packs without normalization.
        /// </summary>
        public static ushort PackAngle(float radians)
        {
            double turns = radians / Tau;
            turns -= Math.Floor(turns);   // 0 <= turns < 1
            return (ushort)((uint)Math.Round(turns * 65536.0) & 0xFFFF);
        }

        /// <summary>Radians in [0, 2 pi).</summary>
        public static float UnpackAngle(ushort packed) => (float)(packed / 65536.0 * Tau);
    }
}
