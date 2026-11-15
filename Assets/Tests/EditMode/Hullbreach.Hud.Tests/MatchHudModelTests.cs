using System.Collections.Generic;
using Hullbreach.Hud;
using NUnit.Framework;

namespace Hullbreach.Hud.Tests
{
    public sealed class MatchHudModelTests
    {
        [Test]
        public void CoreDangerBands()
        {
            Assert.AreEqual(CoreDanger.Healthy, CoreHealthModel.Danger(1f));
            Assert.AreEqual(CoreDanger.Low, CoreHealthModel.Danger(0.5f));
            Assert.AreEqual(CoreDanger.Critical, CoreHealthModel.Danger(0.25f));
            Assert.AreEqual(CoreDanger.Critical, CoreHealthModel.Danger(0f));
        }

        [Test]
        public void CoreLabelClampsAndRounds()
        {
            Assert.AreEqual("YOUR CORE 72%", CoreHealthModel.Label("YOUR", 0.716f));
            Assert.AreEqual("ENEMY CORE 0%", CoreHealthModel.Label("ENEMY", -1f));
            Assert.AreEqual("ENEMY CORE 100%", CoreHealthModel.Label("ENEMY", 3f));
        }

        [Test]
        public void CoreColourTurnsAmberThenRed()
        {
            var cyan = new HudColor(0f, 1f, 1f);
            Assert.AreEqual(cyan.G, CoreHealthModel.Colour(0.9f, cyan).G);
            Assert.AreEqual(HudColor.WarningStrainYellow.R, CoreHealthModel.Colour(0.4f, cyan).R);
            Assert.AreEqual(HudColor.WarningCriticalRed.G, CoreHealthModel.Colour(0.1f, cyan).G);
            Assert.AreEqual(1f, CoreHealthModel.Pulse(1f, 3.3f));
            Assert.That(CoreHealthModel.Pulse(0.1f, 0.1f), Is.InRange(0.6f, 1f));
        }

        [Test]
        public void FeedFoldsRepeatsFadesAndExpires()
        {
            var feed = new EventFeedModel(6f, 3);
            var lines = new List<FeedLine>();
            var red = HudColor.WarningCriticalRed;
            feed.Push("ENEMY CORE HIT", red, 0f);
            feed.Push("ENEMY CORE HIT", red, 0.5f);
            feed.Visible(1f, lines);
            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual("ENEMY CORE HIT x2", lines[0].Text);
            Assert.AreEqual(1f, lines[0].Alpha);
            feed.Visible(6.2f, lines); // the repeat refreshed the line at 0.5, so it is 5.7s old: in its final second
            Assert.AreEqual(1, lines.Count);
            Assert.Less(lines[0].Alpha, 1f);
            feed.Visible(7f, lines);
            Assert.AreEqual(0, lines.Count);
        }

        [Test]
        public void FeedKeepsOnlyTheNewestLines()
        {
            var feed = new EventFeedModel(6f, 2);
            var lines = new List<FeedLine>();
            feed.Push("a", HudColor.SteerWhite, 0f);
            feed.Push("b", HudColor.SteerWhite, 2f);
            feed.Push("c", HudColor.SteerWhite, 4f);
            feed.Visible(4.5f, lines);
            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual("b", lines[0].Text);
            Assert.AreEqual("c", lines[1].Text);
        }
    }
}
