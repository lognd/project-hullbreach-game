using NUnit.Framework;

namespace Hullbreach.Hud.Tests
{
    public sealed class HubTierTests
    {
        [Test]
        public void Newcomer_DefaultsToEasy()
        {
            Assert.AreEqual(0, HubMenuModel.DefaultBotTier(-1, false, 1));
            Assert.AreEqual(0, HubMenuModel.DefaultBotTier(-1, false, 2), "the rating cannot override a newcomer");
        }

        [Test]
        public void RememberedTier_WinsOnceThereIsOne()
        {
            Assert.AreEqual(2, HubMenuModel.DefaultBotTier(2, true, 0));
            Assert.AreEqual(1, HubMenuModel.DefaultBotTier(1, false, 0));
            Assert.AreEqual(0, HubMenuModel.DefaultBotTier(0, true, 2));
        }

        [Test]
        public void ExperiencedPlayerWithNoMemory_FallsBackToTheSuggestion()
        {
            Assert.AreEqual(2, HubMenuModel.DefaultBotTier(-1, true, 2));
            Assert.AreEqual(1, HubMenuModel.DefaultBotTier(-1, true, 99), "an out-of-range fallback becomes Normal");
            Assert.AreEqual(0, HubMenuModel.DefaultBotTier(7, false, 1), "a corrupt memory is ignored");
        }

        [Test]
        public void Descriptions_AreOneLine()
        {
            Assert.AreEqual("Easy: forgiving practice", HubMenuModel.TierDescription(0));
            Assert.AreEqual("Normal: a fair fight", HubMenuModel.TierDescription(1));
            Assert.AreEqual("Hard: expert", HubMenuModel.TierDescription(2));
            Assert.AreEqual(string.Empty, HubMenuModel.TierDescription(3));
            foreach (var d in HubMenuModel.BotTierDescriptions) StringAssert.DoesNotContain("\n", d);
        }

        [Test]
        public void Labels_StarTheDefaultAndCarryTheHint()
        {
            Assert.AreEqual("EASY *\n<size=60%>forgiving practice</size>", HubMenuModel.TierLabelWithHint(0, 0));
            Assert.AreEqual("NORMAL\n<size=60%>a fair fight</size>", HubMenuModel.TierLabelWithHint(1, 0));
            Assert.AreEqual("HARD *", HubMenuModel.TierLabel(2, 2));
            Assert.AreEqual(string.Empty, HubMenuModel.TierLabel(5, 0));
        }
    }
}
