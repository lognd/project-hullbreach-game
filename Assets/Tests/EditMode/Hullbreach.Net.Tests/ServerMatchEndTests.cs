using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Match;
using Hullbreach.Platform;

namespace Hullbreach.Net.Tests
{
    // The live server builds, ticks and ends a MatchStateMachine from real impacts.
    public class ServerMatchEndTests
    {
        static ShipSnapshot Ship(params SnapshotBlock[] blocks)
            => new ShipSnapshot(0, 0, blocks, 0, 0, 0, 0, 0, 0);

        static SnapshotBlock Core() => new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0);

        static (ServerSimulation server, List<MatchEndedArgs> raised) Arena()
        {
            var feed = new MatchEndFeed();
            var raised = new List<MatchEndedArgs>();
            feed.Ended += raised.Add;
            var server = new ServerSimulation(new ServerOutbox()) { TickRate = 50f, MatchFeed = feed };
            return (server, raised);
        }

        [Test]
        public void Match_StartsOnlyWhenSecondPeerJoins()
        {
            var (server, _) = Arena();
            server.Join(1, Ship(Core()));
            Assert.IsNull(server.Match);
            server.Join(2, Ship(Core()));
            Assert.AreEqual(MatchPhase.Running, server.Match.Phase);
        }

        [Test]
        public void ImpactsThatDestroyACore_RaiseMatchEndOnce_WithTheShooterAsWinner()
        {
            var (server, raised) = Arena();
            server.Join(1, Ship(Core(), new SnapshotBlock(0, 1, BlockTypes.Cannon, 0, 0)));
            server.Join(2, Ship(Core()));
            server.Ships[1].Position = float2.zero;
            server.Ships[2].Position = new float2(0f, 4f);

            uint tick = 1;
            for (int i = 0; i < 3000 && server.Match.Phase == MatchPhase.Running; i++)
            {
                server.SetInput(1, InputMessage.FromFloats(1, tick, 0f, 0f, true));
                server.SetInput(2, InputMessage.FromFloats(2, tick, 0f, 0f, false));
                tick++;
                server.Tick();
            }
            Assert.AreEqual(MatchPhase.Ended, server.Match.Phase, "damaging the core to 255 must end the match");
            Assert.AreEqual(MatchResult.CoreBreached, server.Match.Result);
            Assert.AreEqual(1, server.Match.WinnerPeer);

            for (int i = 0; i < 50; i++) server.Tick();
            Assert.AreEqual(1, raised.Count, "the feed fires exactly once");
            Assert.AreEqual(1, raised[0].WinnerPeer);
            Assert.AreEqual(server.Match.MatchId, raised[0].MatchId);
            Assert.AreEqual(1, raised[0].PeerA);
            Assert.AreEqual(2, raised[0].PeerB);
        }

        [Test]
        public void LeavingMidMatch_IsAForfeitWin_ForTheRemainingPeer_AndIsRaised()
        {
            var (server, raised) = Arena();
            server.Join(1, Ship(Core()));
            server.Join(2, Ship(Core()));
            server.Leave(2);
            server.Tick();
            Assert.AreEqual(MatchResult.Forfeit, server.Match.Result);
            Assert.AreEqual(1, server.Match.WinnerPeer);
            Assert.AreEqual(1, raised.Count);
            Assert.AreEqual(1, raised[0].WinnerPeer);
        }

        [Test]
        public void ReportedStats_CountTheWinnersDamage_AndDestroyedCore_NotZeros()
        {
            var (server, raised) = Arena();
            server.Join(1, Ship(Core(), new SnapshotBlock(0, 1, BlockTypes.Cannon, 0, 0)));
            server.Join(2, Ship(Core()));
            server.Ships[1].Position = float2.zero;
            server.Ships[2].Position = new float2(0f, 4f);

            uint tick = 1;
            for (int i = 0; i < 3000 && server.Match.Phase == MatchPhase.Running; i++)
            {
                server.SetInput(1, InputMessage.FromFloats(1, tick, 0f, 0f, true));
                server.SetInput(2, InputMessage.FromFloats(2, tick, 0f, 0f, false));
                tick++;
                server.Tick();
            }
            Assert.AreEqual(1, raised.Count);
            Assert.Greater(raised[0].StatsA.DamageDealt, 0, "the shooter dealt damage");
            Assert.AreEqual(0, raised[0].StatsB.DamageDealt, "the target never fired");
            Assert.Greater(raised[0].StatsA.TimeAliveSeconds, 0);
        }

        [Test]
        public void BlocksPlacedDuringTheMatch_AreCounted()
        {
            var (server, raised) = Arena();
            server.Join(1, Ship(Core()));
            server.Join(2, Ship(Core()));
            server.SetInput(1, new InputMessage(1, 1, 0, 0, InputMessage.BuildModeBit));
            Assert.IsTrue(server.TryPlaceBlock(1, 1, 0, BlockTypes.Hull, 0));
            server.Leave(2);
            Assert.AreEqual(1, raised[0].StatsA.BlocksPlaced);
            Assert.AreEqual(0, raised[0].StatsB.BlocksPlaced);
        }
    }
}
