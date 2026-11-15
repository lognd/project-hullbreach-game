using Hullbreach.Hud;
using NUnit.Framework;

namespace Hullbreach.Hud.Tests
{
    public sealed class HubMenuModelTests
    {
        [Test]
        public void OnlyTheOnlineQueueNeedsAnAccount()
        {
            Assert.IsTrue(HubMenuModel.NeedsSignIn(HubDestination.OnlineQueue));
            foreach (var d in new[] { HubDestination.Bot, HubDestination.CadetTrial, HubDestination.Lan, HubDestination.Title })
                Assert.IsFalse(HubMenuModel.NeedsSignIn(d), d.ToString());
        }

        [Test]
        public void OnlineGoesToTheQueueWhenSignedInAndToTheFormOtherwise()
        {
            Assert.AreEqual(HubDestination.OnlineQueue, HubMenuModel.OnlineTarget(true));
            Assert.AreEqual(HubDestination.SignIn, HubMenuModel.OnlineTarget(false));
        }

        [Test]
        public void AccountLineNamesTheGuestOrTheUser()
        {
            StringAssert.Contains("Ada", HubMenuModel.AccountLine(true, false, "Ada"));
            StringAssert.Contains("guest", HubMenuModel.AccountLine(false, true, ""));
            StringAssert.Contains("guests can play", HubMenuModel.AccountLine(false, false, ""));
        }
    }
}
