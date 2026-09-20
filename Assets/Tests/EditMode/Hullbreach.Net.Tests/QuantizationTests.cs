using System;
using NUnit.Framework;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    public class QuantizationTests
    {
        [Test]
        public void Position_RoundTripIsIdempotent()
        {
            // Repeated quantization must land on the same lattice point, or
            // interpolated positions crawl every time they cross the wire.
            foreach (var v in new[] { 0f, 1.2345f, -17.001f, 100.5f, 0.0019f })
            {
                short once = Quantization.PackPosition(v);
                short twice = Quantization.PackPosition(Quantization.UnpackPosition(once));
                Assert.AreEqual(once, twice, $"drift at {v}");
                Assert.AreEqual(v, Quantization.UnpackPosition(once), 0.5f / Quantization.PositionScale + 1e-6f);
            }
        }

        [Test]
        public void Position_SaturatesInsteadOfWrapping()
        {
            Assert.AreEqual(short.MaxValue, Quantization.PackPosition(1e6f));
            Assert.AreEqual(short.MinValue, Quantization.PackPosition(-1e6f));
        }

        [Test]
        public void Angle_WrapsWholeTurnsAndNegatives()
        {
            float twoPi = (float)(2 * Math.PI);
            Assert.AreEqual(Quantization.PackAngle(1f), Quantization.PackAngle(1f + twoPi));
            Assert.AreEqual(Quantization.PackAngle(1f), Quantization.PackAngle(1f - 3 * twoPi));
            Assert.AreEqual(0, Quantization.PackAngle(0f));
            Assert.AreEqual(32768, Quantization.PackAngle((float)Math.PI));
        }

        [Test]
        public void Angle_RoundTripWithinOneStep()
        {
            float step = (float)(2 * Math.PI / 65536.0);
            foreach (var a in new[] { 0.1f, 2.5f, 6.0f, -1f })
            {
                float back = Quantization.UnpackAngle(Quantization.PackAngle(a));
                float expected = a - (float)(Math.Floor(a / (2 * Math.PI)) * 2 * Math.PI);
                Assert.AreEqual(expected, back, step);
            }
        }
    }
}
