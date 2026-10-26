using System;
using System.Collections.Generic;
using Hullbreach.Net;
using Hullbreach.Platform;
using NUnit.Framework;

namespace Hullbreach.Net.Tests
{
    // Pins the matchmaking client, state machine, session and handoff parsing (S29-2, S29-3).
    public class MatchmakingTests
    {
        const string Token = "tok-SECRET-123";
        const string MatchId = "6f1c1c1e-0a53-4d2e-9d57-3f2a6a8d9b10";

        sealed class FakeHttp : IPlatformHttp
        {
            public readonly List<PlatformHttpRequest> Requests = new List<PlatformHttpRequest>();
            public readonly Queue<PlatformHttpResponse> Scripted = new Queue<PlatformHttpResponse>();

            public void Send(PlatformHttpRequest request, Action<PlatformHttpResponse> onComplete)
            {
                Requests.Add(request);
                onComplete(Scripted.Count > 0 ? Scripted.Dequeue() : PlatformHttpResponse.Http(204, ""));
            }
        }

        sealed class Log : IPlatformLog
        {
            public readonly List<string> Lines = new List<string>();
            public void Info(string message) => Lines.Add(message);
            public void Warning(string message) => Lines.Add(message);
        }

        static string Queued(int waited = 3) =>
            "{\"state\":\"queued\",\"rating\":1250,\"waited_seconds\":" + waited + ",\"window\":150,\"match\":null}";

        static string Matched(string address = "10.0.0.5:7777") =>
            "{\"state\":\"matched\",\"rating\":null,\"waited_seconds\":null,\"window\":null,\"match\":{\"match_id\":\"" +
            MatchId + "\",\"server_address\":\"" + address + "\",\"opponent_id\":\"u2\"}}";

        static PlatformConfig Config() => new PlatformConfig("http://platform.test:8002", 3, false, null);

        // --- handoff ---

        [Test]
        public void Handoff_ParsesHostAndPort()
        {
            var r = MatchHandoff.Parse(MatchId, " 127.0.0.1:7777 ", "u2");
            Assert.IsTrue(r.IsOk, r.Error);
            Assert.AreEqual(MatchId, r.Value.MatchId);
            Assert.AreEqual("127.0.0.1", r.Value.Host);
            Assert.AreEqual(7777, r.Value.Port);
            Assert.AreEqual("u2", r.Value.OpponentId);
        }

        [TestCase(null, "h:1")]
        [TestCase("", "h:1")]
        [TestCase("id", null)]
        [TestCase("id", "")]
        [TestCase("id", "nohost")]
        [TestCase("id", ":7777")]
        [TestCase("id", "host:")]
        [TestCase("id", "host:0")]
        [TestCase("id", "host:65536")]
        [TestCase("id", "host:-1")]
        [TestCase("id", "host:abc")]
        [TestCase("id", "a:b:7777")]
        [TestCase("id", "ho st:7777")]
        public void Handoff_BadInput_IsErrorNotThrow(string id, string address)
        {
            var r = MatchHandoff.Parse(id, address);
            Assert.IsFalse(r.IsOk);
            Assert.IsNotEmpty(r.Error);
        }

        // --- client ---

        [Test]
        public void Client_RequestShape_ForJoinPollCancel()
        {
            var http = new FakeHttp();
            var client = new MatchmakingClient(Config(), http);
            http.Scripted.Enqueue(PlatformHttpResponse.Http(200, Queued()));
            http.Scripted.Enqueue(PlatformHttpResponse.Http(200, Queued()));
            http.Scripted.Enqueue(PlatformHttpResponse.Http(204, ""));
            client.Join(Token, _ => { });
            client.Poll(Token, _ => { });
            QueueOutcome cancel = default;
            client.Cancel(Token, o => cancel = o);

            Assert.AreEqual(PlatformHttpMethod.Post, http.Requests[0].Method);
            Assert.AreEqual(PlatformHttpMethod.Get, http.Requests[1].Method);
            Assert.AreEqual(PlatformHttpMethod.Delete, http.Requests[2].Method);
            foreach (var request in http.Requests)
            {
                Assert.AreEqual("http://platform.test:8002/api/v1/queue", request.Url);
                Assert.AreEqual("Bearer " + Token, request.Header("Authorization"));
                Assert.IsNull(request.Body);
            }
            Assert.IsTrue(cancel.IsOk);
        }

        [Test]
        public void Client_NoToken_IsUnauthorizedWithoutSending()
        {
            var http = new FakeHttp();
            QueueOutcome got = default;
            new MatchmakingClient(Config(), http).Join("", o => got = o);
            Assert.AreEqual(QueueCallStatus.Unauthorized, got.Status);
            Assert.AreEqual(0, http.Requests.Count);
        }

        [Test]
        public void Map_QueuedStatus_ParsesFields()
        {
            var o = MatchmakingClient.Map(PlatformHttpMethod.Get, PlatformHttpResponse.Http(200, Queued(7)));
            Assert.IsTrue(o.IsOk);
            Assert.AreEqual(QueueWireState.Queued, o.Queue.State);
            Assert.AreEqual(1250.0, o.Queue.Rating);
            Assert.AreEqual(7.0, o.Queue.WaitedSeconds);
            Assert.AreEqual(150.0, o.Queue.Window);
        }

        [Test]
        public void Map_MatchedStatus_CarriesHandoff()
        {
            var o = MatchmakingClient.Map(PlatformHttpMethod.Get, PlatformHttpResponse.Http(200, Matched()));
            Assert.AreEqual(QueueWireState.Matched, o.Queue.State);
            Assert.IsTrue(o.Queue.Match.IsOk, o.Queue.Match.Error);
            Assert.AreEqual("10.0.0.5", o.Queue.Match.Value.Host);
            Assert.AreEqual(7777, o.Queue.Match.Value.Port);
        }

        [Test]
        public void Map_MatchedWithBadAddress_KeepsOkStatusButFailedHandoff()
        {
            var o = MatchmakingClient.Map(PlatformHttpMethod.Get, PlatformHttpResponse.Http(200, Matched("garbage")));
            Assert.IsTrue(o.IsOk);
            Assert.IsFalse(o.Queue.Match.IsOk);
        }

        [TestCase(401, QueueCallStatus.Unauthorized)]
        [TestCase(422, QueueCallStatus.Rejected)]
        [TestCase(404, QueueCallStatus.Rejected)]
        [TestCase(500, QueueCallStatus.ServerError)]
        [TestCase(503, QueueCallStatus.ServerError)]
        public void Map_ErrorStatuses(int code, QueueCallStatus expected)
        {
            var o = MatchmakingClient.Map(PlatformHttpMethod.Get, PlatformHttpResponse.Http(code, "{}"));
            Assert.AreEqual(expected, o.Status);
            Assert.IsFalse(o.IsOk);
        }

        [Test]
        public void Map_Conflict_AlreadyMatchedOnlyWithKind()
        {
            var yes = MatchmakingClient.Map(PlatformHttpMethod.Post,
                PlatformHttpResponse.Http(409, "{\"kind\":\"already_matched\",\"detail\":\"x\"}"));
            var other = MatchmakingClient.Map(PlatformHttpMethod.Post, PlatformHttpResponse.Http(409, "{}"));
            Assert.AreEqual(QueueCallStatus.AlreadyMatched, yes.Status);
            Assert.AreEqual(QueueCallStatus.Rejected, other.Status);
        }

        [Test]
        public void Map_TransportFailure_IsRetryable()
        {
            var o = MatchmakingClient.Map(PlatformHttpMethod.Get, PlatformHttpResponse.Transport("timeout"));
            Assert.AreEqual(QueueCallStatus.Unreachable, o.Status);
            Assert.IsTrue(o.IsRetryable);
        }

        [TestCase("not json")]
        [TestCase("[]")]
        [TestCase("{\"state\":\"weird\"}")]
        [TestCase("{}")]
        public void Map_UnreadableBody_IsBadResponse(string body)
        {
            var o = MatchmakingClient.Map(PlatformHttpMethod.Get, PlatformHttpResponse.Http(200, body));
            Assert.AreEqual(QueueCallStatus.BadResponse, o.Status);
        }

        // --- machine ---

        sealed class Clock
        {
            public double Now;
            public Func<double> Fn => () => Now;
        }

        static MatchmakingMachine Machine(Clock clock, double timeout = 60, int maxErrors = 3) =>
            new MatchmakingMachine(clock.Fn, new MatchmakingOptions(1.0, timeout, maxErrors));

        [Test]
        public void Machine_StartsIdle_AndPollsImmediatelyThenEverySecond()
        {
            var clock = new Clock();
            var m = Machine(clock);
            Assert.AreEqual(MatchmakingState.Idle, m.State);
            Assert.IsFalse(m.IsPollDue());
            Assert.IsTrue(m.Start());
            Assert.IsFalse(m.Start());
            Assert.IsTrue(m.IsPollDue());
            m.MarkRequestSent();
            Assert.IsFalse(m.IsPollDue());
            m.OnQueued(1200);
            Assert.IsFalse(m.IsPollDue());
            clock.Now = 0.99;
            Assert.IsFalse(m.IsPollDue());
            clock.Now = 1.0;
            Assert.IsTrue(m.IsPollDue());
            Assert.AreEqual(1200.0, m.Rating);
        }

        [Test]
        public void Machine_Timeout_FailsWithMessage()
        {
            var clock = new Clock();
            var m = Machine(clock, timeout: 10);
            m.Start();
            clock.Now = 9.9;
            Assert.IsFalse(m.CheckTimeout());
            clock.Now = 10;
            Assert.IsTrue(m.CheckTimeout());
            Assert.AreEqual(MatchmakingState.Failed, m.State);
            Assert.IsNotEmpty(m.FailureMessage);
            Assert.IsFalse(m.CheckTimeout());
        }

        [Test]
        public void Machine_ZeroTimeout_WaitsForever()
        {
            var clock = new Clock();
            var m = Machine(clock, timeout: 0);
            m.Start();
            clock.Now = 1e9;
            Assert.IsFalse(m.CheckTimeout());
            Assert.AreEqual(MatchmakingState.Queued, m.State);
        }

        [Test]
        public void Machine_Matched_AndBadHandoffFails()
        {
            var good = Machine(new Clock());
            good.Start();
            good.OnMatched(MatchHandoff.Parse(MatchId, "h:1"));
            Assert.AreEqual(MatchmakingState.Matched, good.State);
            Assert.AreEqual(MatchId, good.Handoff.MatchId);

            var bad = Machine(new Clock());
            bad.Start();
            bad.OnMatched(MatchHandoff.Parse(MatchId, "nope"));
            Assert.AreEqual(MatchmakingState.Failed, bad.State);
        }

        [Test]
        public void Machine_TransientErrors_ToleratedUntilLimit_ThenSuccessResets()
        {
            var m = Machine(new Clock(), maxErrors: 3);
            m.Start();
            m.OnError("e", true);
            m.OnError("e", true);
            Assert.AreEqual(MatchmakingState.Queued, m.State);
            m.OnQueued(1200);
            m.OnError("e", true);
            m.OnError("e", true);
            Assert.AreEqual(MatchmakingState.Queued, m.State);
            m.OnError("boom", true);
            Assert.AreEqual(MatchmakingState.Failed, m.State);
            Assert.AreEqual("boom", m.FailureMessage);
        }

        [Test]
        public void Machine_NonRetryableError_FailsAtOnce()
        {
            var m = Machine(new Clock());
            m.Start();
            m.OnError("denied", false);
            Assert.AreEqual(MatchmakingState.Failed, m.State);
        }

        [Test]
        public void Machine_CancelOnlyWhileQueued_AndIgnoresLateAnswers()
        {
            var m = Machine(new Clock());
            Assert.IsFalse(m.Cancel());
            m.Start();
            Assert.IsTrue(m.Cancel());
            Assert.AreEqual(MatchmakingState.Cancelled, m.State);
            m.OnMatched(MatchHandoff.Parse(MatchId, "h:1"));
            m.OnQueued(1);
            m.OnIdle();
            Assert.AreEqual(MatchmakingState.Cancelled, m.State);
            Assert.IsTrue(m.Start());
            Assert.AreEqual(MatchmakingState.Queued, m.State);
        }

        [Test]
        public void Machine_IdleFromPlatform_Fails()
        {
            var m = Machine(new Clock());
            m.Start();
            m.OnIdle();
            Assert.AreEqual(MatchmakingState.Failed, m.State);
        }

        // --- session ---

        static MatchmakingSession Session(FakeHttp http, Clock clock, Log log = null, double timeout = 60) =>
            new MatchmakingSession(new MatchmakingClient(Config(), http, log), Machine(clock, timeout), log);

        static List<PlatformHttpMethod> Methods(FakeHttp http)
        {
            var list = new List<PlatformHttpMethod>();
            foreach (var r in http.Requests) list.Add(r.Method);
            return list;
        }

        [Test]
        public void Session_JoinThenPollsThenMatched_AcknowledgesAndHandsOff()
        {
            var http = new FakeHttp();
            var clock = new Clock();
            var s = Session(http, clock);
            http.Scripted.Enqueue(PlatformHttpResponse.Http(200, Queued(0)));
            http.Scripted.Enqueue(PlatformHttpResponse.Http(200, Queued(1)));
            http.Scripted.Enqueue(PlatformHttpResponse.Http(200, Matched()));

            Assert.IsTrue(s.Start(Token));
            s.Tick();
            s.Tick();
            Assert.AreEqual(1, http.Requests.Count, "no second request inside the interval");
            clock.Now = 1;
            s.Tick();
            clock.Now = 2;
            s.Tick();

            Assert.AreEqual(MatchmakingState.Matched, s.Machine.State);
            Assert.AreEqual("10.0.0.5", s.Machine.Handoff.Host);
            Assert.AreEqual(new[] { PlatformHttpMethod.Post, PlatformHttpMethod.Get, PlatformHttpMethod.Get,
                PlatformHttpMethod.Delete }, Methods(http).ToArray());
        }

        [Test]
        public void Session_Cancel_SendsDeleteAndStopsPolling()
        {
            var http = new FakeHttp();
            var clock = new Clock();
            var s = Session(http, clock);
            http.Scripted.Enqueue(PlatformHttpResponse.Http(200, Queued(0)));
            s.Start(Token);
            s.Tick();
            Assert.IsTrue(s.Cancel());
            Assert.IsFalse(s.Cancel());
            clock.Now = 5;
            s.Tick();
            Assert.AreEqual(MatchmakingState.Cancelled, s.Machine.State);
            Assert.AreEqual(new[] { PlatformHttpMethod.Post, PlatformHttpMethod.Delete }, Methods(http).ToArray());
        }

        [Test]
        public void Session_Timeout_LeavesQueueAndFails()
        {
            var http = new FakeHttp();
            var clock = new Clock();
            var s = Session(http, clock, timeout: 5);
            http.Scripted.Enqueue(PlatformHttpResponse.Http(200, Queued(0)));
            s.Start(Token);
            s.Tick();
            clock.Now = 5;
            s.Tick();
            Assert.AreEqual(MatchmakingState.Failed, s.Machine.State);
            Assert.AreEqual(PlatformHttpMethod.Delete, http.Requests[http.Requests.Count - 1].Method);
        }

        [Test]
        public void Session_AlreadyMatchedOnJoin_AcknowledgesThenRejoins()
        {
            var http = new FakeHttp();
            var clock = new Clock();
            var s = Session(http, clock);
            http.Scripted.Enqueue(PlatformHttpResponse.Http(409, "{\"kind\":\"already_matched\",\"detail\":\"d\"}"));
            http.Scripted.Enqueue(PlatformHttpResponse.Http(204, ""));
            http.Scripted.Enqueue(PlatformHttpResponse.Http(200, Queued(0)));
            s.Start(Token);
            s.Tick();
            clock.Now = 1;
            s.Tick();
            Assert.AreEqual(MatchmakingState.Queued, s.Machine.State);
            Assert.AreEqual(new[] { PlatformHttpMethod.Post, PlatformHttpMethod.Delete, PlatformHttpMethod.Post },
                Methods(http).ToArray());
        }

        [Test]
        public void Session_Unauthorized_FailsAndNeverLogsToken()
        {
            var http = new FakeHttp();
            var log = new Log();
            var s = Session(http, new Clock(), log);
            http.Scripted.Enqueue(PlatformHttpResponse.Http(401, "{}"));
            s.Start(Token);
            s.Tick();
            Assert.AreEqual(MatchmakingState.Failed, s.Machine.State);
            Assert.IsFalse(string.Join("\n", log.Lines).Contains(Token));
        }

        [Test]
        public void Session_TransportBlip_KeepsPolling()
        {
            var http = new FakeHttp();
            var clock = new Clock();
            var s = Session(http, clock);
            http.Scripted.Enqueue(PlatformHttpResponse.Transport("down"));
            http.Scripted.Enqueue(PlatformHttpResponse.Http(200, Queued(0)));
            s.Start(Token);
            s.Tick();
            Assert.AreEqual(MatchmakingState.Queued, s.Machine.State);
            clock.Now = 1;
            s.Tick();
            Assert.AreEqual(2, http.Requests.Count);
            Assert.AreEqual(PlatformHttpMethod.Post, http.Requests[1].Method, "join retried until it lands");
        }

        [Test]
        public void Session_MatchedWithBadAddress_FailsButStillAcknowledges()
        {
            var http = new FakeHttp();
            var s = Session(http, new Clock());
            http.Scripted.Enqueue(PlatformHttpResponse.Http(200, Matched("garbage")));
            s.Start(Token);
            s.Tick();
            Assert.AreEqual(MatchmakingState.Failed, s.Machine.State);
            Assert.AreEqual(PlatformHttpMethod.Delete, http.Requests[1].Method);
        }
    }
}
