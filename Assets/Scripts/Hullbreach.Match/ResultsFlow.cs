using System;
using Hullbreach.Platform;

namespace Hullbreach.Match
{
    // What the flow needs from a results screen; HullbreachResultsView is the uGUI implementation.
    // frob:ticket 01M5VF1PBQZ9D0Y25J0PQB6Q8N
    // frob:doc docs/reference/hullbreach-match.md#resultsflow
    public interface IResultsPresenter
    {
        // frob:doc docs/reference/hullbreach-match.md#resultsflow
        void Show(ResultsScreenModel model);

        // frob:doc docs/reference/hullbreach-match.md#resultsflow
        void Hide();
    }

    // Engine-free wiring: a match-end feed event becomes a ResultsScreenModel shown on the presenter,
    // and the player's rematch / lobby pick is routed to the lobby flow callbacks.
    // frob:ticket 01M5VF1PBQZ9D0Y25J0PQB6Q8N
    // frob:doc docs/reference/hullbreach-match.md#resultsflow
    public sealed class ResultsFlow : IDisposable
    {
        readonly IResultsPresenter _presenter;
        readonly Action _onRematch;
        readonly Action _onReturnToLobby;
        readonly Func<MatchEndedArgs, MatchResult> _resultOf;
        readonly Func<string, string> _nameOf;
        readonly Func<int> _localPeer;
        readonly IPlatformLog _log;
        MatchEndFeed _feed;

        // resultOf supplies the real MatchResult (the feed args do not carry it); null infers it.
        // frob:doc docs/reference/hullbreach-match.md#resultsflow
        public ResultsFlow(IResultsPresenter presenter, Action onRematch, Action onReturnToLobby,
            Func<MatchEndedArgs, MatchResult> resultOf = null, Func<int> localPeer = null,
            Func<string, string> nameOf = null, IPlatformLog log = null)
        {
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _onRematch = onRematch;
            _onReturnToLobby = onReturnToLobby;
            _resultOf = resultOf ?? InferResult;
            _localPeer = localPeer;
            _nameOf = nameOf;
            _log = log ?? NullPlatformLog.Instance;
        }

        // Starts listening to the feed (replacing an earlier attachment); Dispose stops it.
        // frob:doc docs/reference/hullbreach-match.md#resultsflow
        public void Attach(MatchEndFeed feed)
        {
            Detach();
            _feed = feed ?? throw new ArgumentNullException(nameof(feed));
            _feed.Ended += Handle;
            _log.Info("Hullbreach results: listening for the match-end event.");
        }

        // Builds the model for a finished match and shows it; also the direct entry point.
        // frob:doc docs/reference/hullbreach-match.md#resultsflow
        public ResultsScreenModel OnEnded(MatchEndedArgs e)
        {
            MatchResult result = _resultOf(e);
            int local = _localPeer?.Invoke() ?? -1;
            ResultsScreenModel model = ResultsScreenModel.FromEnded(result, e, local, _nameOf, _log);
            model.RematchRequested += () => Route("rematch", _onRematch);
            model.ReturnToLobbyRequested += () => Route("return to lobby", _onReturnToLobby);
            _presenter.Show(model);
            return model;
        }

        // frob:doc docs/reference/hullbreach-match.md#resultsflow
        public void Dispose() => Detach();

        // A winnerPeer outside the two peers (-1) is a draw; anything else defaults to a core breach.
        static MatchResult InferResult(MatchEndedArgs e) =>
            e.WinnerPeer == e.PeerA || e.WinnerPeer == e.PeerB ? MatchResult.CoreBreached : MatchResult.Draw;

        void Handle(MatchEndedArgs e) => OnEnded(e);

        void Route(string what, Action target)
        {
            _presenter.Hide();
            _log.Info($"Hullbreach results: routing {what} to the lobby flow.");
            target?.Invoke();
        }

        void Detach()
        {
            if (_feed == null) return;
            _feed.Ended -= Handle;
            _feed = null;
        }
    }
}
