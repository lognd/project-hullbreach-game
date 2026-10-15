using Hullbreach.Platform;
using NUnit.Framework;

namespace Hullbreach.Platform.Tests
{
    // Request shape, every status mapping, and that secrets never reach the log.
    public sealed class PlatformClientTests
    {
        const string Token = "tok-SECRET-123";
        const string Password = "pw-SECRET-456";
        const string Key = "key-SECRET-789";

        static PlatformClient Client(FakePlatformHttp http, RecordingPlatformLog log, string serverKey = null) =>
            new PlatformClient(new PlatformConfig("http://platform.test:8002/", 3, false, serverKey), http, log);

        static LoginOutcome LoginWith(PlatformHttpResponse response, out FakePlatformHttp http, out RecordingPlatformLog log)
        {
            http = new FakePlatformHttp();
            log = new RecordingPlatformLog();
            http.Enqueue(response);
            LoginOutcome result = default;
            Client(http, log).Login(" pilot ", Password, outcome => result = outcome);
            return result;
        }

        [Test]
        public void Login_PostsJsonCredentialsToLoginPath()
        {
            LoginWith(PlatformHttpResponse.Http(401, "{}"), out var http, out _);
            Assert.AreEqual(1, http.Requests.Count);
            var request = http.Requests[0];
            Assert.AreEqual(PlatformHttpMethod.Post, request.Method);
            Assert.AreEqual("http://platform.test:8002/api/v1/auth/login", request.Url);
            Assert.AreEqual("{\"username\":\"pilot\",\"password\":\"" + Password + "\"}", request.Body);
            Assert.AreEqual(3.0, request.TimeoutSeconds);
            Assert.IsNull(request.Header("Authorization"));
        }

        [Test]
        public void Login_Success_ReturnsTokenAndCanonicalUsername()
        {
            var outcome = LoginWith(PlatformHttpResponse.Http(200,
                "{\"token\":\"" + Token + "\",\"user\":{\"id\":\"u1\",\"username\":\"Pilot\"}}"), out _, out var log);
            Assert.AreEqual(LoginStatus.Success, outcome.Status);
            Assert.IsTrue(outcome.IsSuccess);
            Assert.AreEqual(Token, outcome.Token);
            Assert.AreEqual("Pilot", outcome.Username);
            StringAssert.DoesNotContain("SECRET", outcome.ToString());
            StringAssert.DoesNotContain("SECRET", log.All);
            StringAssert.Contains("login succeeded for 'Pilot'", log.All);
        }

        [TestCase(401, LoginStatus.InvalidCredentials, "Wrong username or password.")]
        [TestCase(429, LoginStatus.RateLimited, "Too many sign-in attempts. Wait a minute and try again.")]
        [TestCase(422, LoginStatus.Failed, "The server rejected the sign-in request (HTTP 422).")]
        [TestCase(503, LoginStatus.Failed, "Sign-in failed (HTTP 503).")]
        public void Login_MapsErrorStatuses(int code, LoginStatus expected, string message)
        {
            var outcome = LoginWith(PlatformHttpResponse.Http(code, "{\"detail\":\"x\"}"), out _, out var log);
            Assert.AreEqual(expected, outcome.Status);
            Assert.AreEqual(message, outcome.Message);
            Assert.AreEqual("pilot", outcome.Username);
            Assert.AreEqual(string.Empty, outcome.Token);
            StringAssert.DoesNotContain("SECRET", log.All);
        }

        [Test]
        public void Login_TransportFailure_IsUnreachable()
        {
            var outcome = LoginWith(PlatformHttpResponse.Transport("Cannot connect to destination host"), out _, out var log);
            Assert.AreEqual(LoginStatus.Unreachable, outcome.Status);
            StringAssert.Contains("Cannot reach the Hullbreach servers", outcome.Message);
            StringAssert.DoesNotContain("SECRET", log.All);
        }

        [TestCase("not json")]
        [TestCase("{\"user\":{\"username\":\"pilot\"}}")]
        [TestCase("{\"token\":\"\"}")]
        public void Login_UnexpectedSuccessBody_IsFailed(string body)
        {
            var outcome = LoginWith(PlatformHttpResponse.Http(200, body), out _, out _);
            Assert.AreEqual(LoginStatus.Failed, outcome.Status);
            Assert.AreEqual(string.Empty, outcome.Token);
        }

        [Test]
        public void Login_SuccessWithoutUsername_FallsBackToTyped()
        {
            var outcome = LoginWith(PlatformHttpResponse.Http(200, "{\"token\":\"" + Token + "\"}"), out _, out _);
            Assert.IsTrue(outcome.IsSuccess);
            Assert.AreEqual("pilot", outcome.Username);
        }

        [TestCase("", "pw")]
        [TestCase("   ", "pw")]
        [TestCase("pilot", "")]
        [TestCase(null, null)]
        public void Login_EmptyFields_ShortCircuitWithoutHttp(string user, string pass)
        {
            var http = new FakePlatformHttp();
            LoginOutcome result = default;
            Client(http, new RecordingPlatformLog()).Login(user, pass, outcome => result = outcome);
            Assert.AreEqual(0, http.Requests.Count);
            Assert.AreEqual(LoginStatus.InvalidCredentials, result.Status);
            Assert.AreEqual("Enter a username and password.", result.Message);
        }

        [Test]
        public void ValidateSession_SendsBearerAndServerKey()
        {
            var http = new FakePlatformHttp();
            var log = new RecordingPlatformLog();
            http.Enqueue(PlatformHttpResponse.Http(200, "{\"user_id\":\"0f8e-uuid\",\"role\":\"player\"}"));
            SessionOutcome result = default;
            Client(http, log, Key).ValidateSession(Token, outcome => result = outcome);

            var request = http.Requests[0];
            Assert.AreEqual(PlatformHttpMethod.Get, request.Method);
            Assert.AreEqual("http://platform.test:8002/api/v1/auth/session", request.Url);
            Assert.IsNull(request.Body);
            Assert.AreEqual("Bearer " + Token, request.Header("authorization"));
            Assert.AreEqual(Key, request.Header(PlatformClient.ServerKeyHeader));
            Assert.AreEqual(SessionStatus.Valid, result.Status);
            Assert.AreEqual("0f8e-uuid", result.UserId);
            Assert.AreEqual("player", result.Role);
            StringAssert.DoesNotContain("SECRET", log.All);
        }

        [Test]
        public void ValidateSession_WithoutServerKey_OmitsHeader()
        {
            var http = new FakePlatformHttp();
            http.Enqueue(PlatformHttpResponse.Http(401, "{\"detail\":\"not authenticated\"}"));
            SessionOutcome result = default;
            Client(http, new RecordingPlatformLog()).ValidateSession(Token, outcome => result = outcome);
            Assert.IsNull(http.Requests[0].Header(PlatformClient.ServerKeyHeader));
            Assert.AreEqual(SessionStatus.Invalid, result.Status);
        }

        [Test]
        public void ValidateSession_EmptyToken_IsInvalidWithoutHttp()
        {
            var http = new FakePlatformHttp();
            SessionOutcome result = default;
            Client(http, new RecordingPlatformLog()).ValidateSession("", outcome => result = outcome);
            Assert.AreEqual(0, http.Requests.Count);
            Assert.AreEqual(SessionStatus.Invalid, result.Status);
        }

        [Test]
        public void MapSession_CoversEveryBranch()
        {
            Assert.AreEqual(SessionStatus.Unreachable, PlatformClient.MapSession(PlatformHttpResponse.Transport("timeout")).Status);
            Assert.AreEqual(SessionStatus.Invalid, PlatformClient.MapSession(PlatformHttpResponse.Http(401, "")).Status);
            Assert.AreEqual(SessionStatus.Failed, PlatformClient.MapSession(PlatformHttpResponse.Http(500, "")).Status);
            Assert.AreEqual(SessionStatus.Failed, PlatformClient.MapSession(PlatformHttpResponse.Http(200, "{\"role\":\"player\"}")).Status);
            Assert.AreEqual(SessionStatus.Failed, PlatformClient.MapSession(PlatformHttpResponse.Http(200, "<html>")).Status);
        }
    }
}
