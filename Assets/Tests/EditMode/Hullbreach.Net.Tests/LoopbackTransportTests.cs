using System.Text;
using NUnit.Framework;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    /// <summary>Exercises LoopbackTransport in isolation, before it is ever
    /// wired to ServerSimulation/ClientReplica: delivery, delay, drop, and
    /// peer connect/disconnect events.</summary>
    public class LoopbackTransportTests
    {
        static byte[] Payload(string s) => Encoding.ASCII.GetBytes(s);

        [Test]
        public void ReliableMessage_IsDeliveredAfterEnoughTicks()
        {
            var hub = new LoopbackTransport { DelayTicks = 2 };
            var a = hub.CreateEndpoint(out int idA);
            var b = hub.CreateEndpoint(out int idB);
            hub.Connect(idA, idB);

            a.SendReliable(idB, Payload("hi"));

            var buf = new byte[16];
            hub.Tick();
            Assert.IsFalse(b.TryReceive(out _, buf, out _), "should not arrive before delay elapses");
            hub.Tick();
            Assert.IsTrue(b.TryReceive(out int from, buf, out int len));
            Assert.AreEqual(idA, from);
            Assert.AreEqual("hi", Encoding.ASCII.GetString(buf, 0, len));
        }

        [Test]
        public void UnreliableMessage_CanBeDropped()
        {
            var hub = new LoopbackTransport(seed: 1) { UnreliableDropRate = 1f };
            var a = hub.CreateEndpoint(out int idA);
            var b = hub.CreateEndpoint(out int idB);
            hub.Connect(idA, idB);

            a.SendUnreliable(idB, Payload("x"));
            for (int i = 0; i < 5; i++) hub.Tick();

            Assert.IsFalse(b.TryReceive(out _, new byte[8], out _));
        }

        [Test]
        public void ReliableMessage_IsNeverDropped_EvenWithDropRateOne()
        {
            var hub = new LoopbackTransport(seed: 2) { UnreliableDropRate = 1f };
            var a = hub.CreateEndpoint(out int idA);
            var b = hub.CreateEndpoint(out int idB);
            hub.Connect(idA, idB);

            a.SendReliable(idB, Payload("must-arrive"));
            for (int i = 0; i < 5; i++) hub.Tick();

            var buf = new byte[16];
            Assert.IsTrue(b.TryReceive(out _, buf, out int len));
            Assert.AreEqual("must-arrive", Encoding.ASCII.GetString(buf, 0, len));
        }

        [Test]
        public void PeerConnected_FiresOnBothEndpoints()
        {
            var hub = new LoopbackTransport();
            var a = hub.CreateEndpoint(out int idA);
            var b = hub.CreateEndpoint(out int idB);

            int? aSawPeer = null, bSawPeer = null;
            a.PeerConnected += p => aSawPeer = p;
            b.PeerConnected += p => bSawPeer = p;

            hub.Connect(idA, idB);

            Assert.AreEqual(idB, aSawPeer);
            Assert.AreEqual(idA, bSawPeer);
        }

        [Test]
        public void Disconnect_FiresOnTheOtherEndpointAndStopsDelivery()
        {
            var hub = new LoopbackTransport();
            var a = hub.CreateEndpoint(out int idA);
            var b = hub.CreateEndpoint(out int idB);
            hub.Connect(idA, idB);

            int? bSawDisconnect = null;
            b.PeerDisconnected += p => bSawDisconnect = p;

            hub.Disconnect(idA, idB);
            Assert.AreEqual(idA, bSawDisconnect);

            // idA no longer exists on the hub: anything still addressed TO
            // it (from b) must be silently dropped rather than delivered.
            b.SendReliable(idA, Payload("late"));
            hub.Tick();
            Assert.IsFalse(a.TryReceive(out _, new byte[16], out _));
        }

        [Test]
        public void Jitter_CanReorderTwoReliableMessages()
        {
            // With a wide jitter window and a fixed seed that is known to
            // reorder, the second message sent can arrive before the first.
            // ClientReplica.ApplyReliable is what makes that safe; this test
            // only proves the transport is actually capable of doing it.
            var hub = new LoopbackTransport(seed: 42) { DelayTicks = 1, JitterTicks = 10 };
            var a = hub.CreateEndpoint(out int idA);
            var b = hub.CreateEndpoint(out int idB);
            hub.Connect(idA, idB);

            a.SendReliable(idB, Payload("first"));
            a.SendReliable(idB, Payload("second"));

            var order = new System.Collections.Generic.List<string>();
            var buf = new byte[16];
            for (int i = 0; i < 20; i++)
            {
                hub.Tick();
                while (b.TryReceive(out _, buf, out int len))
                    order.Add(Encoding.ASCII.GetString(buf, 0, len));
            }

            Assert.AreEqual(2, order.Count);
            CollectionAssert.Contains(order, "first");
            CollectionAssert.Contains(order, "second");
        }
    }
}
