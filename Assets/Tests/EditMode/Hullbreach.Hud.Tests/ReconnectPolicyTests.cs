using Hullbreach.Hud;
using NUnit.Framework;

namespace Hullbreach.Hud.Tests
{
    // frob:ticket 01M7FJ5KYZDQ37M8MC4CEV1DG7
    public sealed class ReconnectPolicyTests
    {
        // Drives the policy with a link that never comes back; returns the times (s) at which it asked to dial.
        static System.Collections.Generic.List<double> Dials(ReconnectPolicy p, double until)
        {
            var times = new System.Collections.Generic.List<double>();
            p.Update(0, true, false);
            for (double t = 0.1; t <= until; t += 0.1)
                if (p.Update(t, false, false) == ReconnectAction.Attempt) times.Add(System.Math.Round(t, 1));
            return times;
        }

        [Test]
        public void BackoffDoublesToTheCap()
        {
            var p = new ReconnectPolicy(6, 1.0, 8.0, 5.0);
            Assert.AreEqual(new[] { 1.0, 2.0, 4.0, 8.0, 8.0, 8.0 }, new[] { p.DelayBefore(1), p.DelayBefore(2), p.DelayBefore(3), p.DelayBefore(4), p.DelayBefore(5), p.DelayBefore(6) });
        }

        [Test]
        public void RetriesOnTheScheduleThenGivesUpWithAMessage()
        {
            var p = new ReconnectPolicy(3, 1.0, 4.0, 2.0);
            var dials = Dials(p, 60);
            Assert.AreEqual(3, dials.Count, "never more than the cap");
            Assert.AreEqual(ReconnectPhase.GaveUp, p.Phase);
            StringAssert.Contains("3 attempts", p.GiveUpText);
            Assert.AreEqual(string.Empty, p.StatusText);
            Assert.Greater(dials[1] - dials[0], 2.9, "attempt timeout plus backoff between dials");
        }

        [Test]
        public void StatusTextCountsAttempts()
        {
            var p = new ReconnectPolicy(6, 1.0, 8.0, 5.0);
            p.Update(0, true, false);
            p.Update(1, false, false);
            Assert.AreEqual("Reconnecting... (1/6)", p.StatusText);
            Assert.AreEqual(ReconnectAction.Attempt, p.Update(2.0, false, false));
            Assert.AreEqual("Reconnecting... (1/6)", p.StatusText);
            p.Update(7.5, false, false);
            Assert.AreEqual("Reconnecting... (2/6)", p.StatusText, "backing off before the next attempt");
        }

        [Test]
        public void SuccessResetsTheCounter()
        {
            var p = new ReconnectPolicy(2, 1.0, 4.0, 2.0);
            p.Update(0, true, false);
            p.Update(1, false, false);
            Assert.AreEqual(ReconnectAction.Attempt, p.Update(2, false, false));
            p.Update(3, true, false);
            Assert.AreEqual(0, p.Attempts);
            Assert.AreEqual(ReconnectPhase.Idle, p.Phase);
            p.Update(10, false, false);
            Assert.AreEqual(ReconnectAction.Attempt, p.Update(11, false, false), "a later drop retries afresh");
        }

        [Test]
        public void NeverRetriesWhenNeverConnected()
        {
            var p = new ReconnectPolicy();
            for (double t = 0; t < 60; t += 0.5) Assert.AreEqual(ReconnectAction.None, p.Update(t, false, false));
            Assert.AreEqual(ReconnectPhase.Idle, p.Phase);
        }

        [Test]
        public void NeverRetriesAfterADeliberateLeave()
        {
            var p = new ReconnectPolicy();
            p.Update(0, true, false);
            p.NoteDeliberateLeave();
            Assert.AreEqual(0, Dials(p, 60).Count);
            Assert.AreEqual(ReconnectPhase.Idle, p.Phase);
        }

        [Test]
        public void NeverRetriesAfterMatchEnd_EvenMidRetry()
        {
            var p = new ReconnectPolicy();
            p.Update(0, true, false);
            p.Update(1, false, false);
            Assert.AreEqual(ReconnectPhase.Waiting, p.Phase);
            Assert.AreEqual(ReconnectAction.None, p.Update(2, false, true));
            Assert.AreEqual(ReconnectPhase.Idle, p.Phase);
            for (double t = 3; t < 60; t += 0.5) Assert.AreEqual(ReconnectAction.None, p.Update(t, false, false));
        }

        [Test]
        public void BannerShowsReconnectingThenGiveUp_AndOnlyForADroppedLink()
        {
            var p = new ReconnectPolicy(2, 1.0, 4.0, 2.0);
            p.Update(0, true, false);
            p.Update(1, false, false);
            ConnectionBanner b = ConnectionStatusModel.Evaluate(true, false, true, 30, 5, false, p);
            Assert.AreEqual(ConnectionPhase.Reconnecting, b.Phase);
            Assert.AreEqual("Reconnecting... (1/2)", b.Title);
            Assert.IsTrue(b.OffersLobby);

            for (double t = 2; t < 40; t += 0.5) p.Update(t, false, false);
            b = ConnectionStatusModel.Evaluate(true, false, true, 30, 5, false, p);
            Assert.AreEqual(ConnectionPhase.Lost, b.Phase);
            StringAssert.Contains("2 attempts", b.Detail);

            // Never connected: the original join-failure wording, untouched by the policy.
            var fresh = new ReconnectPolicy();
            Assert.AreEqual("Could not join the match", ConnectionStatusModel.Evaluate(true, false, false, 12, 0, false, fresh).Title);
            // Deliberate leave: the plain Disconnected banner, no retry text.
            var left = new ReconnectPolicy();
            left.Update(0, true, false);
            left.NoteDeliberateLeave();
            left.Update(1, false, false);
            Assert.AreEqual("Disconnected", ConnectionStatusModel.Evaluate(true, false, true, 30, 5, false, left).Title);
        }
    }
}
