using System;
using NUnit.Framework;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    // Randomized round-trips over the FULL field range (fixed seed, so a
    // failure reproduces), to catch what hand-picked extremes miss.
    public class RandomizedRoundTripTests
    {
        const int Iterations = 5000;

        static Random NewRng(int salt) => new Random(unchecked(1000003 * salt + 7));

        [Test]
        public void InputMessage_RandomizedRoundTrip()
        {
            var rng = NewRng(1);
            var buf = new byte[64];
            for (int i = 0; i < Iterations; i++)
            {
                var msg = new InputMessage(
                    (ushort)rng.Next(0, ushort.MaxValue + 1),
                    (uint)rng.Next(int.MinValue, int.MaxValue),
                    (sbyte)rng.Next(sbyte.MinValue, sbyte.MaxValue + 1),
                    (sbyte)rng.Next(sbyte.MinValue, sbyte.MaxValue + 1),
                    (byte)rng.Next(0, 256));

                var w = new ByteWriter(buf);
                msg.Write(ref w);
                var r = new ByteReader(buf);
                var back = InputMessage.Read(ref r);

                Assert.AreEqual(msg.NetId, back.NetId);
                Assert.AreEqual(msg.Tick, back.Tick);
                Assert.AreEqual(msg.ThrustAxis, back.ThrustAxis);
                Assert.AreEqual(msg.Steer, back.Steer);
                Assert.AreEqual(msg.Flags, back.Flags);
            }
        }

        [Test]
        public void InputMessage_FromFloats_NeverOverflowsSbyteRange()
        {
            var rng = NewRng(2);
            for (int i = 0; i < Iterations; i++)
            {
                float thrust = (float)(rng.NextDouble() * 4.0 - 2.0); // includes out-of-range on both sides
                float steer = (float)(rng.NextDouble() * 4.0 - 2.0);
                var msg = InputMessage.FromFloats(1, 1, thrust, steer, rng.Next(2) == 0);
                Assert.GreaterOrEqual(msg.ThrustAxis, (sbyte)-127);
                Assert.LessOrEqual(msg.ThrustAxis, (sbyte)127);
                Assert.GreaterOrEqual(msg.Steer, (sbyte)-127);
                Assert.LessOrEqual(msg.Steer, (sbyte)127);
            }
        }

        [Test]
        public void ShipState_RandomizedRoundTrip()
        {
            var rng = NewRng(3);
            var buf = new byte[64];
            for (int i = 0; i < Iterations; i++)
            {
                var msg = RandomShipState(rng);
                var w = new ByteWriter(buf);
                msg.Write(ref w);
                Assert.AreEqual(ShipState.ByteSize, w.Position, "Write must consume exactly ByteSize bytes every time");

                var r = new ByteReader(buf);
                var back = ShipState.Read(ref r);
                AssertShipStateEqual(msg, back);
            }
        }

        [Test]
        public void ShipState_BackToBack_InOneSharedBuffer_DoNotOverlap()
        {
            // Writes N ShipStates back-to-back into one buffer, proving
            // Write/Read agree on how many bytes each message takes.
            var rng = NewRng(4);
            const int count = 64;
            var buf = new byte[ShipState.ByteSize * count];
            var messages = new ShipState[count];

            var w = new ByteWriter(buf);
            for (int i = 0; i < count; i++)
            {
                messages[i] = RandomShipState(rng);
                messages[i].Write(ref w);
            }
            Assert.AreEqual(buf.Length, w.Position);

            var r = new ByteReader(buf);
            for (int i = 0; i < count; i++)
            {
                var back = ShipState.Read(ref r);
                AssertShipStateEqual(messages[i], back);
            }
        }

        [Test]
        public void BlockDestroyed_RandomizedRoundTrip()
        {
            var rng = NewRng(5);
            var buf = new byte[32];
            for (int i = 0; i < Iterations; i++)
            {
                var msg = new BlockDestroyed(
                    (uint)rng.Next(int.MinValue, int.MaxValue),
                    (ushort)rng.Next(0, ushort.MaxValue + 1),
                    (sbyte)rng.Next(sbyte.MinValue, sbyte.MaxValue + 1),
                    (sbyte)rng.Next(sbyte.MinValue, sbyte.MaxValue + 1));
                var w = new ByteWriter(buf);
                msg.Write(ref w);
                var r = new ByteReader(buf);
                var back = BlockDestroyed.Read(ref r);
                Assert.AreEqual(msg.Sequence, back.Sequence);
                Assert.AreEqual(msg.NetId, back.NetId);
                Assert.AreEqual(msg.X, back.X);
                Assert.AreEqual(msg.Y, back.Y);
            }
        }

        [Test]
        public void BlockDamaged_RandomizedRoundTrip()
        {
            var rng = NewRng(6);
            var buf = new byte[32];
            for (int i = 0; i < Iterations; i++)
            {
                var msg = new BlockDamaged(
                    (uint)rng.Next(int.MinValue, int.MaxValue),
                    (ushort)rng.Next(0, ushort.MaxValue + 1),
                    (sbyte)rng.Next(sbyte.MinValue, sbyte.MaxValue + 1),
                    (sbyte)rng.Next(sbyte.MinValue, sbyte.MaxValue + 1),
                    (byte)rng.Next(0, 256));
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
        }

        [Test]
        public void BlockPlaced_RandomizedRoundTrip()
        {
            var rng = NewRng(7);
            var buf = new byte[32];
            for (int i = 0; i < Iterations; i++)
            {
                var msg = new BlockPlaced(
                    (uint)rng.Next(int.MinValue, int.MaxValue),
                    (ushort)rng.Next(0, ushort.MaxValue + 1),
                    (sbyte)rng.Next(sbyte.MinValue, sbyte.MaxValue + 1),
                    (sbyte)rng.Next(sbyte.MinValue, sbyte.MaxValue + 1),
                    (byte)rng.Next(0, 256),
                    (byte)rng.Next(0, 256));
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
        }

        [Test]
        public void FragmentSpawned_RandomizedRoundTrip()
        {
            var rng = NewRng(8);
            var buf = new byte[64];
            for (int i = 0; i < Iterations; i++)
            {
                var msg = new FragmentSpawned(
                    (uint)rng.Next(int.MinValue, int.MaxValue),
                    (ushort)rng.Next(0, ushort.MaxValue + 1),
                    (ushort)rng.Next(0, ushort.MaxValue + 1),
                    (short)rng.Next(short.MinValue, short.MaxValue + 1),
                    (short)rng.Next(short.MinValue, short.MaxValue + 1),
                    (ushort)rng.Next(0, ushort.MaxValue + 1),
                    (short)rng.Next(short.MinValue, short.MaxValue + 1),
                    (short)rng.Next(short.MinValue, short.MaxValue + 1),
                    (ushort)rng.Next(0, ushort.MaxValue + 1));
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
        }

        [Test]
        public void PowerupApplied_RandomizedRoundTrip()
        {
            var rng = NewRng(9);
            var buf = new byte[32];
            for (int i = 0; i < Iterations; i++)
            {
                var msg = new PowerupApplied(
                    (uint)rng.Next(int.MinValue, int.MaxValue),
                    (ushort)rng.Next(0, ushort.MaxValue + 1),
                    (sbyte)rng.Next(sbyte.MinValue, sbyte.MaxValue + 1),
                    (sbyte)rng.Next(sbyte.MinValue, sbyte.MaxValue + 1),
                    (byte)rng.Next(0, 256),
                    (ushort)rng.Next(0, ushort.MaxValue + 1));
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
        }

        [Test]
        public void GravityWellSpawned_RandomizedRoundTrip_IncludingSignAndSaturation()
        {
            var rng = NewRng(10);
            var buf = new byte[32];
            for (int i = 0; i < Iterations; i++)
            {
                // Includes values past the wire's representable range,
                // to exercise FromFloats' saturation too.
                float px = (float)(rng.NextDouble() * 2000.0 - 1000.0);
                float py = (float)(rng.NextDouble() * 2000.0 - 1000.0);
                float mu = (float)(rng.NextDouble() * 400000.0 - 200000.0);
                float radius = (float)(rng.NextDouble() * 600.0 - 300.0);
                float seconds = (float)(rng.NextDouble() * 20000.0 - 10000.0);

                var msg = GravityWellSpawned.FromFloats(px, py, mu, radius, seconds);
                var w = new ByteWriter(buf);
                msg.Write(ref w);
                var r = new ByteReader(buf);
                var back = GravityWellSpawned.Read(ref r);

                Assert.AreEqual(msg.Px, back.Px);
                Assert.AreEqual(msg.Py, back.Py);
                Assert.AreEqual(msg.Mu, back.Mu);
                Assert.AreEqual(msg.Radius, back.Radius);
                Assert.AreEqual(msg.Seconds10, back.Seconds10);

                // Radius/seconds are clamped into their unsigned wire ranges;
                // Mu is clamped into the signed i16 range.
                Assert.GreaterOrEqual(back.Radius, (byte)0);
                Assert.LessOrEqual(back.Radius, byte.MaxValue);
                Assert.GreaterOrEqual(back.Seconds10, (ushort)0);
                Assert.GreaterOrEqual(back.Mu, short.MinValue);
                Assert.LessOrEqual(back.Mu, short.MaxValue);
            }
        }

        [Test]
        public void ShipSnapshot_RandomizedRoundTrip_VaryingBlockCounts()
        {
            var rng = NewRng(11);
            foreach (int count in new[] { 0, 1, 2, 7, 64, 500 })
            {
                for (int trial = 0; trial < 20; trial++)
                {
                    var blocks = new SnapshotBlock[count];
                    for (int i = 0; i < count; i++)
                    {
                        blocks[i] = new SnapshotBlock(
                            (sbyte)rng.Next(sbyte.MinValue, sbyte.MaxValue + 1),
                            (sbyte)rng.Next(sbyte.MinValue, sbyte.MaxValue + 1),
                            (byte)rng.Next(0, 256),
                            (byte)rng.Next(0, 256),
                            (byte)rng.Next(0, 256));
                    }

                    var msg = new ShipSnapshot(
                        (uint)rng.Next(int.MinValue, int.MaxValue),
                        (ushort)rng.Next(0, ushort.MaxValue + 1),
                        blocks,
                        (short)rng.Next(short.MinValue, short.MaxValue + 1),
                        (short)rng.Next(short.MinValue, short.MaxValue + 1),
                        (ushort)rng.Next(0, ushort.MaxValue + 1),
                        (short)rng.Next(short.MinValue, short.MaxValue + 1),
                        (short)rng.Next(short.MinValue, short.MaxValue + 1),
                        (ushort)rng.Next(0, ushort.MaxValue + 1));

                    var buf = new byte[msg.ByteSize];
                    var w = new ByteWriter(buf);
                    msg.Write(ref w);
                    Assert.AreEqual(msg.ByteSize, w.Position);

                    var r = new ByteReader(buf);
                    var back = ShipSnapshot.Read(ref r);

                    Assert.AreEqual(msg.Sequence, back.Sequence);
                    Assert.AreEqual(msg.NetId, back.NetId);
                    Assert.AreEqual(count, back.Blocks.Length);
                    for (int i = 0; i < count; i++)
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
            }
        }

        [Test]
        public void Quantization_RandomizedPositionRoundTrip_NeverDriftsOnSecondPack()
        {
            var rng = NewRng(12);
            for (int i = 0; i < Iterations; i++)
            {
                float v = (float)(rng.NextDouble() * 4000.0 - 2000.0); // spans well past PositionLimit on both sides
                short once = Quantization.PackPosition(v);
                short twice = Quantization.PackPosition(Quantization.UnpackPosition(once));
                Assert.AreEqual(once, twice, $"drift at v={v}");
            }
        }

        [Test]
        public void Quantization_RandomizedAngleRoundTrip_WrapsConsistently()
        {
            var rng = NewRng(13);
            double twoPi = 2 * Math.PI;
            for (int i = 0; i < Iterations; i++)
            {
                float a = (float)(rng.NextDouble() * 400.0 - 200.0); // many turns in both directions
                ushort packed = Quantization.PackAngle(a);
                float unpacked = Quantization.UnpackAngle(packed);

                Assert.GreaterOrEqual(unpacked, 0f);
                Assert.Less(unpacked, (float)twoPi);

                // Packing again from the unpacked value must be idempotent.
                Assert.AreEqual(packed, Quantization.PackAngle(unpacked));
            }
        }

        static ShipState RandomShipState(Random rng) => new ShipState(
            (ushort)rng.Next(0, ushort.MaxValue + 1),
            (short)rng.Next(short.MinValue, short.MaxValue + 1),
            (short)rng.Next(short.MinValue, short.MaxValue + 1),
            (ushort)rng.Next(0, ushort.MaxValue + 1),
            (short)rng.Next(short.MinValue, short.MaxValue + 1),
            (short)rng.Next(short.MinValue, short.MaxValue + 1),
            (ushort)rng.Next(0, ushort.MaxValue + 1));

        static void AssertShipStateEqual(ShipState expected, ShipState actual)
        {
            Assert.AreEqual(expected.NetId, actual.NetId);
            Assert.AreEqual(expected.Px, actual.Px);
            Assert.AreEqual(expected.Py, actual.Py);
            Assert.AreEqual(expected.Rot, actual.Rot);
            Assert.AreEqual(expected.Vx, actual.Vx);
            Assert.AreEqual(expected.Vy, actual.Vy);
            Assert.AreEqual(expected.Av, actual.Av);
        }
    }
}
