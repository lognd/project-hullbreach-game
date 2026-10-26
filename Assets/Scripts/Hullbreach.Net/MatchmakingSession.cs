using System;
using Hullbreach.Platform;

namespace Hullbreach.Net
{
    // Drives one search: the machine decides when, the client talks to the platform. The queue
    // screen calls Start, Tick every frame and Cancel, and reads Machine.State (S29-2/S29-3).
    // frob:doc docs/reference/hullbreach-net.md#matchmaking
    // frob:ticket 01M3DG5Y1PX311E24E62K5FAKG
    public sealed class MatchmakingSession
    {
        readonly MatchmakingClient _client;
        readonly IPlatformLog _log;
        string _token = string.Empty;
        bool _needJoin;
        bool _acknowledgedConflict;
        int _generation;

        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public MatchmakingSession(MatchmakingClient client, MatchmakingMachine machine, IPlatformLog log = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            Machine = machine ?? throw new ArgumentNullException(nameof(machine));
            _log = log ?? NullPlatformLog.Instance;
        }

        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public MatchmakingMachine Machine { get; }

        // Begins a search with the player's bearer token; false while one is already running.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public bool Start(string token)
        {
            if (!Machine.Start()) return false;
            _token = token ?? string.Empty;
            _needJoin = true;
            _acknowledgedConflict = false;
            _generation++;
            _log.Info("Hullbreach matchmaking: search started.");
            return true;
        }

        // Call once per frame: times out, then sends the join or poll when one is due.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public void Tick()
        {
            if (Machine.State != MatchmakingState.Queued) return;
            if (Machine.CheckTimeout())
            {
                _log.Warning("Hullbreach matchmaking: search timed out; leaving the queue.");
                SendLeave();
                return;
            }
            if (!Machine.IsPollDue()) return;

            int generation = _generation;
            Machine.MarkRequestSent();
            Action<QueueOutcome> handler = outcome => OnOutcome(generation, outcome);
            if (_needJoin) _client.Join(_token, handler);
            else _client.Poll(_token, handler);
        }

        // Backs out of the queue; false when no search is running.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public bool Cancel()
        {
            if (!Machine.Cancel()) return false;
            _log.Info("Hullbreach matchmaking: search cancelled.");
            SendLeave();
            return true;
        }

        void OnOutcome(int generation, QueueOutcome outcome)
        {
            // A late answer from an earlier or cancelled search must not touch the current one.
            if (generation != _generation || Machine.State != MatchmakingState.Queued) return;

            if (outcome.Status == QueueCallStatus.AlreadyMatched && !_acknowledgedConflict)
            {
                // An old match is still held: acknowledge it once, then join again.
                _acknowledgedConflict = true;
                SendLeave();
                Machine.OnError(outcome.Message, true);
                return;
            }
            if (!outcome.IsOk)
            {
                Machine.OnError(PlayerMessage(outcome), outcome.IsRetryable);
                return;
            }

            _needJoin = false;
            switch (outcome.Queue.State)
            {
                case QueueWireState.Queued:
                    Machine.OnQueued(outcome.Queue.Rating);
                    break;
                case QueueWireState.Matched:
                    Machine.OnMatched(outcome.Queue.Match);
                    // The match is read, so acknowledge it; the platform blocks a rejoin until then.
                    SendLeave();
                    if (Machine.State == MatchmakingState.Matched)
                        _log.Info($"Hullbreach matchmaking: matched, {Machine.Handoff}.");
                    break;
                default:
                    Machine.OnIdle();
                    break;
            }
        }

        static string PlayerMessage(QueueOutcome outcome)
        {
            switch (outcome.Status)
            {
                case QueueCallStatus.Unauthorized: return "Sign in again to find a match.";
                case QueueCallStatus.Unreachable: return "Cannot reach the Hullbreach servers.";
                case QueueCallStatus.AlreadyMatched: return "An earlier match is still open. Try again shortly.";
                default: return "Matchmaking failed: " + outcome.Message + ".";
            }
        }

        // Fire and forget: DELETE always succeeds server-side and there is nothing to retry.
        void SendLeave() => _client.Cancel(_token, _ => { });
    }
}
