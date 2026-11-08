using NUnit.Framework;

namespace Hullbreach.Platform.Tests
{
    // Raw service text never reaches the player; game-authored messages do.
    // frob:ticket 01M6WV5HP3KVSZNJT6H99MFS3Z
    // frob:tests Hullbreach.Platform.FriendlyError
    public sealed class FriendlyErrorTests
    {
        [Test]
        public void OwnMessagePassesThrough()
        {
            Assert.AreEqual("Lobby passwords must be 8 to 64 characters.",
                FriendlyError.From("Lobby passwords must be 8 to 64 characters."));
        }

        [Test]
        public void ParameterSuffixIsStripped()
        {
            Assert.AreEqual("Enter a lobby code.", FriendlyError.From("Enter a lobby code. (Parameter 'joinCode')"));
        }

        [TestCase("The request timed out", "The online service did not answer in time. Check your connection and try again.")]
        [TestCase("Incorrect password supplied", "Wrong lobby password.")]
        [TestCase("Lobby is full", "That lobby is full.")]
        [TestCase("Session is locked", "That lobby has already started.")]
        [TestCase("Lobby not found", "No lobby found. Check the code and try again.")]
        [TestCase("HTTP 429 Too Many Requests", "Too many requests. Wait a few seconds and try again.")]
        [TestCase("Cannot resolve destination host", "Cannot reach the online service. Check your internet connection.")]
        public void KnownFailuresMapToClearText(string raw, string expected)
        {
            Assert.AreEqual(expected, FriendlyError.From(raw));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("NullReferenceException at Foo.Bar()")]
        public void UnknownOrEmptyBecomesGeneric(string raw)
        {
            Assert.AreEqual(FriendlyError.Generic, FriendlyError.From(raw));
        }

        [Test]
        public void ServicesDownAddsLanHint()
        {
            Assert.AreEqual(FriendlyError.Generic + FriendlyError.LanHint, FriendlyError.From("boom", false));
        }
    }
}
