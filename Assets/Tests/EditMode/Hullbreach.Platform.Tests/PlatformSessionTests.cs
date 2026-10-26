using System.Collections.Generic;
using Hullbreach.Platform;
using NUnit.Framework;

namespace Hullbreach.Platform.Tests
{
    // The in-memory session is the only place the client keeps the token.
    public sealed class PlatformSessionTests
    {
        [TearDown]
        public void Reset() => PlatformSession.SignOut();

        [Test]
        public void SignInThenSignOut()
        {
            Assert.IsFalse(PlatformSession.IsSignedIn);
            PlatformSession.SignIn("pilot", "tok");
            Assert.IsTrue(PlatformSession.IsSignedIn);
            Assert.AreEqual("pilot", PlatformSession.Username);
            Assert.AreEqual("tok", PlatformSession.Token);
            PlatformSession.SignOut();
            Assert.IsFalse(PlatformSession.IsSignedIn);
            Assert.AreEqual(string.Empty, PlatformSession.Token);
            Assert.AreEqual(string.Empty, PlatformSession.Username);
        }

        [Test]
        public void OfflineAndSignInReplaceEachOther()
        {
            Assert.IsFalse(PlatformSession.HasChosen);
            PlatformSession.SignIn("pilot", "tok");
            PlatformSession.PlayOffline();
            Assert.IsTrue(PlatformSession.IsOffline);
            Assert.IsTrue(PlatformSession.HasChosen);
            Assert.IsFalse(PlatformSession.IsSignedIn);
            Assert.AreEqual(string.Empty, PlatformSession.Token);
            PlatformSession.SignIn("pilot", "tok");
            Assert.IsFalse(PlatformSession.IsOffline);
            PlatformSession.SignOut();
            Assert.IsFalse(PlatformSession.HasChosen);
        }

        static System.Func<string, string> Env(Dictionary<string, string> values) =>
            name => values.TryGetValue(name, out string value) ? value : null;

        [Test]
        public void EnvironmentToken_SignsIn_AndNeverLogsTheToken()
        {
            var log = new RecordingPlatformLog();
            bool used = PlatformSession.SignInFromEnvironment(Env(new Dictionary<string, string>
            {
                [PlatformSession.TokenEnv] = " secret-token-value ",
                [PlatformSession.UsernameEnv] = "pilot",
            }), log);
            Assert.IsTrue(used);
            Assert.IsTrue(PlatformSession.IsSignedIn);
            Assert.AreEqual("secret-token-value", PlatformSession.Token);
            Assert.AreEqual("pilot", PlatformSession.Username);
            StringAssert.Contains("pilot", log.All);
            StringAssert.DoesNotContain("secret-token-value", log.All);
        }

        [Test]
        public void NoEnvironmentToken_LeavesTheSessionAlone()
        {
            var log = new RecordingPlatformLog();
            Assert.IsFalse(PlatformSession.SignInFromEnvironment(Env(new Dictionary<string, string>
            {
                [PlatformSession.TokenEnv] = "  ",
            }), log));
            Assert.IsFalse(PlatformSession.HasChosen);
            Assert.IsEmpty(log.Lines);
        }
    }
}
