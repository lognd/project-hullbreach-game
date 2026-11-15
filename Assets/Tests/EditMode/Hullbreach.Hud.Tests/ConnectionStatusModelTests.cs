using Hullbreach.Hud;
using NUnit.Framework;

namespace Hullbreach.Hud.Tests
{
    public sealed class ConnectionStatusModelTests
    {
        [Test]
        public void NothingShownOutsideAMatchOrWhenHealthy()
        {
            Assert.AreEqual(ConnectionPhase.Hidden, ConnectionStatusModel.Evaluate(false, false, false, 99, 99, false).Phase);
            Assert.AreEqual(ConnectionPhase.Hidden, ConnectionStatusModel.Evaluate(true, true, true, 30, 0.2, false).Phase);
            Assert.AreEqual(ConnectionPhase.Hidden, ConnectionStatusModel.Evaluate(true, false, true, 30, 99, true).Phase);
        }

        [Test]
        public void LoadingThenTimeoutWhenNeverConnected()
        {
            var loading = ConnectionStatusModel.Evaluate(true, false, false, 3, 0, false);
            Assert.AreEqual(ConnectionPhase.Connecting, loading.Phase);
            StringAssert.Contains("3 s", loading.Detail);
            Assert.IsFalse(loading.OffersLobby);
            var failed = ConnectionStatusModel.Evaluate(true, false, false, 10, 0, false);
            Assert.AreEqual(ConnectionPhase.Lost, failed.Phase);
            Assert.AreEqual("Could not join the match", failed.Title);
            Assert.IsTrue(failed.OffersLobby);
        }

        [Test]
        public void QuietServerShowsReconnectBannerThenLost()
        {
            Assert.AreEqual(ConnectionPhase.Unstable, ConnectionStatusModel.Evaluate(true, true, true, 60, 2.5, false).Phase);
            var lost = ConnectionStatusModel.Evaluate(true, true, true, 60, 8, false);
            Assert.AreEqual(ConnectionPhase.Lost, lost.Phase);
            Assert.IsTrue(lost.OffersLobby);
        }

        [Test]
        public void DroppedAfterConnectingMeansTheHostLeft()
        {
            var b = ConnectionStatusModel.Evaluate(true, false, true, 60, 0, false);
            Assert.AreEqual(ConnectionPhase.Lost, b.Phase);
            Assert.AreEqual("Disconnected", b.Title);
            Assert.IsTrue(b.OffersLobby);
        }
    }
}
