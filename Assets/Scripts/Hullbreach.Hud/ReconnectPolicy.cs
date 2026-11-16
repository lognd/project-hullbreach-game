using System;
using System.Globalization;

namespace Hullbreach.Hud
{
    // What the view should do this frame about a dropped connection.
    // frob:ticket 01M7FJ5KYZDQ37M8MC4CEV1DG7
    public enum ReconnectAction
    {
        None,    // nothing to do (healthy, waiting between attempts, or not allowed to retry)
        Attempt, // dial the host again now
    }

    // Where the retry loop stands, for the banner.
    // frob:ticket 01M7FJ5KYZDQ37M8MC4CEV1DG7
    public enum ReconnectPhase
    {
        Idle,     // connected, never connected yet, or retrying is not allowed
        Waiting,  // between attempts (backing off)
        Dialing,  // an attempt is in flight
        GaveUp,   // every attempt failed
    }

    // Engine-free client reconnect policy (S45): after a connection that had worked drops, retry on a backoff schedule
    // up to a cap, then give up with a message. It never retries after a deliberate leave or once the match is over.
    // The view feeds it the clock and the link state each frame and dials when told to; it holds no engine types.
    // frob:ticket 01M7FJ5KYZDQ37M8MC4CEV1DG7
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public sealed class ReconnectPolicy
    {
        public const int DefaultMaxAttempts = 6;
        public const double DefaultInitialDelaySeconds = 1.0;
        public const double DefaultMaxDelaySeconds = 8.0;
        public const double DefaultAttemptTimeoutSeconds = 5.0;

        readonly double _initialDelay;
        readonly double _maxDelay;
        readonly double _attemptTimeout;

        bool _everConnected;
        bool _blocked;
        double _nextAt;
        double _deadline;

        public int MaxAttempts { get; }
        public ReconnectPhase Phase { get; private set; }
        // Attempts started since the link dropped.
        public int Attempts { get; private set; }

        public ReconnectPolicy(int maxAttempts = DefaultMaxAttempts, double initialDelaySeconds = DefaultInitialDelaySeconds,
            double maxDelaySeconds = DefaultMaxDelaySeconds, double attemptTimeoutSeconds = DefaultAttemptTimeoutSeconds)
        {
            if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
            if (initialDelaySeconds < 0 || maxDelaySeconds < initialDelaySeconds || attemptTimeoutSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(initialDelaySeconds));
            MaxAttempts = maxAttempts;
            _initialDelay = initialDelaySeconds;
            _maxDelay = maxDelaySeconds;
            _attemptTimeout = attemptTimeoutSeconds;
        }

        // Wait before attempt n (1-based): the initial delay doubled each time, never above the cap.
        public double DelayBefore(int attempt)
        {
            double d = _initialDelay;
            for (int i = 1; i < attempt && d < _maxDelay; i++) d *= 2.0;
            return Math.Min(d, _maxDelay);
        }

        // The player chose to go back to the lobby: never rejoin on their behalf (until a new match resets it).
        public void NoteDeliberateLeave() => Block();

        // Permanent for this policy; the view builds a fresh one per match scene.
        void Block()
        {
            _blocked = true;
            Phase = ReconnectPhase.Idle;
        }

        // Call each frame. `matchOver` is the results screen being up, so a closing server is normal and not retried.
        // Returns Attempt on the frame a new dial should start.
        public ReconnectAction Update(double now, bool connected, bool matchOver)
        {
            if (matchOver) Block();
            if (connected)
            {
                _everConnected = true;
                Attempts = 0;
                if (!_blocked) Phase = ReconnectPhase.Idle;
                return ReconnectAction.None;
            }
            if (_blocked || !_everConnected || Phase == ReconnectPhase.GaveUp) return ReconnectAction.None;

            if (Phase == ReconnectPhase.Idle)
            {
                // The link just dropped: first attempt after the initial delay.
                Phase = ReconnectPhase.Waiting;
                _nextAt = now + DelayBefore(1);
            }
            if (Phase == ReconnectPhase.Dialing && now >= _deadline)
            {
                if (Attempts >= MaxAttempts)
                {
                    Phase = ReconnectPhase.GaveUp;
                    return ReconnectAction.None;
                }
                Phase = ReconnectPhase.Waiting;
                _nextAt = now + DelayBefore(Attempts + 1);
            }
            if (Phase == ReconnectPhase.Waiting && now >= _nextAt)
            {
                Attempts++;
                Phase = ReconnectPhase.Dialing;
                _deadline = now + _attemptTimeout;
                return ReconnectAction.Attempt;
            }
            return ReconnectAction.None;
        }

        // The attempt number to show: the one in flight, or the one coming next while backing off.
        public int DisplayAttempt => Phase == ReconnectPhase.Dialing ? Attempts : Math.Min(Attempts + 1, MaxAttempts);

        // "Reconnecting... (2/6)"; empty unless a retry is in progress.
        public string StatusText => Phase == ReconnectPhase.Waiting || Phase == ReconnectPhase.Dialing
            ? "Reconnecting... (" + DisplayAttempt.ToString(CultureInfo.InvariantCulture) + "/"
                + MaxAttempts.ToString(CultureInfo.InvariantCulture) + ")"
            : string.Empty;

        // Shown once every attempt has failed.
        public string GiveUpText => "Could not reconnect after " + MaxAttempts.ToString(CultureInfo.InvariantCulture)
            + " attempts. The match may have ended, or the host is gone.";
    }
}
