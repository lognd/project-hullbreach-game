using Hullbreach.Net;
using NUnit.Framework;

namespace Hullbreach.Net.Tests
{
    // S29-2: the queue screen text and button availability follow the machine state.
    // frob:ticket 01M3DG5Y1PX311E24E62K5FAKG
    public sealed class QueueScreenModelTests
    {
        double _now;

        MatchmakingMachine Machine() => new MatchmakingMachine(() => _now, MatchmakingOptions.Defaults());

        // frob:tests QueueScreenModel.Elapsed
        [Test]
        public void ElapsedFormatsMinutesAndSeconds()
        {
            Assert.AreEqual("0:00", QueueScreenModel.Elapsed(-3));
            Assert.AreEqual("0:09", QueueScreenModel.Elapsed(9.9));
            Assert.AreEqual("1:15", QueueScreenModel.Elapsed(75));
        }

        // frob:tests QueueScreenModel.Status
        // frob:tests QueueScreenModel.CanSearch
        // frob:tests QueueScreenModel.CanCancel
        [Test]
        public void IdleOffersSearchOnly()
        {
            MatchmakingMachine m = Machine();
            Assert.AreEqual("Ready to find a match.", QueueScreenModel.Status(m));
            Assert.IsTrue(QueueScreenModel.CanSearch(m));
            Assert.IsFalse(QueueScreenModel.CanCancel(m));
        }

        // frob:tests QueueScreenModel.Status
        // frob:tests QueueScreenModel.CanCancel
        [Test]
        public void QueuedShowsElapsedAndOffersCancel()
        {
            MatchmakingMachine m = Machine();
            m.Start();
            _now = 65;
            Assert.AreEqual("Searching for an opponent... 1:05", QueueScreenModel.Status(m));
            Assert.IsTrue(QueueScreenModel.CanCancel(m));
            Assert.IsFalse(QueueScreenModel.CanSearch(m));
        }

        // frob:tests QueueScreenModel.Status
        [Test]
        public void CancelledAndMatchedAndFailedHaveTheirOwnText()
        {
            MatchmakingMachine m = Machine();
            m.Start();
            m.Cancel();
            Assert.AreEqual("Search cancelled.", QueueScreenModel.Status(m));
            Assert.IsTrue(QueueScreenModel.CanSearch(m));

            m.Start();
            m.OnMatched(MatchHandoff.Parse("m1", "host:7777"));
            StringAssert.Contains("host", QueueScreenModel.Status(m));

            m.Start();
            m.OnError("Cannot reach the Hullbreach servers.", false);
            Assert.AreEqual("Cannot reach the Hullbreach servers.", QueueScreenModel.Status(m));
        }

        // frob:tests QueueScreenModel.IsAlert
        [Test]
        public void OnlyFailureIsAnAlert()
        {
            MatchmakingMachine m = Machine();
            Assert.IsFalse(QueueScreenModel.IsAlert(m));
            m.Start();
            m.OnError("x", false);
            Assert.IsTrue(QueueScreenModel.IsAlert(m));
        }
    }
}
