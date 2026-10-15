using Hullbreach.Platform;
using NUnit.Framework;

namespace Hullbreach.Platform.Tests
{
    // The mini parser must read real platform responses and refuse garbage.
    public sealed class PlatformJsonTests
    {
        [Test]
        public void ParsesLoginResponseShape()
        {
            const string body = "{\"token\":\"abc-_123\",\"user\":{\"id\":\"u1\",\"username\":\"pilot\"," +
                                "\"email\":\"p@example.test\",\"role\":\"player\",\"currency\":0,\"rating\":1000.5," +
                                "\"created_at\":\"2026-10-15T00:00:00Z\",\"tags\":[1,true,null]}}";
            Assert.IsTrue(PlatformJson.TryParseObject(body, out var root));
            Assert.AreEqual("abc-_123", PlatformJson.GetString(root, "token"));
            Assert.AreEqual("pilot", PlatformJson.GetString(PlatformJson.GetObject(root, "user"), "username"));
            Assert.IsNull(PlatformJson.GetString(root, "missing"));
            Assert.IsNull(PlatformJson.GetObject(root, "token"));
        }

        [Test]
        public void DecodesEscapes()
        {
            Assert.IsTrue(PlatformJson.TryParseObject("{\"s\":\"a\\\"b\\\\c\\n\\u0041\"}", out var root));
            Assert.AreEqual("a\"b\\c\nA", PlatformJson.GetString(root, "s"));
        }

        [TestCase("")]
        [TestCase("[1,2]")]
        [TestCase("{\"a\":}")]
        [TestCase("{\"a\":1")]
        [TestCase("{\"a\":1} trailing")]
        [TestCase("<html>Bad Gateway</html>")]
        public void RejectsNonObjectsAndSyntaxErrors(string text)
        {
            Assert.IsFalse(PlatformJson.TryParseObject(text, out _));
        }

        [Test]
        public void QuoteRoundTrips()
        {
            const string raw = "p\"a\\s\ts\u0001";
            Assert.IsTrue(PlatformJson.TryParseObject("{\"v\":" + PlatformJson.Quote(raw) + "}", out var root));
            Assert.AreEqual(raw, PlatformJson.GetString(root, "v"));
        }
    }
}
