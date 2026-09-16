using System;

namespace Hullbreach.Net
{
    /// <summary>Fixed-point packing for the per-tick state.</summary>
    public static class Quantization
    {
        /// <summary>Position resolution: 1/256 of a world unit.</summary>
        public const float PositionScale = 256f;

        // TODO: Round-trip must be stable -- Unpack(Pack(x)) should not drift
        //       when applied repeatedly, or interpolated positions will crawl.
        public static short PackPosition(float v) => throw new NotImplementedException();
        public static float UnpackPosition(short v) => throw new NotImplementedException();

        // TODO: Angle as a 16-bit turn, so it wraps for free at the type
        //       boundary instead of needing explicit modular arithmetic.
        public static ushort PackAngle(float radians) => throw new NotImplementedException();
        public static float UnpackAngle(ushort packed) => throw new NotImplementedException();
    }
}
