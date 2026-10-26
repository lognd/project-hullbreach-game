using System.Collections.Generic;
using Hullbreach.Core;
using Hullbreach.Platform;
using NUnit.Framework;

namespace Hullbreach.Match.Tests
{
    // Win on core destroyed, raise exactly once, and the no-winner endings.
    public sealed class MatchStateMachineTests
    {
        const string UserA = "aaaaaaaa-0000-4000-8000-000000000001";
        const string UserB = "bbbbbbbb-0000-4000-8000-000000000002";
        static readonly int CoreCell = BlockKey.Pack(0, 0);

        double _now;
        int _ids;
        BlockGrid _gridA;
        BlockGrid _gridB;
        MatchEndFeed _feed;
        List<MatchEndedArgs> _raised;
        MatchStateMachine _machine;

        static BlockGrid ShipGrid()
        {
            var g = new BlockGrid();
            g.TryAdd(CoreCell, new Block(BlockTypes.Core));
            return g;
        }

        static void DestroyCore(BlockGrid g) => g.TrySet(CoreCell, g.TryGet(CoreCell, out Block b) ? b.WithDamage(255) : b);

        [SetUp]
        public void SetUp()
        {
            _now = 100.0;
            _ids = 0;
            _gridA = ShipGrid();
            _gridB = ShipGrid();
            _feed = new MatchEndFeed();
            _raised = new List<MatchEndedArgs>();
            _feed.Ended += _raised.Add;
            _machine = new MatchStateMachine(
                new MatchParticipant(1, _gridA, () => new PlayerMatchStats(UserA, 900, 14, 30, 240)),
                new MatchParticipant(2, _gridB, () => new PlayerMatchStats(UserB, 400, 12, 28, 200)),
                () => _now, _feed, () => "match-" + (++_ids));
        }

        [Test]
        public void NewMachine_IsInLobby_AndTickDoesNothing()
        {
            _machine.Tick();
            Assert.AreEqual(MatchPhase.Lobby, _machine.Phase);
            Assert.AreEqual(0, _raised.Count);
        }

        [Test]
        public void Start_MovesToRunning_AndAssignsAFreshId()
        {
            Assert.IsTrue(_machine.Start());
            Assert.AreEqual(MatchPhase.Running, _machine.Phase);
            Assert.AreEqual("match-1", _machine.MatchId);
            Assert.IsFalse(_machine.Start(), "a second Start must not restart the match");
            Assert.AreEqual("match-1", _machine.MatchId);
        }

        [Test]
        public void Start_RefusedWhenAShipHasNoCore()
        {
            var m = new MatchStateMachine(
                new MatchParticipant(1, new BlockGrid(), () => default), new MatchParticipant(2, _gridB, () => default),
                () => _now, _feed);
            Assert.IsFalse(m.Start());
            Assert.AreEqual(MatchPhase.Lobby, m.Phase);
        }

        [Test]
        public void Tick_WhileCoresAlive_NeverRaises()
        {
            _machine.Start();
            _now += 30;
            _machine.Tick();
            _machine.Tick();
            Assert.AreEqual(MatchPhase.Running, _machine.Phase);
            Assert.AreEqual(0, _raised.Count, "no raise before the match ends");
        }

        [Test]
        public void CoreDestroyed_EndsMatch_WithOtherPeerAsWinner()
        {
            _machine.Start();
            _now += 125.4;
            DestroyCore(_gridA);
            _machine.Tick();

            Assert.AreEqual(MatchPhase.Ended, _machine.Phase);
            Assert.AreEqual(MatchResult.CoreBreached, _machine.Result);
            Assert.AreEqual(2, _machine.WinnerPeer);
            Assert.AreEqual(1, _raised.Count);
            MatchEndedArgs args = _raised[0];
            Assert.AreEqual("match-1", args.MatchId);
            Assert.AreEqual(2, args.WinnerPeer);
            Assert.AreEqual(1, args.PeerA);
            Assert.AreEqual(2, args.PeerB);
            Assert.AreEqual(UserA, args.StatsA.UserId);
            Assert.AreEqual(UserB, args.StatsB.UserId);
            Assert.AreEqual(125, args.DurationSeconds);
        }

        [Test]
        public void SecondPlayersCoreDestroyed_MakesFirstPlayerWin()
        {
            _machine.Start();
            DestroyCore(_gridB);
            _machine.Tick();
            Assert.AreEqual(1, _machine.WinnerPeer);
            Assert.AreEqual(1, _raised[0].WinnerPeer);
        }

        [Test]
        public void ManyTicksAfterEnd_RaiseExactlyOnce()
        {
            _machine.Start();
            DestroyCore(_gridA);
            for (int i = 0; i < 5; i++) _machine.Tick();
            DestroyCore(_gridB);
            _machine.Tick();
            _machine.Disconnect(1);
            Assert.AreEqual(1, _raised.Count);
            Assert.AreEqual(2, _machine.WinnerPeer, "the first ending stands");
        }

        // frob:ticket 01M5VGS9DWNF85ZAQ0GD5X8A1J
        // frob:tests Hullbreach.Match.MatchStateMachine
        [Test]
        public void BothCoresDestroyedTogether_IsADraw_RaisesTheFeedWithDrawResult()
        {
            _machine.Start();
            DestroyCore(_gridA);
            DestroyCore(_gridB);
            _machine.Tick();
            Assert.AreEqual(MatchPhase.Ended, _machine.Phase);
            Assert.AreEqual(MatchResult.Draw, _machine.Result);
            Assert.AreEqual(-1, _machine.WinnerPeer);
            Assert.AreEqual(1, _raised.Count, "draws reach the feed; the reporter skips them");
            Assert.AreEqual(MatchResult.Draw, _raised[0].Result);
            Assert.AreEqual(MatchResult.Draw, _machine.EndedArgs.Value.Result);
        }

        [Test]
        public void DisconnectWhileRunning_IsAForfeitWin_ForTheRemainingPeer_AndIsReported()
        {
            _machine.Start();
            _machine.Disconnect(2);
            Assert.AreEqual(MatchPhase.Ended, _machine.Phase);
            Assert.AreEqual(MatchResult.Forfeit, _machine.Result);
            Assert.AreEqual(1, _machine.WinnerPeer);
            Assert.AreEqual(1, _raised.Count);
            Assert.AreEqual(1, _raised[0].WinnerPeer);
        }

        [Test]
        public void Disconnect_OfUnknownPeerOrInLobby_IsIgnored()
        {
            _machine.Disconnect(1);
            Assert.AreEqual(MatchPhase.Lobby, _machine.Phase);
            _machine.Start();
            _machine.Disconnect(99);
            Assert.AreEqual(MatchPhase.Running, _machine.Phase);
        }

        [Test]
        public void MatchesGetDistinctDefaultIds()
        {
            var a = new MatchStateMachine(new MatchParticipant(1, _gridA, () => default),
                new MatchParticipant(2, _gridB, () => default), () => _now, _feed);
            var b = new MatchStateMachine(new MatchParticipant(1, ShipGrid(), () => default),
                new MatchParticipant(2, ShipGrid(), () => default), () => _now, _feed);
            a.Start();
            b.Start();
            Assert.AreNotEqual(a.MatchId, b.MatchId);
            Assert.AreEqual(36, a.MatchId.Length, "a uuid string");
        }

        // frob:ticket 01M3DG5Y36WE3D2224D54MR2S6
        MatchStateMachine Limited(double limit) => new MatchStateMachine(
            new MatchParticipant(1, _gridA, () => new PlayerMatchStats(UserA, 900, 14, 30, 240)),
            new MatchParticipant(2, _gridB, () => new PlayerMatchStats(UserB, 400, 12, 28, 200)),
            () => _now, _feed, () => "match-" + (++_ids), null, limit);

        static void Hurt(BlockGrid g, byte damage) => g.TrySet(CoreCell, g.TryGet(CoreCell, out Block b) ? b.WithDamage(damage) : b);

        [Test]
        public void TimeLimit_BeforeExpiry_KeepsRunning()
        {
            var m = Limited(60);
            m.Start();
            _now += 59;
            m.Tick();
            Assert.AreEqual(MatchPhase.Running, m.Phase);
        }

        [Test]
        public void TimeLimit_HealthierCoreWins_AndIsReportedOnce()
        {
            var m = Limited(60);
            m.Start();
            Hurt(_gridA, (byte)100);
            _now += 60;
            m.Tick();
            m.Tick();
            Assert.AreEqual(MatchResult.TimeLimit, m.Result);
            Assert.AreEqual(2, m.WinnerPeer);
            Assert.AreEqual(1, _raised.Count);
            Assert.AreEqual(2, _raised[0].WinnerPeer);
        }

        // frob:ticket 01M5VGS9DWNF85ZAQ0GD5X8A1J
        // frob:tests Hullbreach.Match.MatchStateMachine
        [Test]
        public void TimeLimit_EqualHealth_RaisesADraw()
        {
            var m = Limited(60);
            m.Start();
            Hurt(_gridA, 40);
            Hurt(_gridB, 40);
            _now += 61;
            m.Tick();
            Assert.AreEqual(MatchResult.Draw, m.Result);
            Assert.AreEqual(-1, m.WinnerPeer);
            Assert.AreEqual(1, _raised.Count);
            Assert.AreEqual(MatchResult.Draw, _raised[0].Result);
        }

        [Test]
        public void NoTimeLimit_NeverExpires()
        {
            _machine.Start();
            _now += 100000;
            _machine.Tick();
            Assert.AreEqual(MatchPhase.Running, _machine.Phase);
        }
    }
}
