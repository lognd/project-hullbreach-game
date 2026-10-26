using Hullbreach.Platform;
using NUnit.Framework;

namespace Hullbreach.Match.Tests
{
    // Winner, reason, duration, stats and the one-shot rematch / lobby choice.
    // frob:ticket 01M3DG5Y37VGGE65ERV1QQDH8X
    public sealed class ResultsScreenModelTests
    {
        static readonly PlayerMatchStats A = new PlayerMatchStats("ua", 900, 14, 30, 240);
        static readonly PlayerMatchStats B = new PlayerMatchStats("ub", 400, 12, 28, 3725);

        static ResultsScreenModel Make(MatchResult r, int winner, int local = -1) =>
            new ResultsScreenModel(r, winner, 185, 1, A, 2, B, local, u => u == "ua" ? "Ann" : "");

        [Test]
        public void CoreBreached_ShowsWinnerReasonDurationAndStats()
        {
            var m = Make(MatchResult.CoreBreached, 1);
            Assert.That(m.Headline, Is.EqualTo("Ann WINS"));
            Assert.That(m.Reason, Is.EqualTo("Core breached"));
            Assert.That(m.Duration, Is.EqualTo("3:05"));
            Assert.That(m.PlayerA.IsWinner, Is.True);
            Assert.That(m.PlayerB.IsWinner, Is.False);
            Assert.That(m.PlayerA.DamageDealt, Is.EqualTo("900"));
            Assert.That(m.PlayerB.Name, Is.EqualTo("Player 2"));
            Assert.That(m.PlayerB.TimeAlive, Is.EqualTo("1:02:05"));
        }

        [Test]
        public void Headline_IsVictoryOrDefeatForTheLocalPlayer()
        {
            Assert.That(Make(MatchResult.CoreBreached, 1, 1).Headline, Is.EqualTo("VICTORY"));
            Assert.That(Make(MatchResult.CoreBreached, 1, 2).Headline, Is.EqualTo("DEFEAT"));
        }

        [Test]
        public void Forfeit_ExplainsTheOpponentLeft()
        {
            Assert.That(Make(MatchResult.Forfeit, 2).Reason, Does.Contain("forfeit"));
        }

        [Test]
        public void TimeLimit_NamesTheLimit()
        {
            Assert.That(Make(MatchResult.TimeLimit, 1).Reason, Is.EqualTo("Time limit reached"));
        }

        [Test]
        public void Draw_HasNoWinner()
        {
            var m = Make(MatchResult.Draw, -1, 1);
            Assert.That(m.HasWinner, Is.False);
            Assert.That(m.Headline, Is.EqualTo("DRAW"));
            Assert.That(m.PlayerA.IsWinner || m.PlayerB.IsWinner, Is.False);
        }

        [Test]
        public void UnknownReason_GetsItsNameInsteadOfThrowing()
        {
            Assert.That(Make((MatchResult)99, 1).Reason, Is.EqualTo("99"));
        }

        [Test]
        public void FormatClock_ClampsNegativeAndRollsOverHours()
        {
            Assert.That(ResultsScreenModel.FormatClock(-5), Is.EqualTo("0:00"));
            Assert.That(ResultsScreenModel.FormatClock(59), Is.EqualTo("0:59"));
            Assert.That(ResultsScreenModel.FormatClock(3600), Is.EqualTo("1:00:00"));
        }

        [Test]
        public void Choice_FiresOnceAndSecondIsIgnored()
        {
            var m = Make(MatchResult.CoreBreached, 1);
            int rematch = 0, lobby = 0;
            m.RematchRequested += () => rematch++;
            m.ReturnToLobbyRequested += () => lobby++;
            Assert.That(m.CanChoose, Is.True);
            m.Rematch();
            m.ReturnToLobby();
            m.Rematch();
            Assert.That(rematch, Is.EqualTo(1));
            Assert.That(lobby, Is.EqualTo(0));
            Assert.That(m.CanChoose, Is.False);
        }

        [Test]
        public void FromEnded_CopiesTheFeedArguments()
        {
            var e = new MatchEndedArgs("m", 2, 1, A, 2, B, 61);
            var m = ResultsScreenModel.FromEnded(MatchResult.CoreBreached, e, 2);
            Assert.That(m.Headline, Is.EqualTo("VICTORY"));
            Assert.That(m.Duration, Is.EqualTo("1:01"));
        }
    }
}
