using System;
using NUnit.Framework;
using Hullbreach.Core;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    // INV-001: remote bytes never size an allocation or index past the received length.
    public class WireBoundsTests
    {
        [Test]
        public void ByteReader_PastTheEnd_FailsAndYieldsZero_WithoutThrowing()
        {
            var r = new ByteReader(new byte[] { 1, 2, 3 });
            Assert.AreEqual(0u, r.ReadU32());
            Assert.IsTrue(r.Failed);
            Assert.AreEqual(0, r.Remaining);
            Assert.AreEqual(0, r.ReadU8(), "a failed reader stays failed");
        }

        [Test]
        public void ByteReader_HonorsTheReceivedLength_NotTheBufferLength()
        {
            var buffer = new byte[] { 9, 9, 9, 9, 9, 9 };
            var r = new ByteReader(buffer, 0, 2);
            Assert.AreEqual(2, r.Remaining);
            r.ReadU16();
            Assert.IsFalse(r.Failed);
            r.ReadU8();
            Assert.IsTrue(r.Failed, "stale bytes beyond length must not be readable");
        }

        [Test]
        public void ShipSnapshotRead_HugeCount_OnTinyBuffer_FailsWithoutAllocating()
        {
            var buf = new byte[32];
            var w = new ByteWriter(buf);
            w.WriteU8((byte)MessageKind.ShipSnapshot);
            w.WriteU32(1);
            w.WriteU16(1);
            w.WriteU16(ushort.MaxValue); // claims 65535 blocks
            var r = new ByteReader(buf, 0, w.Position);
            var snapshot = ShipSnapshot.Read(ref r);
            Assert.IsTrue(r.Failed);
            Assert.AreEqual(0, snapshot.Blocks.Length);
        }

        [Test]
        public void ShipSnapshotRead_CountAboveCap_FailsEvenWhenBytesArePresent()
        {
            int count = ShipSnapshot.MaxBlocks + 1;
            var buf = new byte[9 + count * 5 + 12];
            var w = new ByteWriter(buf);
            w.WriteU8((byte)MessageKind.ShipSnapshot);
            w.WriteU32(1);
            w.WriteU16(1);
            w.WriteU16((ushort)count);
            var r = new ByteReader(buf);
            ShipSnapshot.Read(ref r);
            Assert.IsTrue(r.Failed);
        }

        [Test]
        public void ShipSnapshotWrite_OverTheCap_Throws_InsteadOfWrappingTheCount()
        {
            var blocks = new SnapshotBlock[ShipSnapshot.MaxBlocks + 1];
            var snapshot = new ShipSnapshot(1, 1, blocks, 0, 0, 0, 0, 0, 0);
            var buf = new byte[snapshot.ByteSize];
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var w = new ByteWriter(buf);
                snapshot.Write(ref w);
            });
        }

        [Test]
        public void ApplyReceived_TruncatedAndUnknownPayloads_AreMalformed_AndNeverThrow()
        {
            var replica = new ClientReplica();
            Assert.AreEqual(ReplicaApplyResult.Malformed, replica.ApplyReceived(new byte[8], 0));
            Assert.AreEqual(ReplicaApplyResult.Malformed, replica.ApplyReceived(new byte[8], 99));
            Assert.AreEqual(ReplicaApplyResult.Malformed, replica.ApplyReceived(new byte[] { 200, 1, 2, 3, 4, 5 }, 6));
            Assert.AreEqual(ReplicaApplyResult.Malformed, replica.ApplyReceived(new byte[] { (byte)MessageKind.Input, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, 10));

            foreach (MessageKind kind in Enum.GetValues(typeof(MessageKind)))
            {
                for (int length = 1; length < 4; length++)
                {
                    var buf = new byte[64];
                    buf[0] = (byte)kind;
                    Assert.DoesNotThrow(() => replica.ApplyReceived(buf, length), $"{kind} truncated to {length}");
                }
            }
        }

        [Test]
        public void ApplyReceived_IgnoresBytesBeyondLength()
        {
            var stale = new byte[64];
            var w = new ByteWriter(stale);
            new ShipState(1, 5, 5, 0, 0, 0, 0).Write(ref w);
            var replica = new ClientReplica();
            // Only the kind byte "arrived"; the rest is stale buffer content.
            Assert.AreEqual(ReplicaApplyResult.Malformed, replica.ApplyReceived(stale, 1));
        }
    }
}
