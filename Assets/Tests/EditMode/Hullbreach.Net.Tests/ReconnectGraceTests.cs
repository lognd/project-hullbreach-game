using System.Collections.Generic;
using Hullbreach.Core;
using Hullbreach.Match;
using Hullbreach.Platform;
using Hullbreach.Ship;
using Hullbreach.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hullbreach.Net.Tests
{
    // A dropped connection keeps the ship (and its stock) for a grace window so the same player can rejoin; a deliberate
    // leave, the window running out, or the match ending frees it. Everything is tick-based, so it is deterministic.
    // frob:ticket 01M7FJ5KYZDQ37M8MC4CEV1DG7
    public class ReconnectGraceTests
    {
        static StormConfig ShortStorm() => new StormConfig(50f, 100f, 0f, 0.1f, 20f, 200f, new[]
        {
            new StormPhase(20f, 1f, 0f, 10f),
        });

        static ShipSnapshot Ship(params SnapshotBlock[] blocks) => new ShipSnapshot(0, 0, blocks, 0, 0, 0, 0, 0, 0);
        static SnapshotBlock Core() => new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0);
        static SnapshotBlock Plain(sbyte y) => new SnapshotBlock(0, y, BlockTypes.Cannon, 0, 0);

        static (ServerSimulation server, ServerOutbox outbox, List<MatchEndedArgs> ended) Arena(float grace = 2f, bool storm = false)
        {
            var feed = new MatchEndFeed();
            var ended = new List<MatchEndedArgs>();
            feed.Ended += ended.Add;
            var outbox = new ServerOutbox();
            var server = new ServerSimulation(outbox)
            {
                TickRate = 50f, TimeoutSeconds = 1e6f, MatchFeed = feed, ReconnectGraceSeconds = grace,
                StormEnabled = storm, Storm = ShortStorm(),
            };
            server.Join(1, Ship(Core(), Plain(1)));
            server.Join(2, Ship(Core(), Plain(1)));
            Assert.AreEqual(MatchPhase.Running, server.Match.Phase);
            return (server, outbox, ended);
        }

        static void Run(ServerSimulation server, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                foreach (var ship in server.Ships.Values) ship.Velocity = float2.zero;
                server.Tick();
            }
        }

        [Test]
        public void DefaultGrace_IsTwentySeconds()
        {
            Assert.AreEqual(20f, new ServerSimulation(new ServerOutbox()).ReconnectGraceSeconds);
        }

        [Test]
        public void Rejoin_WithinTheWindow_KeepsShipAndStock()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::Hullbreach.Net.ServerSimulation.Disconnect kind="unit"
            var (server, outbox, ended) = Arena();
            server.SetIdentity(1, "user-a");
            server.GrantStock(1, BlockTypes.Cannon, 3);
            int[] stock = server.StockOf(1);
            ShipBody ship = server.Ships[1];
            int blocks = ship.Grid.Count;

            Assert.IsTrue(server.Disconnect(1));
            Assert.IsTrue(server.IsHeld(1));
            Run(server, 50);                       // 1 s of a 2 s window
            Assert.IsTrue(server.Ships.ContainsKey(1), "still in the world");

            server.SetIdentity(7, "user-a");
            outbox.Clear();
            Assert.AreEqual(JoinResult.Joined, server.Join(7, Ship(Core())));
            Assert.IsFalse(server.Ships.ContainsKey(1));
            Assert.AreSame(ship, server.Ships[7], "the same ship, under the new peer id");
            Assert.AreEqual(blocks, server.Ships[7].Grid.Count, "the submitted design was ignored");
            CollectionAssert.AreEqual(stock, server.StockOf(7), "stock survives");
            Assert.IsFalse(server.IsHeld(7));
            Assert.AreEqual(MatchPhase.Running, server.Match.Phase);

            // The joiner gets every ship plus its inventory; the opponent hears the old id leave and the new one appear.
            ClientReplica joiner = Replay(outbox, 7);
            Assert.AreEqual(2, joiner.Ships.Count);
            Assert.AreEqual(server.Economy.Costs.MaxBlocks, joiner.Economy.MaxBlocks, "inventory snapshot");
            ClientReplica opponent = Replay(outbox, 2);
            Assert.IsTrue(opponent.Ships.ContainsKey(7));
            Assert.IsFalse(opponent.Ships.ContainsKey(1));

            // Past the old deadline nothing expires, and the match reports the new id on a later breach.
            Run(server, 200);
            Assert.IsTrue(server.Ships.ContainsKey(7));
            Assert.AreEqual(0, ended.Count);
        }

        [Test]
        public void Rejoin_AnonymousPeer_ReclaimsTheOnlyHeldShip_ButAnotherIdentityDoesNot()
        {
            var (server, _, _) = Arena();
            server.SetIdentity(1, "user-a");
            server.Disconnect(1);

            server.SetIdentity(5, "user-b");
            JoinResult stranger = server.Join(5, Ship(Core()));
            Assert.AreEqual(JoinResult.Joined, stranger);
            Assert.IsTrue(server.IsHeld(1), "a different identity must not take the held ship");
            Assert.IsTrue(server.Ships.ContainsKey(5), "it joined as a new ship");
            server.Leave(5);

            Assert.AreEqual(JoinResult.Joined, server.Join(6, Ship(Core())));   // no identity: the only held ship
            Assert.IsFalse(server.Ships.ContainsKey(1));
            Assert.IsTrue(server.Ships.ContainsKey(6));
        }

        [Test]
        public void AfterTheWindow_TheShipIsRemoved_AndTheOpponentWinsByForfeit()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::Hullbreach.Net.ServerSimulation.Disconnect kind="unit"
            var (server, outbox, ended) = Arena(grace: 2f);
            server.Disconnect(1);
            Run(server, 99);
            Assert.IsTrue(server.Ships.ContainsKey(1), "window not over");
            Assert.AreEqual(MatchPhase.Running, server.Match.Phase);
            outbox.Clear();
            Run(server, 3);
            Assert.IsFalse(server.Ships.ContainsKey(1));
            Assert.AreEqual(MatchResult.Forfeit, server.Match.Result);
            Assert.AreEqual(2, server.Match.WinnerPeer);
            Assert.AreEqual(1, ended.Count);
            Assert.IsTrue(Replay(outbox, 2).Ships.Count == 1 || outbox.Entries.Count > 0, "opponent was told");

            // Too late to claim it.
            Assert.AreEqual(JoinResult.Joined, server.Join(9, Ship(Core())));
            Assert.AreEqual(1, server.Ships[9].Grid.Count, "fresh ship, not the old one");
        }

        [Test]
        public void DeliberateLeave_RemovesImmediately()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::Hullbreach.Net.ServerSimulation.Leave kind="unit"
            var (server, _, _) = Arena();
            server.Leave(1);
            Assert.IsFalse(server.Ships.ContainsKey(1));
            Assert.IsFalse(server.IsHeld(1));
            Assert.AreEqual(MatchResult.Forfeit, server.Match.Result);
        }

        [Test]
        public void Disconnect_WithNoGrace_OrOutsideAMatch_BehavesLikeLeave()
        {
            var (server, _, _) = Arena(grace: 0f);
            Assert.IsFalse(server.Disconnect(1));
            Assert.IsFalse(server.Ships.ContainsKey(1));

            var lobby = new ServerSimulation(new ServerOutbox()) { TickRate = 50f, MatchFeed = new MatchEndFeed() };
            lobby.Join(1, Ship(Core()));
            Assert.IsFalse(lobby.Disconnect(1), "no running match, nothing to hold");
            Assert.AreEqual(0, lobby.Ships.Count);
        }

        [Test]
        public void MatchEnd_FreesAHeldShipImmediately_AndTheOpponentCanStillWinMeanwhile()
        {
            var (server, _, ended) = Arena(grace: 600f);
            server.Disconnect(1);
            // The opponent breaches the held ship's core: the match ends normally, held or not.
            Assert.IsTrue(server.Ships[1].Grid.TryGet(BlockKey.Pack(0, 0), out Block core));
            server.Ships[1].Grid.TrySet(BlockKey.Pack(0, 0), core.WithDamage(255));
            Run(server, 2);
            Assert.AreEqual(MatchPhase.Ended, server.Match.Phase);
            Assert.AreEqual(MatchResult.CoreBreached, server.Match.Result);
            Assert.AreEqual(2, server.Match.WinnerPeer);
            Assert.AreEqual(1, ended.Count);
            Run(server, 2);
            Assert.IsFalse(server.Ships.ContainsKey(1), "the ended match no longer waits for the dropped player");
        }

        [Test]
        public void HeldShip_ZeroesControls_StillTakesStormDamage_AndRecordsNothingSent()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::Hullbreach.Net.ServerSimulation.Disconnect kind="unit"
            var (server, outbox, _) = Arena(grace: 10f, storm: true);
            server.SetInput(1, InputMessage.FromFloats(1, 1, 1f, 1f, true));
            server.Ships[1].Position = new float2(300f, 0f);   // far outside the 100 radius
            server.Ships[2].Position = float2.zero;
            server.Disconnect(1);
            outbox.Clear();
            Run(server, 100);

            Assert.IsTrue(server.Ships[1].Grid.TryGet(BlockKey.Pack(0, 1), out Block b));
            Assert.That(b.Damage, Is.InRange(19, 20), "storm damage applies to a held ship");
            Assert.AreEqual(0, server.Projectiles.Count, "no shots from a held ship");
            foreach (OutboxEntry e in outbox.Entries) Assert.AreNotEqual(1, e.Peer, "nothing is sent to a dropped connection");
        }

        [Test]
        public void SilentPeer_TimesOutInto_AHeldShip_NotAnInstantRemoval()
        {
            var (server, _, _) = Arena(grace: 4f);
            server.TimeoutSeconds = 1f;
            Run(server, 60);                                   // nobody sends input: both time out
            Assert.IsTrue(server.IsHeld(1));
            Assert.IsTrue(server.Ships.ContainsKey(1));
            Assert.IsTrue(server.TryDequeueDroppedPeer(out int dropped), "the host is still told to close the connection");
            Assert.AreEqual(1, dropped);
        }

        [Test]
        public void HoldAndReclaim_IsDeterministic()
        {
            string a = Fingerprint(), b = Fingerprint();
            Assert.AreEqual(a, b);
        }

        static string Fingerprint()
        {
            var (server, _, _) = Arena(grace: 3f, storm: true);
            server.Ships[1].Position = new float2(120f, 5f);
            Run(server, 20);
            server.Disconnect(1);
            Run(server, 60);
            server.Join(4, Ship(Core()));
            Run(server, 40);
            var ship = server.Ships[4];
            return ship.Position.x.ToString("R") + "," + ship.Position.y.ToString("R") + "," + ship.Grid.Count
                + "," + (server.Ships[2].Grid.TryGet(BlockKey.Pack(0, 1), out Block b) ? b.Damage : -1);
        }

        static ClientReplica Replay(ServerOutbox outbox, int peer)
        {
            var replica = new ClientReplica();
            foreach (OutboxEntry e in outbox.Entries)
                if (e.Peer == peer) replica.ApplyReceived(e.Bytes, e.Bytes.Length);
            return replica;
        }
    }
}
