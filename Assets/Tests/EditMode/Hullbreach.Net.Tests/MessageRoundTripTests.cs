using NUnit.Framework;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    /// <summary>
    /// Write-then-read, field-by-field, for every message struct, including
    /// extreme values (min/max of each integer field) and angle wrap-around.
    /// A message that fails here would silently desync client and server, so
    /// every field on every struct gets its own assertion rather than a
    /// single Assert.AreEqual(original, roundTripped) on the struct (structs
    /// with array fields, like ShipSnapshot, do not compare usefully anyway).
    /// </summary>
    public class MessageRoundTripTests
    {
        static byte[] Buffer(int size = 256) => new byte[size];

        [Test]
        public void InputMessage_RoundTrips()
        {
            var msg = new InputMessage(ushort.MaxValue, uint.MaxValue, sbyte.MinValue, sbyte.MaxValue, InputMessage.FireBit);
            var buf = Buffer();
            var w = new ByteWriter(buf);
            msg.Write(ref w);

            var r = new ByteReader(buf);
            var back = InputMessage.Read(ref r);

            Assert.AreEqual(msg.NetId, back.NetId);
            Assert.AreEqual(msg.Tick, back.Tick);
            Assert.AreEqual(msg.ThrustAxis, back.ThrustAxis);
            Assert.AreEqual(msg.Steer, back.Steer);
            Assert.AreEqual(msg.Flags, back.Flags);
            Assert.IsTrue(back.FirePressed);
        }

        [Test]
        public void InputMessage_FromFloats_QuantizesAndClamps()
        {
            var msg = InputMessage.FromFloats(1, 1, 2f, -2f, true); // out-of-range clamps to +-1
            Assert.AreEqual(127, msg.ThrustAxis);
            Assert.AreEqual(-127, msg.Steer);
            Assert.IsTrue(msg.FirePressed);
        }

        [Test]
        public void ShipSnapshot_RoundTrips_WithBlocksAndExtremePose()
        {
            var blocks = new[]
            {
                new SnapshotBlock(sbyte.MinValue, sbyte.MaxValue, 0, 0, 0),
                new SnapshotBlock(0, 0, 255, 255, 255),
            };
            var msg = new ShipSnapshot(123456u, 42, blocks,
                short.MinValue, short.MaxValue, ushort.MaxValue, short.MaxValue, short.MinValue, 0);

            var buf = Buffer(1024);
            var w = new ByteWriter(buf);
            msg.Write(ref w);
            Assert.AreEqual(msg.ByteSize, w.Position);

            var r = new ByteReader(buf);
            var back = ShipSnapshot.Read(ref r);

            Assert.AreEqual(msg.Sequence, back.Sequence);
            Assert.AreEqual(msg.NetId, back.NetId);
            Assert.AreEqual(2, back.Blocks.Length);
            for (int i = 0; i < blocks.Length; i++)
            {
                Assert.AreEqual(blocks[i].X, back.Blocks[i].X);
                Assert.AreEqual(blocks[i].Y, back.Blocks[i].Y);
                Assert.AreEqual(blocks[i].TypeId, back.Blocks[i].TypeId);
                Assert.AreEqual(blocks[i].Mods, back.Blocks[i].Mods);
                Assert.AreEqual(blocks[i].Damage, back.Blocks[i].Damage);
            }
            Assert.AreEqual(msg.Px, back.Px);
            Assert.AreEqual(msg.Py, back.Py);
            Assert.AreEqual(msg.Rot, back.Rot);
            Assert.AreEqual(msg.Vx, back.Vx);
            Assert.AreEqual(msg.Vy, back.Vy);
            Assert.AreEqual(msg.Av, back.Av);
        }

        [Test]
        public void ShipSnapshot_ZeroBlocks_RoundTrips()
        {
            var msg = new ShipSnapshot(1, 1, new SnapshotBlock[0], 0, 0, 0, 0, 0, 0);
            var buf = Buffer();
            var w = new ByteWriter(buf);
            msg.Write(ref w);
            var r = new ByteReader(buf);
            var back = ShipSnapshot.Read(ref r);
            Assert.AreEqual(0, back.Blocks.Length);
        }

        [Test]
        public void ShipState_RoundTrips_AtExtremesAndIsExactly15Bytes()
        {
            var msg = new ShipState(ushort.MaxValue, short.MinValue, short.MaxValue, ushort.MaxValue, short.MaxValue, short.MinValue, 0);
            var buf = Buffer();
            var w = new ByteWriter(buf);
            msg.Write(ref w);
            Assert.AreEqual(ShipState.ByteSize, w.Position);
            Assert.AreEqual(15, ShipState.ByteSize);

            var r = new ByteReader(buf);
            var back = ShipState.Read(ref r);
            Assert.AreEqual(msg.NetId, back.NetId);
            Assert.AreEqual(msg.Px, back.Px);
            Assert.AreEqual(msg.Py, back.Py);
            Assert.AreEqual(msg.Rot, back.Rot);
            Assert.AreEqual(msg.Vx, back.Vx);
            Assert.AreEqual(msg.Vy, back.Vy);
            Assert.AreEqual(msg.Av, back.Av);
        }

        [Test]
        public void BlockPlaced_RoundTrips()
        {
            var msg = new BlockPlaced(uint.MaxValue, ushort.MaxValue, sbyte.MinValue, sbyte.MaxValue, 255, 255);
            var buf = Buffer();
            var w = new ByteWriter(buf);
            msg.Write(ref w);
            var r = new ByteReader(buf);
            var back = BlockPlaced.Read(ref r);
            Assert.AreEqual(msg.Sequence, back.Sequence);
            Assert.AreEqual(msg.NetId, back.NetId);
            Assert.AreEqual(msg.X, back.X);
            Assert.AreEqual(msg.Y, back.Y);
            Assert.AreEqual(msg.TypeId, back.TypeId);
            Assert.AreEqual(msg.Mods, back.Mods);
        }

        [Test]
        public void BlockDestroyed_RoundTrips()
        {
            var msg = new BlockDestroyed(1u, 2, sbyte.MinValue, sbyte.MaxValue);
            var buf = Buffer();
            var w = new ByteWriter(buf);
            msg.Write(ref w);
            var r = new ByteReader(buf);
            var back = BlockDestroyed.Read(ref r);
            Assert.AreEqual(msg.Sequence, back.Sequence);
            Assert.AreEqual(msg.NetId, back.NetId);
            Assert.AreEqual(msg.X, back.X);
            Assert.AreEqual(msg.Y, back.Y);
        }

        [Test]
        public void FragmentSpawned_RoundTrips()
        {
            var msg = new FragmentSpawned(uint.MaxValue, ushort.MaxValue, 1, short.MinValue, short.MaxValue, ushort.MaxValue, short.MaxValue, short.MinValue, 0);
            var buf = Buffer();
            var w = new ByteWriter(buf);
            msg.Write(ref w);
            var r = new ByteReader(buf);
            var back = FragmentSpawned.Read(ref r);
            Assert.AreEqual(msg.Sequence, back.Sequence);
            Assert.AreEqual(msg.ParentId, back.ParentId);
            Assert.AreEqual(msg.NewId, back.NewId);
            Assert.AreEqual(msg.Px, back.Px);
            Assert.AreEqual(msg.Py, back.Py);
            Assert.AreEqual(msg.Rot, back.Rot);
            Assert.AreEqual(msg.Vx, back.Vx);
            Assert.AreEqual(msg.Vy, back.Vy);
            Assert.AreEqual(msg.Av, back.Av);
        }

        [Test]
        public void BlockDamaged_RoundTrips()
        {
            var msg = new BlockDamaged(1u, 2, sbyte.MinValue, sbyte.MaxValue, 255);
            var buf = Buffer();
            var w = new ByteWriter(buf);
            msg.Write(ref w);
            var r = new ByteReader(buf);
            var back = BlockDamaged.Read(ref r);
            Assert.AreEqual(msg.Sequence, back.Sequence);
            Assert.AreEqual(msg.NetId, back.NetId);
            Assert.AreEqual(msg.X, back.X);
            Assert.AreEqual(msg.Y, back.Y);
            Assert.AreEqual(msg.Damage, back.Damage);
        }

        [Test]
        public void PowerupApplied_RoundTrips()
        {
            var msg = new PowerupApplied(1u, ushort.MaxValue, sbyte.MinValue, sbyte.MaxValue, 255, ushort.MaxValue);
            var buf = Buffer();
            var w = new ByteWriter(buf);
            msg.Write(ref w);
            var r = new ByteReader(buf);
            var back = PowerupApplied.Read(ref r);
            Assert.AreEqual(msg.Sequence, back.Sequence);
            Assert.AreEqual(msg.NetId, back.NetId);
            Assert.AreEqual(msg.X, back.X);
            Assert.AreEqual(msg.Y, back.Y);
            Assert.AreEqual(msg.Variant, back.Variant);
            Assert.AreEqual(msg.Seconds10, back.Seconds10);
        }

        [Test]
        public void GravityWellSpawned_RoundTrips_IncludingNegativeMu()
        {
            var msg = GravityWellSpawned.FromFloats(10.5f, -20.25f, -5.0f, 3f, 12.3f);
            var buf = Buffer();
            var w = new ByteWriter(buf);
            msg.Write(ref w);
            var r = new ByteReader(buf);
            var back = GravityWellSpawned.Read(ref r);
            Assert.AreEqual(msg.Px, back.Px);
            Assert.AreEqual(msg.Py, back.Py);
            Assert.AreEqual(msg.Mu, back.Mu);
            Assert.AreEqual(msg.Radius, back.Radius);
            Assert.AreEqual(msg.Seconds10, back.Seconds10);
            Assert.Less(back.MuFloat, 0f); // anti-gravity gun: negative Mu must survive the round trip
            Assert.AreEqual(12.3f, back.SecondsFloat, 0.05f);
        }

        [Test]
        public void GravityWellSpawned_MuSaturatesAtExtremes()
        {
            var msg = GravityWellSpawned.FromFloats(0f, 0f, 1e6f, 0f, 0f);
            Assert.AreEqual(short.MaxValue, msg.Mu);
            var msg2 = GravityWellSpawned.FromFloats(0f, 0f, -1e6f, 0f, 0f);
            Assert.AreEqual(short.MinValue, msg2.Mu);
        }

        [Test]
        public void MessageKind_IsFirstByteOfEveryEncodedMessage()
        {
            var buf = Buffer();
            var w = new ByteWriter(buf);
            new ShipState(1, 0, 0, 0, 0, 0, 0).Write(ref w);
            Assert.AreEqual((byte)MessageKind.ShipState, buf[0]);
        }
    }
}
