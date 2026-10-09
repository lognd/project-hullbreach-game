using NUnit.Framework;
using Hullbreach.Core;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    // INV-002 and the ordered-stream rules: bounded reorder window, malformed
    // payloads never stall delivery, snapshots are per-ship.
    public class ClientReplicaRobustnessTests
    {
        static SnapshotBlock[] Line()
            => new[]
            {
                new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0),
                new SnapshotBlock(0, 1, BlockTypes.Hull, 0, 0),
                new SnapshotBlock(0, 2, BlockTypes.Hull, 0, 0),
            };

        static byte[] Bytes(ShipSnapshot m) { var b = new byte[m.ByteSize]; var w = new ByteWriter(b); m.Write(ref w); return b; }
        static byte[] Bytes(BlockDestroyed m) { var b = new byte[16]; var w = new ByteWriter(b); m.Write(ref w); return Trim(b, w.Position); }
        static byte[] Bytes(ShipRemoved m) { var b = new byte[16]; var w = new ByteWriter(b); m.Write(ref w); return Trim(b, w.Position); }
        static byte[] Trim(byte[] b, int n) { var t = new byte[n]; System.Array.Copy(b, t, n); return t; }

        static ReplicaApplyResult Feed(ClientReplica r, byte[] bytes) => r.ApplyReceived(bytes, bytes.Length);

        static ShipSnapshot Snap(uint seq, ushort netId) => new ShipSnapshot(seq, netId, Line(), 0, 0, 0, 0, 0, 0);

        [Test]
        public void FarFutureSequence_IsDropped_AndDoesNotBlockLaterInOrderEvents()
        {
            var replica = new ClientReplica();
            Feed(replica, Bytes(Snap(1, 1)));

            var far = Bytes(new BlockDestroyed(1u + (uint)ClientReplica.MaxReliableWindow + 5u, 1, 0, 2));
            Assert.AreEqual(ReplicaApplyResult.OutOfWindow, Feed(replica, far));
            Assert.AreEqual(ReplicaApplyResult.OutOfWindow, Feed(replica, Bytes(new BlockDestroyed(uint.MaxValue / 2, 1, 0, 2))));

            Assert.AreEqual(ReplicaApplyResult.Applied, Feed(replica, Bytes(new BlockDestroyed(2, 1, 0, 2))));
            Assert.IsFalse(replica.Ships[1].Grid.Contains(BlockKey.Pack(0, 2)));
        }

        [Test]
        public void ManySkippedSequences_NeverGrowTheBufferPastTheWindow()
        {
            var replica = new ClientReplica();
            Feed(replica, Bytes(Snap(1, 1)));
            int buffered = 0;
            for (uint seq = 3; seq < 3 + 5000; seq++)
                if (Feed(replica, Bytes(new BlockDestroyed(seq, 1, 0, 2))) == ReplicaApplyResult.Buffered) buffered++;
            Assert.LessOrEqual(buffered, ClientReplica.MaxReliableWindow);
        }

        [Test]
        public void MalformedReliableBody_ConsumesItsSlot_AndDoesNotStallTheStream()
        {
            var replica = new ClientReplica();
            Feed(replica, Bytes(Snap(1, 1)));

            // seq 2: valid header, body cut short.
            var truncated = Bytes(new BlockDestroyed(2, 1, 0, 2));
            Assert.AreEqual(ReplicaApplyResult.Applied, replica.ApplyReceived(truncated, 6));

            Assert.AreEqual(ReplicaApplyResult.Applied, Feed(replica, Bytes(new BlockDestroyed(3, 1, 0, 2))));
            Assert.IsFalse(replica.Ships[1].Grid.Contains(BlockKey.Pack(0, 2)), "later events must still apply");
        }

        [Test]
        public void OlderSnapshot_DoesNotRewindANewerOne()
        {
            var replica = new ClientReplica();
            Assert.AreEqual(ReplicaApplyResult.Applied, Feed(replica, Bytes(Snap(5, 1))));
            Feed(replica, Bytes(new BlockDestroyed(6, 1, 0, 2)));
            Assert.IsFalse(replica.Ships[1].Grid.Contains(BlockKey.Pack(0, 2)));

            Assert.AreEqual(ReplicaApplyResult.Stale, Feed(replica, Bytes(Snap(3, 1))));
            Assert.IsFalse(replica.Ships[1].Grid.Contains(BlockKey.Pack(0, 2)), "stale snapshot must not resurrect the block");
        }

        [Test]
        public void SnapshotOfAnotherShip_DoesNotDiscardPendingEventsForThisShip()
        {
            var replica = new ClientReplica();
            Feed(replica, Bytes(Snap(1, 1)));
            // seq 2 is missing; 3 is buffered behind it.
            Assert.AreEqual(ReplicaApplyResult.Buffered, Feed(replica, Bytes(new BlockDestroyed(3, 1, 0, 2))));
            // A later join snapshot (seq 4, ship 2) must not skip the baseline over 2..3.
            Feed(replica, Bytes(Snap(4, 2)));
            Assert.IsTrue(replica.Ships[1].Grid.Contains(BlockKey.Pack(0, 2)), "event 3 is still waiting for 2");

            Feed(replica, Bytes(new BlockDestroyed(2, 1, 0, 1)));
            Assert.IsFalse(replica.Ships[1].Grid.Contains(BlockKey.Pack(0, 1)));
            Assert.IsFalse(replica.Ships[1].Grid.Contains(BlockKey.Pack(0, 2)));
            Assert.IsTrue(replica.Ships.ContainsKey(2));
        }

        [Test]
        public void EventForAShipWhoseSnapshotIsStillInFlight_IsReplayedWhenItArrives()
        {
            var replica = new ClientReplica();
            Feed(replica, Bytes(Snap(10, 1)));                              // own ship establishes the baseline
            Feed(replica, Bytes(new BlockDestroyed(11, 2, 0, 2)));          // ship 2 is not known yet
            Assert.IsFalse(replica.Ships.ContainsKey(2));

            Feed(replica, Bytes(Snap(9, 2)));                               // its snapshot, stamped before the event
            Assert.IsFalse(replica.Ships[2].Grid.Contains(BlockKey.Pack(0, 2)));
        }

        [Test]
        public void ShipRemoved_DropsTheShip_RaisesAnEvent_AndBlocksStaleResurrection()
        {
            var replica = new ClientReplica();
            Feed(replica, Bytes(Snap(1, 1)));
            Feed(replica, Bytes(Snap(2, 2)));
            Assert.AreEqual(ReplicaApplyResult.Applied, Feed(replica, Bytes(new ShipRemoved(3, 2))));

            Assert.IsFalse(replica.Ships.ContainsKey(2));
            Assert.IsTrue(replica.Ships.ContainsKey(1));
            bool sawRemoval = false;
            while (replica.TryDequeueEvent(out var evt))
                if (evt.Kind == ReplicaEventKind.ShipRemoved && evt.NetId == 2) sawRemoval = true;
            Assert.IsTrue(sawRemoval);

            Feed(replica, Bytes(Snap(2, 2)));
            Assert.IsFalse(replica.Ships.ContainsKey(2), "a duplicate older snapshot must not bring the ship back");
        }
    }
}
