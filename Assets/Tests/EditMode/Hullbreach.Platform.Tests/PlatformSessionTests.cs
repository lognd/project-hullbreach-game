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
    }
}
