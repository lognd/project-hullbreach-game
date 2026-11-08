using System.Linq;
using Hullbreach.Platform;
using NUnit.Framework;

namespace Hullbreach.Platform.Tests
{
    // S26-1..3: five buttons in order, one primary, and the signed-in line.
    // frob:ticket 01M3DG5Y1D78FFHNKW4AB7YB9B
    // frob:ticket 01M3DG5Y1ECEZ8A5Y7AJT2NM7X
    // frob:ticket 01M3DG5Y1FGWV5H8MC0MW0A41P
    public sealed class TitleMenuModelTests
    {
        // frob:tests TitleMenuModel.Items
        [Test]
        public void ItemsAreInOrderWithOnePrimary()
        {
            var items = TitleMenuModel.Items(false);
            CollectionAssert.AreEqual(
                new[] { TitleAction.Play, TitleAction.Build, TitleAction.Settings, TitleAction.SignIn, TitleAction.Quit },
                items.Select(i => i.Action).ToArray());
            Assert.AreEqual(1, items.Count(i => i.Primary));
            Assert.IsTrue(items[0].Primary);
        }

        // frob:tests TitleMenuModel.Items
        [Test]
        public void SignInButtonBecomesAccountWhenSignedIn()
        {
            Assert.AreEqual("SIGN IN", TitleMenuModel.Items(false)[3].Label);
            Assert.AreEqual("ACCOUNT", TitleMenuModel.Items(true)[3].Label);
        }

        // frob:tests TitleMenuModel.AccountLine
        [Test]
        public void AccountLineNamesThePlayerOrTheMode()
        {
            Assert.AreEqual("Signed in as ada", TitleMenuModel.AccountLine(true, false, "ada"));
            Assert.AreEqual("Signed in as player", TitleMenuModel.AccountLine(true, false, ""));
            Assert.AreEqual("Playing offline", TitleMenuModel.AccountLine(false, true, ""));
            Assert.AreEqual("Not signed in", TitleMenuModel.AccountLine(false, false, ""));
        }
    }
}
