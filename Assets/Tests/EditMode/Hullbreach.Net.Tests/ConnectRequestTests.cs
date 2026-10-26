using System.Collections.Generic;
using Hullbreach.Core;
using Hullbreach.Match;
using Hullbreach.Platform;
using NUnit.Framework;

namespace Hullbreach.Net.Tests
{
    // Pins the handoff-to-connect adapter and the server's queue match id (S29-3).
    // frob:ticket 01M3DG5Y1Q243GK60T3RHV8HRV
    public class ConnectRequestTests
    {
        const string Id = "6F1C1C1E-0A53-4D2E-9D57-3F2A6A8D9B10";
        const string Canonical = "6f1c1c1e-0a53-4d2e-9d57-3f2a6a8d9b10";

        static MatchHandoff Handoff(string id = Id) => MatchHandoff.Parse(id, "game.example:7777", "u2").Value;

        // frob:tests Hullbreach.Net.ConnectRequest.FromHandoff
        [Test]
        public void FromHandoff_CarriesAddressMatchIdAndSkins()
        {
            var r = ConnectRequest.FromHandoff(Handoff(), new[] { "gold", "stripe", "gold" });
            Assert.IsTrue(r.IsOk, r.Error);
            Assert.AreEqual("game.example", r.Value.Host);
            Assert.AreEqual(7777, r.Value.Port);
            Assert.AreEqual(Canonical, r.Value.MatchId);
            CollectionAssert.AreEqual(new[] { "gold", "stripe" }, r.Value.EquippedSkins);
            StringAssert.Contains("match_id=" + Canonical, r.Value.Describe());
        }

        // frob:tests Hullbreach.Net.ConnectRequest.FromHandoff
        [Test]
        public void FromHandoff_NoSkins_IsEmptyList()
        {
            Assert.AreEqual(0, ConnectRequest.FromHandoff(Handoff(), null).Value.EquippedSkins.Count);
        }

        // frob:tests Hullbreach.Net.ConnectRequest.FromHandoff
        [Test]
        public void FromHandoff_DefaultHandoff_IsError()
        {
            Assert.IsFalse(ConnectRequest.FromHandoff(default, null).IsOk);
        }

        // frob:tests Hullbreach.Net.ConnectRequest.FromHandoff
        [TestCase("not-a-uuid")]
        [TestCase("00000000-0000-0000-0000-000000000000")]
        public void FromHandoff_BadMatchId_IsError(string id)
        {
            Assert.IsFalse(ConnectRequest.FromHandoff(Handoff(id), null).IsOk);
        }

        // frob:tests Hullbreach.Net.ConnectRequest.FromHandoff
        [TestCase("")]
        [TestCase("  ")]
        [TestCase("has space")]
        [TestCase("caf\x00e9")]
        public void FromHandoff_BadSkinId_IsError(string skin)
        {
            Assert.IsFalse(ConnectRequest.FromHandoff(Handoff(), new[] { skin }).IsOk);
        }

        // frob:tests Hullbreach.Net.ConnectRequest.FromHandoff
        [Test]
        public void FromHandoff_TooManyOrTooLongSkins_IsError()
        {
            var many = new List<string>();
            for (int i = 0; i <= ConnectRequest.MaxSkins; i++) many.Add("s" + i);
            Assert.IsFalse(ConnectRequest.FromHandoff(Handoff(), many).IsOk);
            Assert.IsFalse(ConnectRequest.FromHandoff(Handoff(), new[] { new string('a', ConnectRequest.MaxSkinIdLength + 1) }).IsOk);
        }

        // frob:tests Hullbreach.Net.QueueMatchId.Normalize
        [Test]
        public void Normalize_TrimsAndLowercases()
        {
            Assert.AreEqual(Canonical, QueueMatchId.Normalize("  " + Id + " ").Value);
            Assert.IsFalse(QueueMatchId.Normalize(null).IsOk);
        }

        static ShipSnapshot Ship() => new ShipSnapshot(0, 0, new[] { new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0) }, 0, 0, 0, 0, 0, 0);

        // frob:tests Hullbreach.Net.ServerSimulation.AssignQueueMatch
        [Test]
        public void ServerMatch_UsesAssignedQueueIdOnce()
        {
            var feed = new MatchEndFeed();
            var server = new ServerSimulation(new ServerOutbox()) { MatchFeed = feed };
            Assert.IsTrue(server.AssignQueueMatch(Id));
            server.Join(1, Ship());
            server.Join(2, Ship());
            Assert.AreEqual(Canonical, server.Match.MatchId);
        }

        // frob:tests Hullbreach.Net.ServerSimulation.AssignQueueMatch
        [Test]
        public void ServerAssign_BadIdOrLiveMatch_IsRefused()
        {
            var server = new ServerSimulation(new ServerOutbox()) { MatchFeed = new MatchEndFeed() };
            Assert.IsFalse(server.AssignQueueMatch("nope"));
            server.Join(1, Ship());
            server.Join(2, Ship());
            Assert.IsFalse(server.AssignQueueMatch(Id));
        }
    }
}
