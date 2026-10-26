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
    // frob:ticket 01M3DG5Y36WE3D2224D54MR2S6
    // frob:ticket 01M5VGS9DWNF85ZAQ0GD5X8A1J
    // frob:doc docs/reference/hullbreach-match.md#matchstatemachine
    public sealed class MatchStateMachine
    {
        readonly MatchParticipant _a;
        readonly MatchParticipant _b;
        readonly MatchEndFeed _feed;
        readonly Func<double> _clock;
        readonly Func<string> _newMatchId;
        readonly IPlatformLog _log;
        readonly double _timeLimit;
        double _startedAt;

        // frob:doc docs/reference/hullbreach-match.md#matchstatemachine
        public MatchPhase Phase { get; private set; } = MatchPhase.Lobby;

        // frob:doc docs/reference/hullbreach-match.md#matchstatemachine
        public MatchResult Result { get; private set; } = MatchResult.None;

        // The winning peer, or -1 when there is none (not ended, draw, abandoned).
        // frob:doc docs/reference/hullbreach-match.md#matchstatemachine
        public int WinnerPeer { get; private set; } = -1;

        // What the feed was told when the match ended (draws included); null until then.
        // frob:ticket 01M5VGS9DWNF85ZAQ0GD5X8A1J
        // frob:doc docs/reference/hullbreach-match.md#matchstatemachine
        public MatchEndedArgs? EndedArgs { get; private set; }

        // Empty until the match starts; a fresh uuid per match.
        // frob:doc docs/reference/hullbreach-match.md#matchstatemachine
        public string MatchId { get; private set; } = string.Empty;

        // clock returns seconds; timeLimitSeconds <= 0 means no limit; feed defaults to MatchEndFeed.Shared, newMatchId to a random uuid.
        // frob:doc docs/reference/hullbreach-match.md#matchstatemachine
        public MatchStateMachine(MatchParticipant a, MatchParticipant b, Func<double> clock,
            MatchEndFeed feed = null, Func<string> newMatchId = null, IPlatformLog log = null,
            double timeLimitSeconds = 0)
        {
            _timeLimit = timeLimitSeconds;
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
            else if (_timeLimit > 0 && _clock() - _startedAt >= _timeLimit) EndOnTimeLimit();
        }

        // Time is up with both cores alive: the higher core health wins, equal health is a Draw.
        // frob:ticket 01M3DG5Y36WE3D2224D54MR2S6
        void EndOnTimeLimit()
        {
            int healthA = CoreHealth(_a.Grid);
            int healthB = CoreHealth(_b.Grid);
            _log.Info($"Hullbreach match {MatchId}: time limit {_timeLimit}s reached, core health {healthA} vs {healthB}.");
            if (healthA == healthB) End(MatchResult.Draw, -1);
            else End(MatchResult.TimeLimit, healthA > healthB ? _a.Peer : _b.Peer);
        }

        // Core health is the undamaged part of the core block (255 minus damage).
        static int CoreHealth(BlockGrid grid)
        {
            if (!grid.CoreKey.HasValue || !grid.TryGet(grid.CoreKey.Value, out Block core)) return 0;
            return Math.Max(0, Connectivity.DestroyedDamage - core.Damage);
        }

        // A peer leaving a Running match forfeits it: the remaining peer wins and is reported,
        // because the platform report needs a winner.
        // frob:ticket 01M5V9XQ5361EETNPBNQ1H60DM
        // frob:doc docs/reference/hullbreach-match.md#matchstatemachine
        public void Disconnect(int peer)
        {
            if (Phase != MatchPhase.Running) return;
            if (peer != _a.Peer && peer != _b.Peer) return;
            int remaining = peer == _a.Peer ? _b.Peer : _a.Peer;
            _log.Warning($"Hullbreach match {MatchId}: peer {peer} disconnected, forfeit to peer {remaining}.");
            End(MatchResult.Forfeit, remaining);
        }

        // Only a destroyed core ends a match; an isolated one is still alive.
        static bool CoreBreached(BlockGrid grid) =>
            Connectivity.ClassifyCore(grid) == CoreState.Destroyed;

        void End(MatchResult result, int winnerPeer)
        {
            Phase = MatchPhase.Ended;
            Result = result;
            WinnerPeer = winnerPeer;
            int duration = (int)Math.Round(Math.Max(0.0, _clock() - _startedAt));
            if (winnerPeer < 0)
                _log.Warning($"Hullbreach match {MatchId}: ended {result} after {duration}s; the feed carries it "
                    + "but the platform reporter skips it because the report requires a winner.");
            else
                _log.Info($"Hullbreach match {MatchId}: peer {winnerPeer} won by {result} after {duration}s.");
            EndedArgs = new MatchEndedArgs(MatchId, winnerPeer, _a.Peer, _a.Stats(), _b.Peer, _b.Stats(), duration, result);
            _feed.Raise(EndedArgs.Value);
        }
    }
}
