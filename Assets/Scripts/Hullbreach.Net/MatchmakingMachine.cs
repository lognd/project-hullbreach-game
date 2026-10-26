using System;

namespace Hullbreach.Net
{
    // Where a player is in the online queue (S29-2).
    // frob:doc docs/reference/hullbreach-net.md#matchmaking
    public enum MatchmakingState
    {
        Idle,
        Queued,
        Matched,
        Cancelled,
        Failed,
    }

    // Timing knobs of the queue loop; the platform asks for about one poll a second.
    // frob:doc docs/reference/hullbreach-net.md#matchmaking
    public readonly struct MatchmakingOptions
    {
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public MatchmakingOptions(double pollIntervalSeconds, double timeoutSeconds, int maxConsecutiveErrors)
        {
            PollIntervalSeconds = pollIntervalSeconds;
            TimeoutSeconds = timeoutSeconds;
            MaxConsecutiveErrors = maxConsecutiveErrors;
        }

        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public double PollIntervalSeconds { get; }
        // Give up after this long in the queue; zero or less means wait forever.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public double TimeoutSeconds { get; }
        // Transient failures in a row tolerated before the search fails.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public int MaxConsecutiveErrors { get; }

        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public static MatchmakingOptions Defaults() => new MatchmakingOptions(1.0, 300.0, 5);
    }

    // Pure queue state machine: no I/O, time comes from the injected clock (seconds). The
    // caller sends requests and feeds the answers back, so every rule is testable offline.
    // frob:doc docs/reference/hullbreach-net.md#matchmaking
    // frob:ticket 01M3DG5Y1PX311E24E62K5FAKG
    public sealed class MatchmakingMachine
    {
        readonly Func<double> _clock;
        readonly MatchmakingOptions _options;
        double _startedAt;
        double _nextPollAt;
        bool _inFlight;
        int _errors;

        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public MatchmakingMachine(Func<double> clock, MatchmakingOptions options)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _options = options;
        }

        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public MatchmakingState State { get; private set; } = MatchmakingState.Idle;
        // Set only in Matched.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public MatchHandoff Handoff { get; private set; }
        // Player-facing reason; set in Failed, empty otherwise.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public string FailureMessage { get; private set; } = string.Empty;
        // Last rating the platform reported for this wait; zero before the first poll.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public double Rating { get; private set; }

        // Seconds since Start while Queued, else zero.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public double ElapsedSeconds => State == MatchmakingState.Queued ? _clock() - _startedAt : 0.0;

        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public bool IsFinished => State == MatchmakingState.Matched || State == MatchmakingState.Cancelled ||
                                  State == MatchmakingState.Failed;

        // Begins a search from Idle or any finished state; false while one is already running.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public bool Start()
        {
            if (State == MatchmakingState.Queued) return false;
            State = MatchmakingState.Queued;
            Handoff = default;
            FailureMessage = string.Empty;
            Rating = 0.0;
            _startedAt = _clock();
            _nextPollAt = _startedAt;
            _inFlight = false;
            _errors = 0;
            return true;
        }

        // True when the caller should send the next request now; at most one is outstanding.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public bool IsPollDue() =>
            State == MatchmakingState.Queued && !_inFlight && _clock() >= _nextPollAt;

        // The caller sent a request; no other is due until its answer arrives.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public void MarkRequestSent()
        {
            if (State == MatchmakingState.Queued) _inFlight = true;
        }

        // Fails the search once it has waited past the timeout; true if this call timed it out.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public bool CheckTimeout()
        {
            if (State != MatchmakingState.Queued || _options.TimeoutSeconds <= 0.0) return false;
            if (_clock() - _startedAt < _options.TimeoutSeconds) return false;
            Fail("No opponent found in time. Try again.");
            return true;
        }

        // The player backed out; false when there is no search to cancel.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public bool Cancel()
        {
            if (State != MatchmakingState.Queued) return false;
            State = MatchmakingState.Cancelled;
            _inFlight = false;
            return true;
        }

        // A queued status arrived: stay Queued, clear the error streak, schedule the next poll.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public void OnQueued(double rating)
        {
            if (State != MatchmakingState.Queued) return;
            Rating = rating;
            _errors = 0;
            ScheduleNextPoll();
        }

        // The platform paired us: a good handoff is Matched, a bad one Failed.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public void OnMatched(ConfigResult<MatchHandoff> handoff)
        {
            if (State != MatchmakingState.Queued) return;
            _inFlight = false;
            if (handoff.IsOk)
            {
                Handoff = handoff.Value;
                State = MatchmakingState.Matched;
            }
            else
            {
                Fail("The match could not be started: " + handoff.Error + ".");
            }
        }

        // The platform says we are not queued (it restarted, or the match expired).
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public void OnIdle()
        {
            if (State == MatchmakingState.Queued) Fail("You left the queue. Try again.");
        }

        // A request failed; transient ones are retried up to the limit, others end the search.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public void OnError(string message, bool retryable)
        {
            if (State != MatchmakingState.Queued) return;
            _errors++;
            if (!retryable || _errors >= _options.MaxConsecutiveErrors)
            {
                Fail(message);
                return;
            }
            ScheduleNextPoll();
        }

        void ScheduleNextPoll()
        {
            _inFlight = false;
            _nextPollAt = _clock() + _options.PollIntervalSeconds;
        }

        void Fail(string message)
        {
            State = MatchmakingState.Failed;
            FailureMessage = message ?? string.Empty;
            _inFlight = false;
        }
    }
}
