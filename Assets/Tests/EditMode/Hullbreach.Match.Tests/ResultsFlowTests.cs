using Hullbreach.Platform;
using NUnit.Framework;

namespace Hullbreach.Match.Tests
{
    // Feed event -> model -> presenter, and rematch / lobby routing.
    // frob:ticket 01M5VF1PBQZ9D0Y25J0PQB6Q8N
    public sealed class ResultsFlowTests
    {
        sealed class FakePresenter : IResultsPresenter
        {
            public ResultsScreenModel Shown;
            public int Hides;
            public void Show(ResultsScreenModel model) => Shown = model;
            public void Hide() => Hides++;
        }

        static readonly PlayerMatchStats A = new PlayerMatchStats("ua", 1, 2, 3, 4);
        static readonly PlayerMatchStats B = new PlayerMatchStats("ub", 5, 6, 7, 8);

        static MatchEndedArgs Ended(int winner) => new MatchEndedArgs("m", winner, 1, A, 2, B, 60);

        // frob:ticket 01M5VGS9DWNF85ZAQ0GD5X8A1J
        // frob:tests Hullbreach.Match.ResultsFlow
        [Test]
        public void ArgsResult_IsUsed_InsteadOfInference()
        {
            var feed = new MatchEndFeed();
            var view = new FakePresenter();
            using (var flow = new ResultsFlow(view, null, null, localPeer: () => 1))
            {
                flow.Attach(feed);
                feed.Raise(new MatchEndedArgs("m", 1, 1, A, 2, B, 60, MatchResult.Forfeit));
            }
            Assert.That(view.Shown.Reason, Does.Contain("forfeit"));
        }

        // frob:ticket 01M5VGS9DWNF85ZAQ0GD5X8A1J
        // frob:tests Hullbreach.Match.ResultsFlow
        [Test]
        public void SameMatchIdTwice_IsShownOnce()
        {
            var feed = new MatchEndFeed();
            var view = new FakePresenter();
            using (var flow = new ResultsFlow(view, null, null, localPeer: () => 1))
            {
                flow.Attach(feed);
                feed.Raise(Ended(1));
                view.Shown = null;
                feed.Raise(Ended(1));
            }
            Assert.That(view.Shown, Is.Null);
        }

        [Test]
        public void FeedEvent_ShowsModelOnPresenter()
        {
            var feed = new MatchEndFeed();
            var view = new FakePresenter();
            using (var flow = new ResultsFlow(view, null, null, localPeer: () => 1))
            {
                flow.Attach(feed);
                Assert.That(feed.Raise(Ended(1)), Is.EqualTo(1));
            }
            Assert.That(view.Shown.Headline, Is.EqualTo("VICTORY"));
            Assert.That(view.Shown.Duration, Is.EqualTo("1:00"));
        }

        [Test]
        public void Draw_WinnerMinusOne_ShowsDrawWithBothCoresReason()
        {
            var view = new FakePresenter();
            new ResultsFlow(view, null, null).OnEnded(Ended(-1));
            Assert.That(view.Shown.Headline, Is.EqualTo("DRAW"));
            Assert.That(view.Shown.Reason, Is.EqualTo("Both cores breached"));
        }

        [Test]
        public void ResultSource_OverridesInferredReason()
        {
            var view = new FakePresenter();
            new ResultsFlow(view, null, null, e => MatchResult.TimeLimit).OnEnded(Ended(2));
            Assert.That(view.Shown.Reason, Is.EqualTo("Time limit reached"));
        }

        [Test]
        public void RematchAndLobby_RouteToCallbacksAndHide()
        {
            int rematch = 0, lobby = 0;
            var view = new FakePresenter();
            var flow = new ResultsFlow(view, () => rematch++, () => lobby++);
            flow.OnEnded(Ended(1)).Rematch();
            Assert.That(rematch, Is.EqualTo(1));
            Assert.That(lobby, Is.EqualTo(0));
            Assert.That(view.Hides, Is.EqualTo(1));
            flow.OnEnded(Ended(1)).ReturnToLobby();
            Assert.That(lobby, Is.EqualTo(1));
        }

        [Test]
        public void Dispose_StopsListening()
        {
            var feed = new MatchEndFeed();
            var view = new FakePresenter();
            var flow = new ResultsFlow(view, null, null);
            flow.Attach(feed);
            flow.Dispose();
            Assert.That(feed.Raise(Ended(1)), Is.EqualTo(0));
            Assert.That(view.Shown, Is.Null);
        }
    }
}
