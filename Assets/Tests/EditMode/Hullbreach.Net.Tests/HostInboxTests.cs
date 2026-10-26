using NUnit.Framework;
using Hullbreach.Core;
using Hullbreach.Match;
using Hullbreach.Net;
using Hullbreach.Platform;

namespace Hullbreach.Net.Tests
{
    // The host decode path end to end: ReadyUp bytes sent over LoopbackTransport reach SetReady.
    public class HostInboxTests
    {
        static ShipSnapshot Ship() => new ShipSnapshot(0, 0, new[] { new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0) }, 0, 0, 0, 0, 0, 0);

        static void SendReady(ITransport client, int serverId, bool ready)
        {
            var buf = new byte[ReadyUp.ByteSize];
            var w = new ByteWriter(buf);
            new ReadyUp(ready).Write(ref w);
            client.SendReliable(serverId, new System.ReadOnlySpan<byte>(buf, 0, w.Position));
        }

        static void Pump(LoopbackTransport hub, ITransport serverTransport, ServerSimulation server)
        {
            hub.Tick();
            var scratch = new byte[512];
            while (serverTransport.TryReceive(out int from, scratch, out int length))
                HostInbox.Dispatch(server, from, scratch, length);
        }

        [Test]
        public void ReadyUpOverLoopback_FromBothClients_StartsTheMatch()
        {
            var hub = new LoopbackTransport(seed: 3);
            var serverTransport = hub.CreateEndpoint(out int serverId);
            var c1 = hub.CreateEndpoint(out int p1);
            var c2 = hub.CreateEndpoint(out int p2);
            hub.Connect(serverId, p1);
            hub.Connect(serverId, p2);

            var outbox = new ServerOutbox();
            var server = new ServerSimulation(outbox) { TickRate = 50f, MatchFeed = new MatchEndFeed(), RequireReady = true };
            server.Join(p1, Ship());
            server.Join(p2, Ship());

            SendReady(c1, serverId, true);
            Pump(hub, serverTransport, server);
            Assert.IsNull(server.Match, "one ready peer must not start the match");

            SendReady(c2, serverId, true);
            Pump(hub, serverTransport, server);
            Assert.IsNotNull(server.Match);
            Assert.AreEqual(MatchPhase.Running, server.Match.Phase);

            int started = 0;
            foreach (var e in outbox.Entries)
                if (e.Reliable && e.Bytes[0] == (byte)MessageKind.MatchStarted) started++;
            Assert.AreEqual(2, started, "MatchStarted goes to both peers");
        }

        [Test]
        public void ReadyUpThenUnready_DoesNotStart()
        {
            var hub = new LoopbackTransport(seed: 3);
            var serverTransport = hub.CreateEndpoint(out int serverId);
            var c1 = hub.CreateEndpoint(out int p1);
            var c2 = hub.CreateEndpoint(out int p2);
            hub.Connect(serverId, p1);
            hub.Connect(serverId, p2);
            var server = new ServerSimulation(new ServerOutbox()) { TickRate = 50f, MatchFeed = new MatchEndFeed(), RequireReady = true };
            server.Join(p1, Ship());
            server.Join(p2, Ship());

            // One Pump per message: LoopbackTransport.Tick releases same-tick messages newest-first.
            SendReady(c1, serverId, true);
            Pump(hub, serverTransport, server);
            SendReady(c1, serverId, false);
            Pump(hub, serverTransport, server);
            SendReady(c2, serverId, true);
            Pump(hub, serverTransport, server);
            Assert.IsNull(server.Match);
        }

        [Test]
        public void Dispatch_RejectsEmptyMalformedAndServerOnlyKinds()
        {
            var server = new ServerSimulation(new ServerOutbox()) { TickRate = 50f, RequireReady = true };
            server.Join(1, Ship());
            Assert.IsFalse(HostInbox.Dispatch(server, 1, new byte[8], 0), "empty");
            Assert.IsFalse(HostInbox.Dispatch(server, 1, new byte[] { (byte)MessageKind.ReadyUp }, 1), "truncated");
            Assert.IsFalse(HostInbox.Dispatch(server, 1, new byte[] { (byte)MessageKind.ReadyUp, 2 }, 2), "bad flag");
            Assert.IsFalse(HostInbox.Dispatch(server, 1, new byte[] { (byte)MessageKind.MatchStarted, 0, 0 }, 3), "server-only kind");
            Assert.IsTrue(HostInbox.Dispatch(server, 1, new byte[] { (byte)MessageKind.ReadyUp, 1 }, 2));
        }

        [Test]
        public void Dispatch_RoutesInputToSetInput()
        {
            var server = new ServerSimulation(new ServerOutbox()) { TickRate = 50f };
            server.Join(1, Ship());
            var buf = new byte[16];
            var w = new ByteWriter(buf);
            InputMessage.FromFloats(1, 1, 1f, 0f, false).Write(ref w);
            Assert.IsTrue(HostInbox.Dispatch(server, 1, buf, w.Position));
        }
    }
}
