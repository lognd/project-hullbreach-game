using System.Collections.Generic;
using Hullbreach.Platform;
using NUnit.Framework;

namespace Hullbreach.Platform.Tests
{
    // Report validation and JSON, PlatformClient.PostMatchResult, and the retrying reporter.
    public sealed class MatchResultReportTests
    {
        const string Key = "key-SECRET-789";
        const string Match = "0b0c1d2e-1111-4222-8333-444455556666";
        const string UserA = "aaaaaaaa-0000-4000-8000-000000000001";
        const string UserB = "bbbbbbbb-0000-4000-8000-000000000002";
        const string Token = "tok-SECRET-abc";

        FakePlatformHttp _http;
        RecordingPlatformLog _log;

        [SetUp]
        public void SetUp()
        {
            _http = new FakePlatformHttp();
            _log = new RecordingPlatformLog();
        }

        [TearDown]
        public void NoSecretsLogged() => StringAssert.DoesNotContain("SECRET", _log.All);

        PlatformClient Client(string key = Key) =>
            new PlatformClient(new PlatformConfig("http://platform.test:8002/", 3, false, key), _http, _log);

        static MatchResultReport Report(string winner = UserA, int duration = 240, string idB = UserB) =>
            new MatchResultReport(Match, winner, duration, new[]
            {
                new PlayerMatchStats(UserA, 900, 14, 30, 240),
                new PlayerMatchStats(idB, 400, 12, 28, 200),
            });

        MatchReportOutcome Post(MatchResultReport report, PlatformHttpResponse response)
        {
            _http.Enqueue(response);
            MatchReportOutcome result = default;
            Client().PostMatchResult(report, o => result = o);
            return result;
        }

        [Test]
        public void ToJson_MatchesThePlatformContract()
        {
            Assert.IsNull(Report().Validate());
            Assert.AreEqual(
                "{\"match_id\":\"" + Match + "\",\"winner_id\":\"" + UserA + "\",\"duration_seconds\":240,\"players\":[" +
                "{\"user_id\":\"" + UserA + "\",\"damage_dealt\":900,\"blocks_destroyed\":14,\"blocks_placed\":30,\"time_alive_seconds\":240}," +
                "{\"user_id\":\"" + UserB + "\",\"damage_dealt\":400,\"blocks_destroyed\":12,\"blocks_placed\":28,\"time_alive_seconds\":200}]}",
                Report().ToJson());
        }

        [Test]
        public void Validate_UppercaseIdsAreSentLowercase()
        {
            var report = new MatchResultReport(Match.ToUpperInvariant(), UserA.ToUpperInvariant(), 1, new[]
            {
                new PlayerMatchStats(UserA.ToUpperInvariant(), 0, 0, 0, 0),
                new PlayerMatchStats(UserB, 0, 0, 0, 0),
            });
            Assert.IsNull(report.Validate());
            StringAssert.Contains("\"match_id\":\"" + Match + "\"", report.ToJson());
        }

        [Test]
        public void Validate_RejectsWhatThePlatformWould422()
        {
            Assert.IsNotNull(new MatchResultReport("nope", UserA, 1, Report().Players).Validate());
            Assert.IsNotNull(Report(winner: "nope").Validate());
            Assert.IsNotNull(Report(duration: -1).Validate());
            Assert.IsNotNull(Report(winner: "cccccccc-0000-4000-8000-000000000003").Validate());
            Assert.IsNotNull(Report(idB: UserA).Validate());
            Assert.IsNotNull(new MatchResultReport(Match, UserA, 1, new[] { new PlayerMatchStats(UserA, 0, 0, 0, 0) }).Validate());
            var negative = new MatchResultReport(Match, UserA, 1, new[]
            {
                new PlayerMatchStats(UserA, -1, 0, 0, 0), new PlayerMatchStats(UserB, 0, 0, 0, 0),
            });
            Assert.IsNotNull(negative.Validate());
        }

        [Test]
        public void Post_SendsJsonWithServerKeyToMatchesPath()
        {
            var outcome = Post(Report(), PlatformHttpResponse.Http(201, "{}"));
            var request = _http.Requests[0];
            Assert.AreEqual(PlatformHttpMethod.Post, request.Method);
            Assert.AreEqual("http://platform.test:8002/api/v1/matches", request.Url);
            Assert.AreEqual(Key, request.Header("X-Server-Key"));
            Assert.IsNull(request.Header("Authorization"));
            Assert.AreEqual(Report().ToJson(), request.Body);
            Assert.AreEqual(3.0, request.TimeoutSeconds);
            Assert.AreEqual(MatchReportStatus.Recorded, outcome.Status);
            Assert.AreEqual(Match, outcome.MatchId);
        }

        [TestCase(201, MatchReportStatus.Recorded, true, false)]
        [TestCase(200, MatchReportStatus.AlreadyRecorded, true, false)]
        [TestCase(404, MatchReportStatus.UnknownPlayer, false, false)]
        [TestCase(409, MatchReportStatus.Conflict, false, false)]
        [TestCase(401, MatchReportStatus.Rejected, false, false)]
        [TestCase(422, MatchReportStatus.Rejected, false, false)]
        [TestCase(503, MatchReportStatus.ServerError, false, true)]
        [TestCase(500, MatchReportStatus.ServerError, false, true)]
        [TestCase(418, MatchReportStatus.Rejected, false, false)]
        public void Post_MapsEveryStatus(int code, MatchReportStatus status, bool stored, bool retryable)
        {
            var outcome = Post(Report(), PlatformHttpResponse.Http(code, "{\"detail\":\"why\",\"field\":\"x\"}"));
            Assert.AreEqual(status, outcome.Status);
            Assert.AreEqual(stored, outcome.IsStored);
            Assert.AreEqual(retryable, outcome.IsRetryable);
        }

        [Test]
        public void Post_TransportFailureIsRetryableUnreachable()
        {
            var outcome = Post(Report(), PlatformHttpResponse.Transport("timeout"));
            Assert.AreEqual(MatchReportStatus.Unreachable, outcome.Status);
            Assert.IsTrue(outcome.IsRetryable);
        }

        [Test]
        public void Post_InvalidReportOrMissingKeyNeverHitsTheNetwork()
        {
            var invalid = Post(Report(duration: -5), PlatformHttpResponse.Http(201, "{}"));
            Assert.AreEqual(MatchReportStatus.InvalidReport, invalid.Status);
            MatchReportOutcome keyless = default;
            Client(key: null).PostMatchResult(Report(), o => keyless = o);
            Assert.AreEqual(MatchReportStatus.Rejected, keyless.Status);
            Assert.AreEqual(0, _http.Requests.Count);
        }

        PlatformAdmissionGate Gate(PlatformClient client, bool peer1Verified = true, bool peer2Verified = true)
        {
            var gate = new PlatformAdmissionGate(client, false, 10, _log);
            gate.OnConnected(1, 0);
            gate.OnConnected(2, 0);
            if (peer1Verified) Verify(gate, 1, UserA);
            if (peer2Verified) Verify(gate, 2, UserB);
            return gate;
        }

        void Verify(PlatformAdmissionGate gate, int peer, string userId)
        {
            _http.Enqueue(PlatformHttpResponse.Http(200, "{\"user_id\":\"" + userId + "\",\"role\":\"player\"}"));
            gate.OnHello(peer, Token, "p" + peer, 0);
        }

        bool Finish(MatchResultReporter reporter, double now, int winner = 2) =>
            reporter.ReportFinished(Match, winner, 1, new PlayerMatchStats(null, 900, 14, 30, 240), 2,
                new PlayerMatchStats(null, 400, 12, 28, 200), 240, now);

        [Test]
        public void Gate_ExposesUserIdOnlyForVerifiedPeers()
        {
            PlatformAdmissionGate gate = Gate(Client(), peer2Verified: false);
            Assert.IsTrue(gate.TryGetUserId(1, out string id));
            Assert.AreEqual(UserA, id);
            Assert.IsFalse(gate.TryGetUserId(2, out _));
            Assert.IsFalse(gate.TryGetUserId(9, out _));
        }

        [Test]
        public void Reporter_SendsOnceWithTheWinnersAccountId()
        {
            PlatformClient client = Client();
            var reporter = new MatchResultReporter(client, Gate(client), _log);
            int baseline = _http.Requests.Count;
            _http.Enqueue(PlatformHttpResponse.Http(201, "{}"));
            Assert.IsTrue(Finish(reporter, 10, winner: 2));
            Assert.AreEqual(baseline + 1, _http.Requests.Count);
            StringAssert.Contains("\"winner_id\":\"" + UserB + "\"", _http.Requests[baseline].Body);
            Assert.AreEqual(0, reporter.PendingCount);
            Assert.IsTrue(reporter.TryTakeFinished(out var outcome));
            Assert.AreEqual(MatchReportStatus.Recorded, outcome.Status);
        }

        MatchEndedArgs Ended(int winner = 1) => new MatchEndedArgs(Match, winner, 1,
            new PlayerMatchStats(null, 900, 14, 30, 240), 2, new PlayerMatchStats(null, 400, 12, 28, 200), 240);

        [Test]
        public void Feed_MatchEndPostsOneReportUsingTheClock()
        {
            PlatformClient client = Client();
            var reporter = new MatchResultReporter(client, Gate(client), _log);
            var feed = new MatchEndFeed();
            double clock = 42;
            using (reporter.Attach(feed, () => clock))
            {
                int baseline = _http.Requests.Count;
                _http.Enqueue(PlatformHttpResponse.Http(503, "{}"));
                Assert.AreEqual(1, feed.Raise(Ended()));
                Assert.AreEqual(baseline + 1, _http.Requests.Count);
                StringAssert.Contains("\"winner_id\":\"" + UserA + "\"", _http.Requests[baseline].Body);
                Assert.AreEqual(1, reporter.PendingCount, "503 stays queued for the backoff");
                // Backoff is measured from the clock the feed handler read (42 + 2s).
                reporter.Tick(43);
                Assert.AreEqual(baseline + 1, _http.Requests.Count);
            }
        }

        [Test]
        public void Feed_DisposedSubscriptionAndGuestsSendNothing()
        {
            PlatformClient client = Client();
            var feed = new MatchEndFeed();
            Assert.AreEqual(0, feed.Raise(Ended()), "no subscriber");

            var reporter = new MatchResultReporter(client, Gate(client), _log);
            reporter.Attach(feed, () => 0).Dispose();
            Assert.AreEqual(0, feed.Raise(Ended()), "unsubscribed");

            var guests = new MatchResultReporter(client, Gate(client, peer2Verified: false), _log);
            using (guests.Attach(feed, () => 0))
                Assert.AreEqual(1, feed.Raise(Ended()));
            Assert.AreEqual(0, _http.Requests.FindAll(r => r.Url.EndsWith("/matches")).Count);
            Assert.Throws<System.ArgumentNullException>(() => reporter.Attach(null, () => 0));
        }

        [Test]
        public void Reporter_IsANoOpForGuestsAndUnconfiguredServers()
        {
            PlatformClient client = Client();
            var guestReporter = new MatchResultReporter(client, Gate(client, peer2Verified: false), _log);
            Assert.IsFalse(Finish(guestReporter, 0));

            PlatformClient keyless = Client(key: null);
            var unconfigured = new MatchResultReporter(keyless, Gate(keyless), _log);
            Assert.IsFalse(unconfigured.Enabled);
            Assert.IsFalse(Finish(unconfigured, 0));

            Assert.IsFalse(Finish(new MatchResultReporter(client, Gate(client), _log), 0, winner: 7));
            Assert.AreEqual(0, _http.Requests.FindAll(r => r.Url.EndsWith("/matches")).Count, "no report was sent");
        }

        [Test]
        public void Reporter_ResendsTheIdenticalBodyAfterBackoffThenStops()
        {
            PlatformClient client = Client();
            var reporter = new MatchResultReporter(client, Gate(client), _log);
            int baseline = _http.Requests.Count;
            _http.Enqueue(PlatformHttpResponse.Transport("down"));
            _http.Enqueue(PlatformHttpResponse.Http(503, "{\"detail\":\"busy\"}"));
            _http.Enqueue(PlatformHttpResponse.Http(200, "{}"));
            Finish(reporter, 0);
            Assert.AreEqual(1, reporter.PendingCount);

            reporter.Tick(1.9);
            Assert.AreEqual(baseline + 1, _http.Requests.Count, "first retry waits 2s");
            reporter.Tick(2.0);
            Assert.AreEqual(baseline + 2, _http.Requests.Count);
            reporter.Tick(6.9);
            Assert.AreEqual(baseline + 2, _http.Requests.Count, "second retry waits 5s");
            reporter.Tick(7.0);
            Assert.AreEqual(baseline + 3, _http.Requests.Count);
            Assert.AreEqual(_http.Requests[baseline].Body, _http.Requests[baseline + 2].Body);
            Assert.AreEqual(0, reporter.PendingCount);
            Assert.IsTrue(reporter.TryTakeFinished(out var outcome));
            Assert.AreEqual(MatchReportStatus.AlreadyRecorded, outcome.Status);
        }

        [Test]
        public void Reporter_GivesUpAfterTheLastDelayAndNeverRetriesAPermanentError()
        {
            PlatformClient client = Client();
            var reporter = new MatchResultReporter(client, Gate(client), _log);
            int baseline = _http.Requests.Count;
            Finish(reporter, 0);
            double now = 0;
            for (int i = 0; i < 20; i++)
            {
                now += 1000;
                reporter.Tick(now);
            }
            Assert.AreEqual(MatchResultReporter.RetryDelaysSeconds.Length + 1, _http.Requests.Count - baseline);
            Assert.AreEqual(0, reporter.PendingCount);
            Assert.IsTrue(reporter.TryTakeFinished(out var gaveUp));
            Assert.IsTrue(gaveUp.IsRetryable);

            baseline = _http.Requests.Count;
            _http.Enqueue(PlatformHttpResponse.Http(409, "{\"detail\":\"clash\",\"field\":\"match_id\"}"));
            Finish(reporter, now);
            reporter.Tick(now + 100000);
            Assert.AreEqual(1, _http.Requests.Count - baseline);
            Assert.IsTrue(reporter.TryTakeFinished(out var conflict));
            Assert.AreEqual(MatchReportStatus.Conflict, conflict.Status);
        }
    }
}
