using System;
using Hullbreach.Core;
using Hullbreach.Platform;

namespace Hullbreach.Match
{
    // Lobby until Start, Running until a core falls (or someone leaves), then Ended for good.
    // frob:doc docs/reference/hullbreach-match.md#matchphase
    public enum MatchPhase
    {
        Lobby,
        Running,
        Ended,
    }

    // Why an Ended match ended; only CoreBreached has a winner and is reported to the platform.
    // frob:doc docs/reference/hullbreach-match.md#matchresult
    public enum MatchResult
    {
        None,
        CoreBreached,
        Draw,
        Abandoned,
    }

    // One side of the match: its network peer, its ship's grid, and a stats snapshot source.
    // frob:doc docs/reference/hullbreach-match.md#matchparticipant
    public sealed class MatchParticipant
    {
        // frob:doc docs/reference/hullbreach-match.md#matchparticipant
        public readonly int Peer;

        // frob:doc docs/reference/hullbreach-match.md#matchparticipant
        public readonly BlockGrid Grid;

        // frob:doc docs/reference/hullbreach-match.md#matchparticipant
        public readonly Func<PlayerMatchStats> Stats;

        // frob:doc docs/reference/hullbreach-match.md#matchparticipant
        public MatchParticipant(int peer, BlockGrid grid, Func<PlayerMatchStats> stats)
        {
            Peer = peer;
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
        }
    }

    // Plain, engine-free match state machine for roadmap S46-1: the server polls Tick, and when a
    // core is breached the match ends once and MatchEndFeed.Raise tells the platform reporter.
    // frob:ticket 01M3DG5Y35GWWAHRPBN3Y92T1Z
    // frob:ticket 01M5V8S9HK53WMKF29MXJYM8Z5
    // frob:doc docs/reference/hullbreach-match.md#matchstatemachine
    public sealed class MatchStateMachine
    {
        // A core block this damaged counts as destroyed (ServerSimulation caps damage at 255).
        const int DestroyedDamage = 255;

        readonly MatchParticipant _a;
        readonly MatchParticipant _b;
        readonly MatchEndFeed _feed;
        readonly Func<double> _clock;
        readonly Func<string> _newMatchId;
        readonly IPlatformLog _log;
        double _startedAt;

        // frob:doc docs/reference/hullbreach-match.md#matchstatemachine
        public MatchPhase Phase { get; private set; } = MatchPhase.Lobby;

        // frob:doc docs/reference/hullbreach-match.md#matchstatemachine
        public MatchResult Result { get; private set; } = MatchResult.None;

        // The winning peer, or -1 when there is none (not ended, draw, abandoned).
        // frob:doc docs/reference/hullbreach-match.md#matchstatemachine
        public int WinnerPeer { get; private set; } = -1;

        // Empty until the match starts; a fresh uuid per match.
        // frob:doc docs/reference/hullbreach-match.md#matchstatemachine
        public string MatchId { get; private set; } = string.Empty;

        // clock returns seconds; feed defaults to MatchEndFeed.Shared, newMatchId to a random uuid.
        // frob:doc docs/reference/hullbreach-match.md#matchstatemachine
        public MatchStateMachine(MatchParticipant a, MatchParticipant b, Func<double> clock,
            MatchEndFeed feed = null, Func<string> newMatchId = null, IPlatformLog log = null)
        {
            _a = a ?? throw new ArgumentNullException(nameof(a));
            _b = b ?? throw new ArgumentNullException(nameof(b));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _feed = feed ?? MatchEndFeed.Shared;
            _newMatchId = newMatchId ?? (() => Guid.NewGuid().ToString());
            _log = log ?? NullPlatformLog.Instance;
        }

        // Lobby -> Running; false (and still Lobby) when already started or a ship has no live core.
        // frob:doc docs/reference/hullbreach-match.md#matchstatemachine
        public bool Start()
        {
            if (Phase != MatchPhase.Lobby)
            {
                _log.Warning($"Hullbreach match: Start ignored in phase {Phase}.");
                return false;
            }
            if (CoreBreached(_a.Grid) || CoreBreached(_b.Grid))
            {
                _log.Warning("Hullbreach match: Start refused, a ship has no live core.");
                return false;
            }
            MatchId = _newMatchId();
            _startedAt = _clock();
            Phase = MatchPhase.Running;
            _log.Info($"Hullbreach match {MatchId}: started, peers {_a.Peer} vs {_b.Peer}.");
            return true;
        }

        // Polls both cores while Running; no-op in any other phase.
        // frob:doc docs/reference/hullbreach-match.md#matchstatemachine
        public void Tick()
        {
            if (Phase != MatchPhase.Running) return;
            bool aDown = CoreBreached(_a.Grid);
            bool bDown = CoreBreached(_b.Grid);
            if (aDown && bDown) End(MatchResult.Draw, -1);
            else if (aDown) End(MatchResult.CoreBreached, _b.Peer);
            else if (bDown) End(MatchResult.CoreBreached, _a.Peer);
        }

        // A peer leaving a Running match abandons it: no winner, nothing reported.
        // frob:doc docs/reference/hullbreach-match.md#matchstatemachine
        public void Disconnect(int peer)
        {
            if (Phase != MatchPhase.Running) return;
            if (peer != _a.Peer && peer != _b.Peer) return;
            _log.Warning($"Hullbreach match {MatchId}: peer {peer} disconnected.");
            End(MatchResult.Abandoned, -1);
        }

        static bool CoreBreached(BlockGrid grid)
        {
            if (!grid.CoreKey.HasValue) return true;
            return !grid.TryGet(grid.CoreKey.Value, out Block core) || core.Damage >= DestroyedDamage;
        }

        void End(MatchResult result, int winnerPeer)
        {
            Phase = MatchPhase.Ended;
            Result = result;
            WinnerPeer = winnerPeer;
            int duration = (int)Math.Round(Math.Max(0.0, _clock() - _startedAt));
            if (result != MatchResult.CoreBreached)
            {
                _log.Info($"Hullbreach match {MatchId}: ended {result} after {duration}s; not reported.");
                return;
            }
            _log.Info($"Hullbreach match {MatchId}: peer {winnerPeer} won after {duration}s.");
            _feed.Raise(new MatchEndedArgs(MatchId, winnerPeer, _a.Peer, _a.Stats(), _b.Peer, _b.Stats(), duration));
        }
    }
}
