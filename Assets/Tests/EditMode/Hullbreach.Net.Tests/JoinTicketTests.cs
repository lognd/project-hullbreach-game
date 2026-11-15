using Hullbreach.Core;
using Hullbreach.Match;
using Hullbreach.Platform;
using NUnit.Framework;

namespace Hullbreach.Net.Tests
{
    // Pins the queue match id handoff: the JoinTicket wire message, its server gate and the allocation argument (S29-3c).
    // frob:ticket 01M5XACF0K75AVMM0MCNDDWXNE
    public class JoinTicketTests
    {
        const string Id = "6f1c1c1e-0a53-4d2e-9d57-3f2a6a8d9b10";
        const string Other = "11111111-2222-3333-4444-555555555555";

        static ShipSnapshot Ship() => new ShipSnapshot(0, 0, new[] { new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0) }, 0, 0, 0, 0, 0, 0);

        static byte[] Wire(string id)
        {
            var buffer = new byte[JoinTicket.ByteSize];
            var w = new ByteWriter(buffer);
            Assert.IsTrue(new JoinTicket(id).TryWrite(ref w));
            Assert.AreEqual(JoinTicket.ByteSize, w.Position);
            return buffer;
        }

        static ServerSimulation Server(bool requireTicket = true) =>
            new ServerSimulation(new ServerOutbox()) { MatchFeed = new MatchEndFeed(), RequireJoinTicket = requireTicket };

        [Test]
        public void RoundTrip_KeepsTheId()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/NetMessages.cs::Hullbreach.Net.JoinTicket.TryWrite kind="unit"
            // frob:tests Assets/Scripts/Hullbreach.Net/NetMessages.cs::Hullbreach.Net.JoinTicket.Read kind="unit"
            byte[] bytes = Wire(Id.ToUpperInvariant());
            Assert.AreEqual((byte)MessageKind.JoinTicket, bytes[0]);
            var r = new ByteReader(bytes, 0, bytes.Length);
            Assert.AreEqual(Id, JoinTicket.Read(ref r).MatchId);
            Assert.IsFalse(r.Failed);
        }

        [Test]
        public void BadId_IsNotWritten_AndShortOrZeroBodyFails()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/NetMessages.cs::Hullbreach.Net.JoinTicket.TryWrite kind="unit"
            // frob:tests Assets/Scripts/Hullbreach.Net/NetMessages.cs::Hullbreach.Net.JoinTicket.Read kind="unit"
            var w = new ByteWriter(new byte[JoinTicket.ByteSize]);
            Assert.IsFalse(new JoinTicket("nope").TryWrite(ref w));
            Assert.AreEqual(0, w.Position);

            byte[] bytes = Wire(Id);
            var shortReader = new ByteReader(bytes, 0, 9);
            JoinTicket.Read(ref shortReader);
            Assert.IsTrue(shortReader.Failed);

            var zero = new byte[JoinTicket.ByteSize];
            zero[0] = (byte)MessageKind.JoinTicket;
            var zeroReader = new ByteReader(zero, 0, zero.Length);
            JoinTicket.Read(ref zeroReader);
            Assert.IsTrue(zeroReader.Failed);
        }

        [Test]
        public void Dispatch_MatchingTicket_LetsThePeerJoin()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/HostInbox.cs::Hullbreach.Net.HostInbox.Dispatch kind="unit"
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::Hullbreach.Net.ServerSimulation.PresentTicket kind="unit"
            ServerSimulation server = Server();
            Assert.IsTrue(server.AssignQueueMatch(Id));
            Assert.AreEqual(JoinResult.TicketRejected, server.Join(1, Ship()));

            byte[] bytes = Wire(Id);
            Assert.IsTrue(HostInbox.Dispatch(server, 1, bytes, bytes.Length));
            Assert.AreEqual(JoinResult.Joined, server.Join(1, Ship()));
            Assert.IsFalse(server.TryDequeueDroppedPeer(out _));
        }

        [Test]
        public void Dispatch_WrongTicket_RefusesAndDropsThePeer()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::Hullbreach.Net.ServerSimulation.PresentTicket kind="unit"
            ServerSimulation server = Server();
            server.AssignQueueMatch(Id);
            byte[] bytes = Wire(Other);
            Assert.IsFalse(HostInbox.Dispatch(server, 7, bytes, bytes.Length));
            Assert.IsTrue(server.TryDequeueDroppedPeer(out int dropped));
            Assert.AreEqual(7, dropped);
            Assert.AreEqual(JoinResult.TicketRejected, server.Join(7, Ship()));
        }

        [Test]
        public void NoAllocationYet_AcceptsNobodyWhileTicketsAreRequired()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::Hullbreach.Net.ServerSimulation.PresentTicket kind="unit"
            ServerSimulation server = Server();
            Assert.IsFalse(server.PresentTicket(1, Id));
            Assert.AreEqual(JoinResult.TicketRejected, server.Join(1, Ship()));
        }

        [Test]
        public void TicketsNotRequired_LanServerStaysOpen()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::Hullbreach.Net.ServerSimulation.PresentTicket kind="unit"
            ServerSimulation server = Server(requireTicket: false);
            Assert.IsTrue(server.PresentTicket(1, Id));
            Assert.AreEqual(JoinResult.Joined, server.Join(1, Ship()));
        }

        [Test]
        public void Skin_RoundTrips_AndNoSkinKeepsTheOriginalSeventeenBytes()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/NetMessages.cs::Hullbreach.Net.JoinTicket.TryWrite kind="unit"
            // frob:tests Assets/Scripts/Hullbreach.Net/NetMessages.cs::Hullbreach.Net.JoinTicket.Read kind="unit"
            const string Slug = "hull-skin-deep-sea-camo";
            var buffer = new byte[128];
            var w = new ByteWriter(buffer);
            Assert.IsTrue(new JoinTicket(Id, Slug).TryWrite(ref w));
            Assert.AreEqual(JoinTicket.ByteSize + 1 + Slug.Length, w.Position);
            var r = new ByteReader(buffer, 0, w.Position);
            JoinTicket read = JoinTicket.Read(ref r);
            Assert.IsFalse(r.Failed);
            Assert.AreEqual(Id, read.MatchId);
            Assert.AreEqual(Slug, read.Skin);

            var plain = new byte[JoinTicket.ByteSize];
            var pw = new ByteWriter(plain);
            Assert.IsTrue(new JoinTicket(Id).TryWrite(ref pw));
            Assert.AreEqual(JoinTicket.ByteSize, pw.Position);
            var pr = new ByteReader(plain, 0, pw.Position);
            Assert.AreEqual(string.Empty, JoinTicket.Read(ref pr).Skin);
        }

        [Test]
        public void Skin_BadOnTheWire_IsNotWrittenOrFailsTheReader()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/NetMessages.cs::Hullbreach.Net.JoinTicket.TryWrite kind="unit"
            var buffer = new byte[256];
            var w = new ByteWriter(buffer);
            Assert.IsFalse(new JoinTicket(Id, new string('x', JoinTicket.MaxSkinLength + 1)).TryWrite(ref w));
            Assert.IsFalse(new JoinTicket(Id, "caf\u00e9").TryWrite(ref w));

            byte[] bytes = Wire(Id);
            var padded = new byte[bytes.Length + 3];
            bytes.CopyTo(padded, 0);
            padded[bytes.Length] = 10; // claims 10 skin bytes but only 2 follow
            var r = new ByteReader(padded, 0, padded.Length);
            JoinTicket.Read(ref r);
            Assert.IsTrue(r.Failed);
        }

        [Test]
        public void Skin_IsKeptPerPeer_UnknownIsDefault_AndClearedOnLeave()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::Hullbreach.Net.ServerSimulation.PresentTicket kind="unit"
            ServerSimulation server = Server();
            server.AssignQueueMatch(Id);
            Assert.IsTrue(server.PresentTicket(1, Id, "hull-skin-deep-sea-camo"));
            Assert.IsTrue(server.PresentTicket(2, Id, "hull-skin-from-the-future"));
            Assert.AreEqual(ShipSkins.IdOf("hull-skin-deep-sea-camo"), server.SkinOf(1));
            Assert.AreNotEqual(0, server.SkinOf(1));
            Assert.AreEqual(0, server.SkinOf(2));
            Assert.AreEqual(0, server.SkinOf(3));
            Assert.IsFalse(server.PresentTicket(4, Other, "hull-skin-deep-sea-camo"));
            Assert.AreEqual(0, server.SkinOf(4), "a refused ticket sets no skin");
            server.Leave(1);
            Assert.AreEqual(0, server.SkinOf(1));
        }

        [Test]
        public void ShipSkins_EverySlugRoundTrips_AndLooksAreDistinctFromDefault()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ShipSkins.cs::Hullbreach.Net.ShipSkins.IdOf kind="unit"
            Assert.AreEqual(17, ShipSkins.Count);
            for (byte id = 1; id <= ShipSkins.Count; id++)
            {
                string slug = ShipSkins.SlugOf(id);
                Assert.AreEqual(id, ShipSkins.IdOf(slug));
                Assert.Greater(ShipSkins.LookOf(id).Mix, 0f, slug);
            }
            Assert.AreEqual(0, ShipSkins.IdOf(null));
            Assert.AreEqual(0, ShipSkins.IdOf(""));
            Assert.AreEqual(0, ShipSkins.IdOf("nope"));
            Assert.AreEqual(0f, ShipSkins.LookOf(0).Mix);
            Assert.AreEqual(0f, ShipSkins.LookOf(200).Mix);
            Assert.AreEqual(string.Empty, ShipSkins.SlugOf(200));
        }

        [Test]
        public void AdoptFirstTicket_FirstValidTicketNamesTheMatch()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::Hullbreach.Net.ServerSimulation.PresentTicket kind="unit"
            ServerSimulation server = Server();
            server.AdoptFirstTicket = true;
            Assert.IsFalse(server.IsTicketed(1));
            Assert.IsTrue(server.PresentTicket(1, Id));
            Assert.IsTrue(server.IsTicketed(1));
            Assert.IsTrue(server.PresentTicket(2, Id), "the opponent presents the same id");
            Assert.AreEqual(JoinResult.Joined, server.Join(2, Ship()));
            Assert.IsFalse(server.PresentTicket(3, Other), "a different match is refused once one is named");
            Assert.IsFalse(server.IsTicketed(3));
            Assert.AreEqual(JoinResult.TicketRejected, server.Join(3, Ship()));
        }

        [Test]
        public void AdoptFirstTicket_Off_NoAllocationStillAcceptsNobody()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::Hullbreach.Net.ServerSimulation.PresentTicket kind="unit"
            ServerSimulation server = Server();
            Assert.IsFalse(server.PresentTicket(1, Id));
            Assert.IsFalse(server.IsTicketed(1));
        }

        [Test]
        public void ServerArgs_ReadTheAllocatedMatchId()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/DirectLaunchPlan.cs::Hullbreach.Net.DirectLaunchPlan.Parse kind="unit"
            DirectLaunchPlan ok = DirectLaunchPlan.Parse(new[] { "-hullbreachServer", "-hullbreachMatchId", Id.ToUpperInvariant() }, false);
            Assert.AreEqual(Id, ok.MatchId);
            StringAssert.Contains("match_id=" + Id, ok.Describe());

            DirectLaunchPlan bad = DirectLaunchPlan.Parse(new[] { "-hullbreachServer", "-hullbreachMatchId", "nope" }, false);
            Assert.IsNull(bad.MatchId);
            Assert.AreEqual(1, bad.Warnings.Count);

            Assert.IsNull(DirectLaunchPlan.Parse(new[] { "-hullbreachDirect", "-hullbreachMatchId", Id }, false).MatchId);
        }
    }
}
