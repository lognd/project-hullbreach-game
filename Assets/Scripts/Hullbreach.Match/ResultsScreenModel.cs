using System;
using System.Collections.Generic;
using System.Globalization;
using Hullbreach.Platform;

namespace Hullbreach.Match
{
    // One player's line on the results screen, already formatted for display.
    // frob:doc docs/reference/hullbreach-match.md#resultsscreenmodel
    public readonly struct ResultsPlayerRow
    {
        public readonly int Peer;
        public readonly string Name;
        public readonly bool IsWinner;
        public readonly string DamageDealt;
        public readonly string BlocksDestroyed;
        public readonly string BlocksPlaced;
        public readonly string TimeAlive;

        // frob:doc docs/reference/hullbreach-match.md#resultsscreenmodel
        public ResultsPlayerRow(int peer, string name, bool isWinner, PlayerMatchStats stats)
        {
            Peer = peer;
            Name = name;
            IsWinner = isWinner;
            DamageDealt = stats.DamageDealt.ToString(CultureInfo.InvariantCulture);
            BlocksDestroyed = stats.BlocksDestroyed.ToString(CultureInfo.InvariantCulture);
            BlocksPlaced = stats.BlocksPlaced.ToString(CultureInfo.InvariantCulture);
            TimeAlive = ResultsScreenModel.FormatClock(stats.TimeAliveSeconds);
        }
    }

    // Engine-free state of the post-match results screen (S46-3): winner, reason, duration, both
    // players' stats, and the rematch / return-to-lobby choice. The uGUI view only mirrors it.
    // frob:ticket 01M3DG5Y37VGGE65ERV1QQDH8X
    // frob:doc docs/reference/hullbreach-match.md#resultsscreenmodel
    public sealed class ResultsScreenModel
    {
        readonly IPlatformLog _log;
        bool _chosen;

        // frob:doc docs/reference/hullbreach-match.md#resultsscreenmodel
        public string Headline { get; }

        // Why the match ended, in words; an unknown MatchResult gets a generic line, never a throw.
        // frob:doc docs/reference/hullbreach-match.md#resultsscreenmodel
        public string Reason { get; }

        // frob:doc docs/reference/hullbreach-match.md#resultsscreenmodel
        public string Duration { get; }

        // frob:doc docs/reference/hullbreach-match.md#resultsscreenmodel
        public bool HasWinner { get; }

        // frob:doc docs/reference/hullbreach-match.md#resultsscreenmodel
        public ResultsPlayerRow PlayerA { get; }

        // frob:doc docs/reference/hullbreach-match.md#resultsscreenmodel
        public ResultsPlayerRow PlayerB { get; }

        // True until the player picks rematch or lobby (a second pick is ignored).
        // frob:doc docs/reference/hullbreach-match.md#resultsscreenmodel
        public bool CanChoose => !_chosen;

        // frob:doc docs/reference/hullbreach-match.md#resultsscreenmodel
        public event Action RematchRequested;

        // frob:doc docs/reference/hullbreach-match.md#resultsscreenmodel
        public event Action ReturnToLobbyRequested;

        // localPeer is the viewer's peer (-1 for a spectator or server view); nameOf maps a user id
        // to a display name and may be null or return empty, which falls back to "Player N".
        // frob:doc docs/reference/hullbreach-match.md#resultsscreenmodel
        public ResultsScreenModel(MatchResult result, int winnerPeer, int durationSeconds,
            int peerA, PlayerMatchStats statsA, int peerB, PlayerMatchStats statsB,
            int localPeer = -1, Func<string, string> nameOf = null, IPlatformLog log = null)
        {
            _log = log ?? NullPlatformLog.Instance;
            HasWinner = winnerPeer == peerA || winnerPeer == peerB;
            string nameA = NameFor(nameOf, statsA.UserId, "Player 1");
            string nameB = NameFor(nameOf, statsB.UserId, "Player 2");
            PlayerA = new ResultsPlayerRow(peerA, nameA, HasWinner && winnerPeer == peerA, statsA);
            PlayerB = new ResultsPlayerRow(peerB, nameB, HasWinner && winnerPeer == peerB, statsB);
            Duration = FormatClock(durationSeconds);
            Reason = ReasonFor(result);
            Headline = HeadlineFor(winnerPeer, localPeer);
            _log.Info($"Hullbreach results: {Headline} ({Reason}) after {Duration}.");
        }

        // The usual path: a finished match as the feed reported it.
        // frob:doc docs/reference/hullbreach-match.md#resultsscreenmodel
        public static ResultsScreenModel FromEnded(MatchResult result, MatchEndedArgs e, int localPeer = -1,
            Func<string, string> nameOf = null, IPlatformLog log = null) =>
            new ResultsScreenModel(result, e.WinnerPeer, e.DurationSeconds, e.PeerA, e.StatsA, e.PeerB,
                e.StatsB, localPeer, nameOf, log);

        // frob:doc docs/reference/hullbreach-match.md#resultsscreenmodel
        public void Rematch() => Choose("rematch", RematchRequested);

        // frob:doc docs/reference/hullbreach-match.md#resultsscreenmodel
        public void ReturnToLobby() => Choose("return to lobby", ReturnToLobbyRequested);

        // m:ss, or h:mm:ss from an hour up; negative input clamps to 0:00.
        // frob:doc docs/reference/hullbreach-match.md#resultsscreenmodel
        public static string FormatClock(int seconds)
        {
            int s = Math.Max(0, seconds);
            int h = s / 3600;
            int m = s / 60 % 60;
            return h > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", h, m, s % 60)
                : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", m, s % 60);
        }

        void Choose(string what, Action handlers)
        {
            if (_chosen)
            {
                _log.Warning($"Hullbreach results: {what} ignored, a choice was already made.");
                return;
            }
            _chosen = true;
            _log.Info($"Hullbreach results: player chose {what}.");
            handlers?.Invoke();
        }

        string HeadlineFor(int winnerPeer, int localPeer)
        {
            if (!HasWinner) return "DRAW";
            if (localPeer >= 0 && (localPeer == PlayerA.Peer || localPeer == PlayerB.Peer))
                return localPeer == winnerPeer ? "VICTORY" : "DEFEAT";
            return (PlayerA.IsWinner ? PlayerA.Name : PlayerB.Name) + " WINS";
        }

        string ReasonFor(MatchResult result)
        {
            switch (result)
            {
                case MatchResult.CoreBreached: return "Core breached";
                case MatchResult.Forfeit: return "Opponent left the match (forfeit)";
                case MatchResult.Draw: return "Both cores breached";
                case MatchResult.None: return "Match ended";
                default:
                    // New reasons (time limit, S46-2) arrive as enum names; show them spaced out.
                    _log.Warning($"Hullbreach results: no wording for {result}; using its name.");
                    return SpaceOut(result.ToString());
            }
        }

        static string NameFor(Func<string, string> nameOf, string userId, string fallback)
        {
            string name = nameOf?.Invoke(userId);
            return string.IsNullOrWhiteSpace(name) ? fallback : name;
        }

        static string SpaceOut(string pascal)
        {
            var chars = new List<char>(pascal.Length + 4);
            for (int i = 0; i < pascal.Length; i++)
            {
                if (i > 0 && char.IsUpper(pascal[i])) chars.Add(' ');
                chars.Add(i == 0 ? pascal[i] : char.ToLowerInvariant(pascal[i]));
            }
            return new string(chars.ToArray());
        }
    }
}
