using System;

namespace Hullbreach.Net
{
    // frob:doc docs/reference/hullbreach-net.md#quantization
    public static class Quantization
    {
        // frob:doc docs/reference/hullbreach-net.md#quantization
        public const float PositionScale = 256f;

        // The arena (S41) must fit inside +-PositionLimit or packing
        // saturates and a ship near the edge stops moving on the wire.
        // frob:doc docs/reference/hullbreach-net.md#quantization
        public const float PositionLimit = short.MaxValue / PositionScale;

        // Rounds (not truncates) so the round trip is idempotent: see
        // docs/reference/hullbreach-net.md#quantization.
        // frob:doc docs/reference/hullbreach-net.md#quantization
        public static short PackPosition(float v)
        {
            float scaled = (float)Math.Round(v * PositionScale, MidpointRounding.AwayFromZero);
            if (scaled > short.MaxValue) return short.MaxValue;
            if (scaled < short.MinValue) return short.MinValue;
            return (short)scaled;
        }

        // frob:doc docs/reference/hullbreach-net.md#quantization
        public static float UnpackPosition(short v) => v / PositionScale;

        const double Tau = 2.0 * Math.PI;

        // Wraps via the ushort cast, i.e. modular arithmetic on the circle,
        // so any radian value packs without normalization.
        // frob:doc docs/reference/hullbreach-net.md#quantization
        public static ushort PackAngle(float radians)
        {
            double turns = radians / Tau;
            turns -= Math.Floor(turns);   // 0 <= turns < 1
            return (ushort)((uint)Math.Round(turns * 65536.0) & 0xFFFF);
        }

        // frob:doc docs/reference/hullbreach-net.md#quantization
        public static float UnpackAngle(ushort packed) => (float)(packed / 65536.0 * Tau);
    }
}
