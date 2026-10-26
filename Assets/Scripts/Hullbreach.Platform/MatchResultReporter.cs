using System;
using System.Collections.Generic;

namespace Hullbreach.Platform
{
    // Sends finished matches to the platform and resends transient failures on a
    // backoff. Engine-free: the server system feeds it the clock through Tick.
    // frob:doc docs/platform-link.md#reporting-a-finished-match
    public sealed class MatchResultReporter
    {
        // Seconds to wait after the 1st, 2nd, ... failed attempt; one attempt more than delays.
        // frob:doc docs/platform-link.md#reporting-a-finished-match
        public static readonly double[] RetryDelaysSeconds = { 2, 5, 15, 30, 60, 120 };

        sealed class Pending
        {
            public MatchResultReport Report;
            public int Attempts;
            public bool InFlight;
            public double NextAttemptAt;
        }

        readonly PlatformClient _client;
        readonly PlatformAdmissionGate _gate;
        readonly IPlatformLog _log;
        readonly List<Pending> _pending = new List<Pending>();
        readonly Queue<MatchReportOutcome> _finished = new Queue<MatchReportOutcome>();

        // frob:doc docs/platform-link.md#reporting-a-finished-match
        public MatchResultReporter(PlatformClient client, PlatformAdmissionGate gate, IPlatformLog log = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
            _log = log ?? NullPlatformLog.Instance;
        }

        // True once a game server key is configured: the platform only takes reports with one.
        // frob:doc docs/platform-link.md#reporting-a-finished-match
        public bool Enabled => _client.Config.HasServerKey;

        // Reports waiting for an answer or a retry.
        // frob:doc docs/platform-link.md#reporting-a-finished-match
        public int PendingCount => _pending.Count;

        // Queues a finished match between two connected peers. A no-op (false) when the server
        // has no key or either peer is a guest, since only platform accounts have ratings.
        // frob:doc docs/platform-link.md#reporting-a-finished-match
        public bool ReportFinished(string matchId, int winnerPeer, int peerA, PlayerMatchStats statsA, int peerB,
            PlayerMatchStats statsB, int durationSeconds, double now)
        {
            if (!Enabled)
            {
                _log.Info($"Hullbreach platform: match {matchId} not reported: no game server key configured.");
                return false;
            }
            if (!_gate.TryGetUserId(peerA, out string userA) || !_gate.TryGetUserId(peerB, out string userB))
            {
                _log.Info($"Hullbreach platform: match {matchId} not reported: a player is a guest.");
                return false;
            }
            if (winnerPeer != peerA && winnerPeer != peerB)
            {
                _log.Warning($"Hullbreach platform: match {matchId} not reported: winner peer {winnerPeer} is not a player.");
                return false;
            }

            var report = new MatchResultReport(matchId, winnerPeer == peerA ? userA : userB, durationSeconds,
                new[] { With(statsA, userA), With(statsB, userB) });
            return Submit(report, now);
        }

        // Reports every match the feed announces until the returned handle is disposed.
        // The clock is read at each match end so retries keep the server's time base.
        // frob:doc docs/platform-link.md#reporting-a-finished-match
        public IDisposable Attach(MatchEndFeed feed, Func<double> clock)
        {
            if (feed == null) throw new ArgumentNullException(nameof(feed));
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            Action<MatchEndedArgs> handler = args =>
            {
                _log.Info($"Hullbreach platform: match {args.MatchId} ended; reporting it.");
                ReportFinished(args.MatchId, args.WinnerPeer, args.PeerA, args.StatsA, args.PeerB, args.StatsB,
                    args.DurationSeconds, clock());
            };
            feed.Ended += handler;
            return new Detach(() => feed.Ended -= handler);
        }

        // Queues a ready report; false when it is invalid or the server is unconfigured.
        // frob:doc docs/platform-link.md#reporting-a-finished-match
        public bool Submit(MatchResultReport report, double now)
        {
            if (!Enabled) return false;
            string invalid = report.Validate();
            if (invalid != null)
            {
                _log.Warning($"Hullbreach platform: match {report.MatchId} not reported: {invalid}.");
                return false;
            }
            _log.Info($"Hullbreach platform: match {report.MatchId} queued for reporting.");
            _pending.Add(new Pending { Report = report, NextAttemptAt = now });
            Tick(now);
            return true;
        }

        // Starts due attempts; call once per server update.
        // frob:doc docs/platform-link.md#reporting-a-finished-match
        public void Tick(double now)
        {
            // Iterate a copy: a synchronous completion removes entries.
            foreach (Pending item in _pending.ToArray())
            {
                if (item.InFlight || now < item.NextAttemptAt) continue;
                item.InFlight = true;
                item.Attempts++;
                _client.PostMatchResult(item.Report, outcome => OnAnswer(item, outcome, now));
            }
        }

        // Hands out each settled report once, for the system to count or surface.
        // frob:doc docs/platform-link.md#reporting-a-finished-match
        public bool TryTakeFinished(out MatchReportOutcome outcome)
        {
            if (_finished.Count > 0)
            {
                outcome = _finished.Dequeue();
                return true;
            }
            outcome = default;
            return false;
        }

        void OnAnswer(Pending item, MatchReportOutcome outcome, double now)
        {
            item.InFlight = false;
            if (outcome.IsRetryable && item.Attempts <= RetryDelaysSeconds.Length)
            {
                double delay = RetryDelaysSeconds[item.Attempts - 1];
                // Measured from when the attempt started, which is the clock the system last gave us.
                item.NextAttemptAt = now + delay;
                _log.Info($"Hullbreach platform: match {item.Report.MatchId} will be resent in {delay}s (attempt {item.Attempts} failed).");
                return;
            }
            if (outcome.IsRetryable)
                _log.Warning($"Hullbreach platform: giving up on match {item.Report.MatchId} after {item.Attempts} attempts.");
            _pending.Remove(item);
            _finished.Enqueue(outcome);
        }

        sealed class Detach : IDisposable
        {
            Action _undo;
            public Detach(Action undo) => _undo = undo;

            public void Dispose()
            {
                _undo?.Invoke();
                _undo = null;
            }
        }

        static PlayerMatchStats With(PlayerMatchStats stats, string userId) =>
            new PlayerMatchStats(userId, stats.DamageDealt, stats.BlocksDestroyed, stats.BlocksPlaced,
                stats.TimeAliveSeconds);
    }
}
