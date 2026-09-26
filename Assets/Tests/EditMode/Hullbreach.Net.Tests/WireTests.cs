using NUnit.Framework;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    // Round-trips every ByteWriter/ByteReader primitive at extreme values.
    public class WireTests
    {
        [Test]
        public void RoundTrips_EveryPrimitive_AtExtremes()
        {
            var buffer = new byte[64];
            var w = new ByteWriter(buffer);
            w.WriteU8(0xFF);
            w.WriteI8(sbyte.MinValue);
            w.WriteU16(ushort.MaxValue);
            w.WriteI16(short.MinValue);
            w.WriteU32(uint.MaxValue);
            w.WriteI32(int.MinValue);
            w.WriteF32(float.NaN);
            w.WriteF32(-1.5f);
            int written = w.Position;

            var r = new ByteReader(buffer);
            Assert.AreEqual(0xFF, r.ReadU8());
            Assert.AreEqual(sbyte.MinValue, r.ReadI8());
            Assert.AreEqual(ushort.MaxValue, r.ReadU16());
            Assert.AreEqual(short.MinValue, r.ReadI16());
            Assert.AreEqual(uint.MaxValue, r.ReadU32());
            Assert.AreEqual(int.MinValue, r.ReadI32());
            Assert.IsTrue(float.IsNaN(r.ReadF32()));
            Assert.AreEqual(-1.5f, r.ReadF32());
            Assert.AreEqual(written, r.Position);
        }

        [Test]
        public void U16_IsLittleEndian()
        {
            var buffer = new byte[2];
            var w = new ByteWriter(buffer);
            w.WriteU16(0x1234);
            Assert.AreEqual(0x34, buffer[0]);
            Assert.AreEqual(0x12, buffer[1]);
        }
    }
}
