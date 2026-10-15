using System.Collections.Generic;
using Hullbreach.Platform;
using NUnit.Framework;

namespace Hullbreach.Platform.Tests
{
    // Every admission branch: guest vs required mode, validation results, timeouts, late callbacks.
    public sealed class PlatformAdmissionGateTests
    {
        const string Token = "tok-SECRET-abc";
        const string ValidBody = "{\"user_id\":\"12345678-aaaa-bbbb\",\"role\":\"player\"}";

        FakePlatformHttp _http;
        RecordingPlatformLog _log;

        PlatformAdmissionGate Gate(bool requireAuth, double helloTimeout = 10)
        {
            _http = new FakePlatformHttp();
            _log = new RecordingPlatformLog();
            var client = new PlatformClient(new PlatformConfig("http://platform.test", 5, requireAuth, "key"), _http, _log);
            return new PlatformAdmissionGate(client, requireAuth, helloTimeout, _log);
        }

        static List<AdmissionDecision> Drain(PlatformAdmissionGate gate)
        {
            var list = new List<AdmissionDecision>();
            while (gate.TryTakeDecision(out var decision)) list.Add(decision);
            return list;
        }

        [TearDown]
        public void NoSecretsLogged()
        {
            if (_log != null) StringAssert.DoesNotContain("SECRET", _log.All);
        }

        [Test]
        public void Optional_AdmitsGuestImmediately()
        {
            PlatformAdmissionGate gate = Gate(false);
            gate.OnConnected(3, 0);
            var decisions = Drain(gate);
            Assert.AreEqual(1, decisions.Count);
            Assert.IsTrue(decisions[0].Admitted);
            Assert.IsFalse(decisions[0].Verified);
            Assert.AreEqual("Guest #3", decisions[0].DisplayName);
            Assert.IsTrue(gate.IsAdmitted(3));
        }

        [Test]
        public void Optional_ValidTokenUpgradesToVerified()
        {
            PlatformAdmissionGate gate = Gate(false);
            gate.OnConnected(1, 0);
            Drain(gate);
            _http.Enqueue(PlatformHttpResponse.Http(200, ValidBody));
            gate.OnHello(1, Token, "pilot", 0.1);
            var decisions = Drain(gate);
            Assert.AreEqual(1, decisions.Count);
            Assert.IsTrue(decisions[0].Admitted);
            Assert.IsTrue(decisions[0].Verified);
            Assert.AreEqual("pilot", decisions[0].DisplayName);
            Assert.AreEqual("12345678-aaaa-bbbb", decisions[0].UserId);
        }

        [Test]
        public void Optional_InvalidTokenStaysGuestWithReason()
        {
            PlatformAdmissionGate gate = Gate(false);
            gate.OnConnected(1, 0);
            Drain(gate);
            _http.Enqueue(PlatformHttpResponse.Http(401, "{}"));
            gate.OnHello(1, Token, "pilot", 0.1);
            var decisions = Drain(gate);
            Assert.AreEqual(1, decisions.Count);
            Assert.IsTrue(decisions[0].Admitted);
            Assert.IsFalse(decisions[0].Verified);
            Assert.AreEqual("Guest #1", decisions[0].DisplayName);
            StringAssert.Contains("invalid or expired", decisions[0].Reason);
            Assert.IsTrue(gate.IsAdmitted(1));
        }

        [Test]
        public void Optional_EmptyTokenHelloIsOfflineGuest_NoHttp()
        {
            PlatformAdmissionGate gate = Gate(false);
            gate.OnConnected(2, 0);
            Drain(gate);
            gate.OnHello(2, "", "pilot", 0.1);
            Assert.AreEqual(0, _http.Requests.Count);
            Assert.AreEqual(0, Drain(gate).Count);
            Assert.IsTrue(gate.IsAdmitted(2));
        }

        [Test]
        public void Optional_SilentPeerIsNeverTimedOut()
        {
            PlatformAdmissionGate gate = Gate(false, 1);
            gate.OnConnected(2, 0);
            Drain(gate);
            gate.Tick(100);
            Assert.AreEqual(0, Drain(gate).Count);
            Assert.IsTrue(gate.IsAdmitted(2));
        }

        [Test]
        public void Required_WaitsForHelloThenAdmitsVerified()
        {
            PlatformAdmissionGate gate = Gate(true);
            gate.OnConnected(4, 0);
            Assert.AreEqual(0, Drain(gate).Count);
            Assert.IsFalse(gate.IsAdmitted(4));
            _http.Enqueue(PlatformHttpResponse.Http(200, ValidBody));
            gate.OnHello(4, Token, "", 0.5);
            var decisions = Drain(gate);
            Assert.AreEqual(1, decisions.Count);
            Assert.IsTrue(decisions[0].Verified);
            Assert.AreEqual("player 12345678", decisions[0].DisplayName);
            Assert.IsTrue(gate.IsAdmitted(4));
        }

        [Test]
        public void Required_EmptyTokenIsRejected()
        {
            PlatformAdmissionGate gate = Gate(true);
            gate.OnConnected(4, 0);
            gate.OnHello(4, null, "pilot", 0.5);
            var decisions = Drain(gate);
            Assert.AreEqual(1, decisions.Count);
            Assert.IsFalse(decisions[0].Admitted);
            StringAssert.Contains("requires a Hullbreach account", decisions[0].Reason);
            Assert.IsFalse(gate.IsAdmitted(4));
        }

        [Test]
        public void Required_InvalidTokenIsRejected()
        {
            PlatformAdmissionGate gate = Gate(true);
            gate.OnConnected(4, 0);
            _http.Enqueue(PlatformHttpResponse.Http(401, "{}"));
            gate.OnHello(4, Token, "pilot", 0.5);
            var decisions = Drain(gate);
            Assert.IsFalse(decisions[0].Admitted);
            Assert.AreEqual("Your platform session is invalid or expired. Sign in again.", decisions[0].Reason);
        }

        [Test]
        public void Required_UnreachablePlatformFailsClosed()
        {
            PlatformAdmissionGate gate = Gate(true);
            gate.OnConnected(4, 0);
            _http.Enqueue(PlatformHttpResponse.Transport("connection refused"));
            gate.OnHello(4, Token, "pilot", 0.5);
            var decisions = Drain(gate);
            Assert.IsFalse(decisions[0].Admitted);
            StringAssert.StartsWith("Could not verify your platform session", decisions[0].Reason);
        }

        [Test]
        public void Required_SilentPeerTimesOut()
        {
            PlatformAdmissionGate gate = Gate(true, 2);
            gate.OnConnected(5, 0);
            gate.Tick(1.9);
            Assert.AreEqual(0, Drain(gate).Count);
            gate.Tick(2.0);
            var decisions = Drain(gate);
            Assert.AreEqual(1, decisions.Count);
            Assert.IsFalse(decisions[0].Admitted);
            Assert.AreEqual("No platform sign-in received in time.", decisions[0].Reason);
            gate.Tick(3);
            Assert.AreEqual(0, Drain(gate).Count);
        }

        [Test]
        public void Required_PendingValidationIsNotTimedOut()
        {
            PlatformAdmissionGate gate = Gate(true, 2);
            _http.Hold = true;
            gate.OnConnected(5, 0);
            gate.OnHello(5, Token, "pilot", 1);
            gate.Tick(10);
            Assert.AreEqual(0, Drain(gate).Count);
            _http.Complete(0, PlatformHttpResponse.Http(200, ValidBody));
            Assert.IsTrue(Drain(gate)[0].Verified);
        }

        [Test]
        public void LateCallbackForReusedPeerIdIsDropped()
        {
            PlatformAdmissionGate gate = Gate(true);
            _http.Hold = true;
            gate.OnConnected(7, 0);
            gate.OnHello(7, Token, "first", 0.1);
            gate.OnDisconnected(7);
            gate.OnConnected(7, 1);
            _http.Complete(0, PlatformHttpResponse.Http(200, ValidBody));
            Assert.AreEqual(0, Drain(gate).Count);
            Assert.IsFalse(gate.IsAdmitted(7));
            StringAssert.Contains("already left", _log.All);
        }

        [Test]
        public void RepeatedAndUnknownHellosAreIgnored()
        {
            PlatformAdmissionGate gate = Gate(true);
            gate.OnHello(9, Token, "ghost", 0);
            gate.OnConnected(1, 0);
            _http.Enqueue(PlatformHttpResponse.Http(200, ValidBody));
            gate.OnHello(1, Token, "pilot", 0.1);
            gate.OnHello(1, Token, "pilot", 0.2);
            Assert.AreEqual(1, _http.Requests.Count);
            Assert.AreEqual(1, Drain(gate).Count);
            StringAssert.Contains("unknown peer 9", _log.All);
            StringAssert.Contains("repeated sign-in from peer 1", _log.All);
        }

        [Test]
        public void SanitizeName_StripsControlCharsAndCapsLength()
        {
            Assert.AreEqual("pilot", PlatformAdmissionGate.SanitizeName("  pi\nlo\u0007t "));
            Assert.AreEqual(string.Empty, PlatformAdmissionGate.SanitizeName(null));
            Assert.AreEqual(PlatformAdmissionGate.MaxDisplayNameLength,
                PlatformAdmissionGate.SanitizeName(new string('x', 100)).Length);
        }
    }
}
