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
