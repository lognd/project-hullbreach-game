using NUnit.Framework;
using Hullbreach.Core;
using Hullbreach.Match;
using Hullbreach.Platform;

namespace Hullbreach.Net.Tests
{
    // The ready-up handshake: ReadyUp from both peers starts the match and MatchStarted reaches both.
    public class ServerReadyUpTests
    {
        static ShipSnapshot Ship() => new ShipSnapshot(0, 0, new[] { new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0) }, 0, 0, 0, 0, 0, 0);

        static (ServerSimulation server, ServerOutbox outbox) Lobby(bool requireReady = true)
        {
            var outbox = new ServerOutbox();
            var server = new ServerSimulation(outbox) { TickRate = 50f, MatchFeed = new MatchEndFeed(), RequireReady = requireReady };
            return (server, outbox);
        }

        static int CountStarted(ServerOutbox outbox, int peer)
        {
            int n = 0;
            foreach (var e in outbox.Entries)
                if (e.Peer == peer && e.Reliable && e.Bytes[0] == (byte)MessageKind.MatchStarted) n++;
            return n;
        }

        [Test]
        public void TwoJoined_MatchWaitsForReady()
        {
            var (server, outbox) = Lobby();
            server.Join(1, Ship());
            server.Join(2, Ship());
            Assert.IsNull(server.Match, "a full lobby must not start without ready-up");
            Assert.AreEqual(0, CountStarted(outbox, 1));
        }

        [Test]
        public void OneReady_DoesNotStart()
        {
            var (server, outbox) = Lobby();
            server.Join(1, Ship());
            server.Join(2, Ship());
            Assert.IsTrue(server.SetReady(1, true));
            Assert.IsNull(server.Match);
            Assert.AreEqual(0, CountStarted(outbox, 2));
        }

        [Test]
        public void BothReady_StartsTheMatch_AndAnnouncesToBothPeersOnce()
        {
            var (server, outbox) = Lobby();
            server.Join(1, Ship());
            server.Join(2, Ship());
            server.SetReady(1, true);
            server.SetReady(2, true);
            server.SetReady(2, true);
            Assert.AreEqual(MatchPhase.Running, server.Match.Phase);
            Assert.AreEqual(1, CountStarted(outbox, 1));
            Assert.AreEqual(1, CountStarted(outbox, 2));
            foreach (var e in outbox.Entries)
            {
                if (e.Bytes[0] != (byte)MessageKind.MatchStarted) continue;
                var r = new ByteReader(e.Bytes, 0, e.Bytes.Length);
                var m = MatchStarted.Read(ref r);
                Assert.IsFalse(r.Failed);
                Assert.AreEqual(1, m.PeerA);
                Assert.AreEqual(2, m.PeerB);
            }
        }

        [Test]
        public void DisconnectBeforeReady_NeverStarts_UntilAReplacementReadies()
        {
            var (server, outbox) = Lobby();
            server.Join(1, Ship());
            server.Join(2, Ship());
            server.SetReady(1, true);
            server.Leave(2);
            Assert.IsNull(server.Match);
            Assert.IsFalse(server.SetReady(2, true), "the leaver is gone");
            Assert.AreEqual(0, CountStarted(outbox, 1));

            server.Join(3, Ship());
            Assert.IsNull(server.Match, "a newcomer is not ready yet");
            server.SetReady(3, true);
            Assert.AreEqual(MatchPhase.Running, server.Match.Phase);
            Assert.AreEqual(1, CountStarted(outbox, 1));
            Assert.AreEqual(1, CountStarted(outbox, 3));
        }

        [Test]
        public void ReadyFromAnUnjoinedPeer_IsIgnored()
        {
            var (server, _) = Lobby();
            server.Join(1, Ship());
            Assert.IsFalse(server.SetReady(7, true));
        }

        [Test]
        public void WithoutRequireReady_TheLegacyStartOnSecondJoinStillHolds()
        {
            var (server, outbox) = Lobby(requireReady: false);
            server.Join(1, Ship());
            server.Join(2, Ship());
            Assert.AreEqual(MatchPhase.Running, server.Match.Phase);
            Assert.AreEqual(0, CountStarted(outbox, 1));
            Assert.IsFalse(server.SetReady(1, true));
        }

        [Test]
        public void ReadyUp_RoundTrips_AndRejectsABadFlag()
        {
            foreach (bool ready in new[] { true, false })
            {
                var buf = new byte[ReadyUp.ByteSize];
                var w = new ByteWriter(buf);
                new ReadyUp(ready).Write(ref w);
                Assert.AreEqual(ReadyUp.ByteSize, w.Position);
                var r = new ByteReader(buf, 0, w.Position);
                var m = ReadyUp.Read(ref r);
                Assert.IsFalse(r.Failed);
                Assert.AreEqual(ready, m.Ready);
            }
            var bad = new byte[] { (byte)MessageKind.ReadyUp, 2 };
            var br = new ByteReader(bad, 0, bad.Length);
            ReadyUp.Read(ref br);
            Assert.IsTrue(br.Failed);
            var shortBytes = new byte[] { (byte)MessageKind.ReadyUp };
            var sr = new ByteReader(shortBytes, 0, 1);
            ReadyUp.Read(ref sr);
            Assert.IsTrue(sr.Failed);
        }

        [Test]
        public void MatchStarted_RoundTrips_AndTruncationFails()
        {
            var buf = new byte[MatchStarted.ByteSize];
            var w = new ByteWriter(buf);
            new MatchStarted(5, 1, 2).Write(ref w);
            var r = new ByteReader(buf, 0, w.Position);
            var m = MatchStarted.Read(ref r);
            Assert.IsFalse(r.Failed);
            Assert.AreEqual(5u, m.Sequence);
            Assert.AreEqual(1, m.PeerA);
            Assert.AreEqual(2, m.PeerB);
            var tr = new ByteReader(buf, 0, w.Position - 1);
            MatchStarted.Read(ref tr);
            Assert.IsTrue(tr.Failed);
        }

        [Test]
        public void ServerToReplica_MatchStartedReachesTheClient_InOrder()
        {
            var (server, outbox) = Lobby();
            var replica = new ClientReplica();
            server.Join(1, Ship());
            server.Join(2, Ship());
            server.SetReady(1, true);
            server.SetReady(2, true);
            Assert.IsNull(replica.MatchStartedPeers);
            foreach (var e in outbox.Entries)
                if (e.Peer == 1) replica.ApplyReceived(e.Bytes, e.Bytes.Length);
            Assert.IsTrue(replica.MatchStartedPeers.HasValue);
            Assert.AreEqual(1, replica.MatchStartedPeers.Value.PeerA);
            Assert.AreEqual(2, replica.MatchStartedPeers.Value.PeerB);
            Assert.AreEqual(2, replica.Ships.Count, "both ships are visible once the match starts");
        }
    }
}
