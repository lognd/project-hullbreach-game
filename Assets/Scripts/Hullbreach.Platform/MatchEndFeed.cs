using System;

namespace Hullbreach.Match
{
    // Why an Ended match ended; CoreBreached, Forfeit and TimeLimit have a winner and are reported to the platform.
    // Lives in the Platform assembly (namespace Hullbreach.Match) so the feed args can carry it.
    // frob:doc docs/reference/hullbreach-match.md#matchresult
    public enum MatchResult
    {
        None,
        CoreBreached,
        Draw,
        Forfeit,
        TimeLimit,
    }
}

namespace Hullbreach.Platform
{
    using Hullbreach.Match;

    // Everything the platform needs to know about a match that just ended.
    // frob:doc docs/platform-link.md#reporting-a-finished-match
    public readonly struct MatchEndedArgs
    {
        public readonly string MatchId;
        public readonly int WinnerPeer;
        public readonly int PeerA;
        public readonly PlayerMatchStats StatsA;
        public readonly int PeerB;
        public readonly PlayerMatchStats StatsB;
        public readonly int DurationSeconds;

        // Why the match ended; None for senders that do not know (the results flow then infers it).
        // frob:doc docs/reference/hullbreach-match.md#matchresult
        public readonly MatchResult Result;

        // frob:doc docs/platform-link.md#reporting-a-finished-match
        public MatchEndedArgs(string matchId, int winnerPeer, int peerA, PlayerMatchStats statsA, int peerB,
            PlayerMatchStats statsB, int durationSeconds, MatchResult result = MatchResult.None)
        {
            MatchId = matchId;
            WinnerPeer = winnerPeer;
            PeerA = peerA;
            StatsA = statsA;
            PeerB = peerB;
            StatsB = statsB;
            DurationSeconds = durationSeconds;
            Result = result;
        }
    }

    // The seam between the match state machine (roadmap S46-1) and the platform reporter:
    // the machine calls Raise once per match, whatever is subscribed reacts.
    // frob:doc docs/platform-link.md#reporting-a-finished-match
    public sealed class MatchEndFeed
    {
        // The server's feed; tests build their own instances instead.
        // frob:doc docs/platform-link.md#reporting-a-finished-match
        public static readonly MatchEndFeed Shared = new MatchEndFeed();

        // frob:doc docs/platform-link.md#reporting-a-finished-match
        public event Action<MatchEndedArgs> Ended;

        // Tells every subscriber the match is over; returns how many were told.
        // frob:doc docs/platform-link.md#reporting-a-finished-match
        public int Raise(MatchEndedArgs args)
        {
            Action<MatchEndedArgs> handlers = Ended;
            if (handlers == null) return 0;
            handlers(args);
            return handlers.GetInvocationList().Length;
        }
    }
}
