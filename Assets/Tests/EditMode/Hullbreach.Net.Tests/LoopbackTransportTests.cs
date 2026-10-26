using System.Text;
using NUnit.Framework;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    // Exercises LoopbackTransport in isolation, before it is wired to
    // ServerSimulation/ClientReplica.
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

        static System.Collections.Generic.List<string> Drain(LoopbackTransport hub, ITransport rx, int ticks)
        {
            var order = new System.Collections.Generic.List<string>();
            var buf = new byte[16];
            for (int i = 0; i < ticks; i++)
            {
                hub.Tick();
                while (rx.TryReceive(out _, buf, out int len))
                    order.Add(Encoding.ASCII.GetString(buf, 0, len));
            }
            return order;
        }

        [Test]
        public void ReliableSends_InOneTick_ArriveInSendOrder()
        {
            var hub = new LoopbackTransport { DelayTicks = 1 };
            var a = hub.CreateEndpoint(out int idA);
            var b = hub.CreateEndpoint(out int idB);
            hub.Connect(idA, idB);

            for (int i = 0; i < 5; i++) a.SendReliable(idB, Payload("m" + i));

            CollectionAssert.AreEqual(new[] { "m0", "m1", "m2", "m3", "m4" }, Drain(hub, b, 3));
        }

        [Test]
        public void ReliableSends_StayOrdered_UnderJitter()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var hub = new LoopbackTransport(seed) { DelayTicks = 1, JitterTicks = 10 };
                var a = hub.CreateEndpoint(out int idA);
                var b = hub.CreateEndpoint(out int idB);
                hub.Connect(idA, idB);

                var want = new System.Collections.Generic.List<string>();
                for (int i = 0; i < 20; i++) { want.Add("m" + i); a.SendReliable(idB, Payload("m" + i)); }

                CollectionAssert.AreEqual(want, Drain(hub, b, 40), "seed " + seed);
            }
        }

        [Test]
        public void ReliableSends_StayOrdered_OnThrottledProfile()
        {
            var hub = LoopbackTransport.Throttled(50f, 100f, 0.02f, seed: 7);
            var a = hub.CreateEndpoint(out int idA);
            var b = hub.CreateEndpoint(out int idB);
            hub.Connect(idA, idB);

            var want = new System.Collections.Generic.List<string>();
            var got = new System.Collections.Generic.List<string>();
            for (int t = 0; t < 100; t++)
            {
                for (int k = 0; k < 2; k++)
                {
                    string m = "t" + t + "k" + k;
                    want.Add(m);
                    a.SendReliable(idB, Payload(m));
                }
                got.AddRange(Drain(hub, b, 1));
            }
            got.AddRange(Drain(hub, b, 10));

            CollectionAssert.AreEqual(want, got);
        }

        [Test]
        public void Jitter_CanReorderUnreliableMessages()
        {
            bool reordered = false;
            for (int seed = 0; seed < 50 && !reordered; seed++)
            {
                var hub = new LoopbackTransport(seed) { DelayTicks = 1, JitterTicks = 10 };
                var a = hub.CreateEndpoint(out int idA);
                var b = hub.CreateEndpoint(out int idB);
                hub.Connect(idA, idB);
                for (int i = 0; i < 10; i++) a.SendUnreliable(idB, Payload("m" + i));
                var got = Drain(hub, b, 20);
                Assert.AreEqual(10, got.Count);
                var sorted = new System.Collections.Generic.List<string>(got);
                sorted.Sort(System.StringComparer.Ordinal);
                reordered = !sorted.Equals(got) && !System.Linq.Enumerable.SequenceEqual(sorted, got);
            }
            Assert.IsTrue(reordered, "unreliable sends may reorder under jitter");
        }
    }
}
