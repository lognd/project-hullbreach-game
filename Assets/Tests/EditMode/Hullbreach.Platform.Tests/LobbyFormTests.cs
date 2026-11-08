using NUnit.Framework;

namespace Hullbreach.Platform.Tests
{
    // Lobby form parsing, local validation and the Esc rule.
    // frob:ticket 01M6WV5HP3KVSZNJT6H99MFS3Z
    // frob:tests Hullbreach.Platform.LobbyForm
    public sealed class LobbyFormTests
    {
        [TestCase("4", 4)]
        [TestCase("", 8)]
        [TestCase(null, 8)]
        [TestCase("abc", 8)]
        [TestCase("1", 2)]
        [TestCase("500", 16)]
        public void ParsePlayersDefaultsAndClamps(string text, int expected)
        {
            Assert.AreEqual(expected, LobbyForm.ParsePlayers(text));
        }

        [TestCase(null, true)]
        [TestCase("", true)]
        [TestCase("short", false)]
        [TestCase("longenough", true)]
        public void PasswordProblemOnlyWhenBadLength(string password, bool ok)
        {
            Assert.AreEqual(ok, LobbyForm.PasswordProblem(password) == null);
        }

        [Test]
        public void PasswordOverSixtyFourIsAProblem()
        {
            Assert.IsNotNull(LobbyForm.PasswordProblem(new string('x', 65)));
        }

        [TestCase(null, false)]
        [TestCase("  ", false)]
        [TestCase("ABC123", true)]
        public void CodeProblemFlagsBlank(string code, bool ok)
        {
            Assert.AreEqual(ok, LobbyForm.CodeProblem(code) == null);
        }

        [Test]
        public void EscapeLeavesOnlyWhenInRoomAndIdle()
        {
            Assert.IsTrue(LobbyForm.EscapeLeavesRoom(true, false));
            Assert.IsFalse(LobbyForm.EscapeLeavesRoom(true, true));
            Assert.IsFalse(LobbyForm.EscapeLeavesRoom(false, false));
        }
    }
}
