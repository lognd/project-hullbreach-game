using System;
using NUnit.Framework;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    // Pins the headless online smoke switch and its log lines, which `play online
    // --headless` and the doctor grep for.
    public class OnlineSmokeTests
    {
        [TestCase("1")]
        [TestCase("true")]
        [TestCase(" Yes ")]
        [TestCase("host")]
        public void Requested_ForTruthyValues(string value) => Assert.IsTrue(OnlineSmoke.Requested(value));

        [TestCase(null)]
        [TestCase("")]
        [TestCase("0")]
        [TestCase("no")]
        public void NotRequested_Otherwise(string value) => Assert.IsFalse(OnlineSmoke.Requested(value));

        [Test]
        public void LobbyName_IsTimeStamped()
        {
            Assert.AreEqual("Hullbreach smoke 20261015-093000",
                OnlineSmoke.LobbyName(new DateTime(2026, 10, 15, 9, 30, 0, DateTimeKind.Utc)));
        }

        [Test]
        public void Profile_ReadsALegalName()
        {
            Assert.AreEqual("player2", OnlineSmoke.Profile(new[] { "game.exe", "-hullbreachProfile", "player2" }));
            Assert.AreEqual("p_2-b", OnlineSmoke.Profile(new[] { "-HULLBREACHPROFILE", "p_2-b" }));
        }

        [Test]
        public void Profile_IsNullWhenMissingOrIllegal()
        {
            Assert.IsNull(OnlineSmoke.Profile(null));
            Assert.IsNull(OnlineSmoke.Profile(new string[0]));
            Assert.IsNull(OnlineSmoke.Profile(new[] { "-hullbreachProfile" }));
            Assert.IsNull(OnlineSmoke.Profile(new[] { "-hullbreachProfile", "" }));
            Assert.IsNull(OnlineSmoke.Profile(new[] { "-hullbreachProfile", "has space" }));
            Assert.IsNull(OnlineSmoke.Profile(new[] { "-hullbreachProfile", "a234567890123456789012345678901" }));
        }

        [Test]
        public void StatusLine_AddsTheErrorOnlyWhenThereIsOne()
        {
            Assert.AreEqual("Hullbreach online: Online", OnlineSmoke.StatusLine("Online", null));
            Assert.AreEqual("Hullbreach online: Online service error (no network)",
                OnlineSmoke.StatusLine("Online service error", "no network"));
        }
    }
}
