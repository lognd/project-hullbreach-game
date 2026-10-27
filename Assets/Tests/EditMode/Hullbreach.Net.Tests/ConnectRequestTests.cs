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

        [Test]
        public void FromHandoff_CarriesAddressMatchIdAndSkins()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ConnectRequest.cs::Hullbreach.Net.ConnectRequest.FromHandoff kind="unit"
            // frob:tests Assets/Scripts/Hullbreach.Net/ConnectRequest.cs::Hullbreach.Net.ConnectRequest.Describe kind="unit"
            var r = ConnectRequest.FromHandoff(Handoff(), new[] { "gold", "stripe", "gold" });
            Assert.IsTrue(r.IsOk, r.Error);
            Assert.AreEqual("game.example", r.Value.Host);
            Assert.AreEqual(7777, r.Value.Port);
            Assert.AreEqual(Canonical, r.Value.MatchId);
            CollectionAssert.AreEqual(new[] { "gold", "stripe" }, r.Value.EquippedSkins);
            StringAssert.Contains("match_id=" + Canonical, r.Value.Describe());
        }

        [Test]
        public void FromHandoff_NoSkins_IsEmptyList()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ConnectRequest.cs::Hullbreach.Net.ConnectRequest.FromHandoff kind="unit"
            Assert.AreEqual(0, ConnectRequest.FromHandoff(Handoff(), null).Value.EquippedSkins.Count);
        }

        [Test]
        public void FromHandoff_DefaultHandoff_IsError()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ConnectRequest.cs::Hullbreach.Net.ConnectRequest.FromHandoff kind="unit"
            Assert.IsFalse(ConnectRequest.FromHandoff(default, null).IsOk);
        }

        [TestCase("not-a-uuid")]
        [TestCase("00000000-0000-0000-0000-000000000000")]
        public void FromHandoff_BadMatchId_IsError(string id)
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ConnectRequest.cs::Hullbreach.Net.ConnectRequest.FromHandoff kind="unit"
            Assert.IsFalse(ConnectRequest.FromHandoff(Handoff(id), null).IsOk);
        }

        [TestCase("")]
        [TestCase("  ")]
        [TestCase("has space")]
        [TestCase("caf\x00e9")]
        public void FromHandoff_BadSkinId_IsError(string skin)
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ConnectRequest.cs::Hullbreach.Net.ConnectRequest.FromHandoff kind="unit"
            Assert.IsFalse(ConnectRequest.FromHandoff(Handoff(), new[] { skin }).IsOk);
        }

        [Test]
        public void FromHandoff_TooManyOrTooLongSkins_IsError()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ConnectRequest.cs::Hullbreach.Net.ConnectRequest.FromHandoff kind="unit"
            var many = new List<string>();
            for (int i = 0; i <= ConnectRequest.MaxSkins; i++) many.Add("s" + i);
            Assert.IsFalse(ConnectRequest.FromHandoff(Handoff(), many).IsOk);
            Assert.IsFalse(ConnectRequest.FromHandoff(Handoff(), new[] { new string('a', ConnectRequest.MaxSkinIdLength + 1) }).IsOk);
        }

        [Test]
        public void Normalize_TrimsAndLowercases()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ConnectRequest.cs::Hullbreach.Net.QueueMatchId.Normalize kind="unit"
            Assert.AreEqual(Canonical, QueueMatchId.Normalize("  " + Id + " ").Value);
            Assert.IsFalse(QueueMatchId.Normalize(null).IsOk);
        }

        static ShipSnapshot Ship() => new ShipSnapshot(0, 0, new[] { new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0) }, 0, 0, 0, 0, 0, 0);

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
