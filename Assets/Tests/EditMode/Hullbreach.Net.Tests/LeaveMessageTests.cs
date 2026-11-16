using System.Collections.Generic;
using Hullbreach.Core;
using Hullbreach.Match;
using Hullbreach.Platform;
using Hullbreach.Ship;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hullbreach.Net.Tests
{
    // A deliberate leave (Back to lobby, Quit) is told to the server, which frees the ship at once; a bare drop still gets
    // the reconnect grace. The address guard keeps a stranger from claiming a held ship whose owner has no identity.
    public class LeaveMessageTests
    {
        static ShipSnapshot Ship() => new ShipSnapshot(0, 0, new[]
        {
            new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0), new SnapshotBlock(0, 1, BlockTypes.Cannon, 0, 0),
        }, 0, 0, 0, 0, 0, 0);

        static (ServerSimulation server, List<MatchEndedArgs> ended) Arena()
        {
            var feed = new MatchEndFeed();
            var ended = new List<MatchEndedArgs>();
            feed.Ended += ended.Add;
            var server = new ServerSimulation(new ServerOutbox()) { TickRate = 50f, TimeoutSeconds = 1e6f, MatchFeed = feed, ReconnectGraceSeconds = 20f };
            server.Join(1, Ship());
            server.Join(2, Ship());
            Assert.AreEqual(MatchPhase.Running, server.Match.Phase);
            return (server, ended);
        }

        static byte[] LeaveBytes()
        {
            var buf = new byte[LeaveMessage.ByteSize];
            var w = new ByteWriter(buf);
            new LeaveMessage().Write(ref w);
            return buf;
        }

        [Test]
        public void LeaveMessage_RoundTrips_AsOneByte()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/NetMessages.cs::Hullbreach.Net.LeaveMessage.Read kind="unit"
            byte[] buf = LeaveBytes();
            Assert.AreEqual(1, buf.Length);
            Assert.AreEqual((byte)MessageKind.Leave, buf[0]);
            Assert.AreEqual(21, buf[0], "the wire value is part of the protocol");
            var r = new ByteReader(buf, 0, buf.Length);
            LeaveMessage.Read(ref r);
            Assert.IsFalse(r.Failed);
        }

        [Test]
        public void LeaveMessage_WrongKindOrEmpty_FailsTheReader()
        {
            var r = new ByteReader(new byte[] { (byte)MessageKind.ReadyUp }, 0, 1);
            LeaveMessage.Read(ref r);
            Assert.IsTrue(r.Failed);
            var empty = new ByteReader(new byte[0], 0, 0);
            LeaveMessage.Read(ref empty);
            Assert.IsTrue(empty.Failed);
        }

        [Test]
        public void LeaveMessage_TrailingBytes_AreIgnored_SoALaterVersionCanAppendFields()
        {
            var buf = new byte[] { (byte)MessageKind.Leave, 9, 9 };
            var r = new ByteReader(buf, 0, buf.Length);
            LeaveMessage.Read(ref r);
            Assert.IsFalse(r.Failed);
            var (server, _) = Arena();
            Assert.IsTrue(HostInbox.Dispatch(server, 1, buf, buf.Length));
            Assert.IsFalse(server.Ships.ContainsKey(1));
        }

        [Test]
        public void Dispatch_Leave_RemovesTheShipImmediately_WithoutAHold()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/HostInbox.cs::Hullbreach.Net.HostInbox.Dispatch kind="unit"
            var (server, ended) = Arena();
            byte[] buf = LeaveBytes();
            Assert.IsTrue(HostInbox.Dispatch(server, 1, buf, buf.Length));
            Assert.IsFalse(server.Ships.ContainsKey(1));
            Assert.IsFalse(server.IsHeld(1));
            Assert.AreEqual(MatchResult.Forfeit, server.Match.Result, "the opponent is told at once");
            Assert.AreEqual(1, ended.Count);
        }

        [Test]
        public void Dispatch_Leave_MidGrace_RemovesTheHeldShip_AndForfeitsAtOnce()
        {
            var (server, ended) = Arena();
            Assert.IsTrue(server.Disconnect(1));
            for (int i = 0; i < 50; i++) server.Tick(); // 1 s into the 20 s window
            Assert.IsTrue(server.IsHeld(1));
            Assert.AreEqual(0, ended.Count);

            byte[] buf = LeaveBytes();
            Assert.IsTrue(HostInbox.Dispatch(server, 1, buf, buf.Length));
            Assert.IsFalse(server.Ships.ContainsKey(1));
            Assert.IsFalse(server.IsHeld(1));
            Assert.AreEqual(MatchResult.Forfeit, server.Match.Result);
            Assert.AreEqual(1, ended.Count);
        }

        [Test]
        public void ABareDrop_StillHoldsTheShip()
        {
            var (server, ended) = Arena();
            Assert.IsTrue(server.Disconnect(1));
            Assert.IsTrue(server.IsHeld(1));
            Assert.IsTrue(server.Ships.ContainsKey(1));
            Assert.AreEqual(0, ended.Count);
        }

        [Test]
        public void LeaveThenTheTransportClose_IsHarmless()
        {
            var (server, ended) = Arena();
            byte[] buf = LeaveBytes();
            HostInbox.Dispatch(server, 1, buf, buf.Length);
            Assert.IsFalse(server.Disconnect(1), "the close that follows a leave finds nothing to hold");
            Assert.AreEqual(1, ended.Count, "and does not end the match twice");
        }

        [Test]
        public void Leave_FromAPeerThatNeverJoined_IsANoOp_AndALeaveIsOnlyFromTheSender()
        {
            var (server, _) = Arena();
            byte[] buf = LeaveBytes();
            Assert.IsTrue(HostInbox.Dispatch(server, 9, buf, buf.Length));
            Assert.IsTrue(server.Ships.ContainsKey(1));
            Assert.IsTrue(server.Ships.ContainsKey(2), "the sender is the transport's peer id, never the message");
        }

        [Test]
        public void BackwardCompat_AnOldClient_NeverSendsLeave_AndStillGetsTheGrace()
        {
            // An old client just closes: the host calls Disconnect, as before this message existed.
            var (server, _) = Arena();
            server.Disconnect(2);
            Assert.IsTrue(server.IsHeld(2));
        }

        [Test]
        public void BackwardCompat_AnUnknownKind_IsStillRejected_SoAnOldServerIgnoresLeave()
        {
            var (server, _) = Arena();
            var junk = new byte[] { 200 };
            Assert.IsFalse(HostInbox.Dispatch(server, 1, junk, 1));
            Assert.IsTrue(server.Ships.ContainsKey(1));
            Assert.AreEqual(21, (int)MessageKind.Leave);
            Assert.AreEqual(20, (int)MessageKind.PlacementResult, "existing values did not move");
        }

        // ---- the join-time address guard ----

        [Test]
        public void HeldShipWithNoIdentity_IsNotClaimedFromADifferentAddress()
        {
            var (server, _) = Arena();
            server.SetRemoteAddress(1, "10.0.0.5");
            server.SetRemoteAddress(2, "10.0.0.6");
            ShipBody held = server.Ships[1];
            server.Disconnect(1);
            server.SetRemoteAddress(7, "10.0.0.99");
            server.Join(7, Ship());
            Assert.AreSame(held, server.Ships[1], "the held ship is still held, not handed to a stranger");
            Assert.IsTrue(server.IsHeld(1));
            if (server.Ships.TryGetValue(7, out ShipBody other)) Assert.AreNotSame(held, other);
        }

        [Test]
        public void HeldShipWithNoIdentity_IsReclaimedFromTheSameAddress()
        {
            var (server, _) = Arena();
            server.SetRemoteAddress(1, "10.0.0.5");
            ShipBody held = server.Ships[1];
            server.Disconnect(1);
            server.SetRemoteAddress(7, "10.0.0.5");
            Assert.AreEqual(JoinResult.Joined, server.Join(7, Ship()));
            Assert.AreSame(held, server.Ships[7]);
            Assert.IsFalse(server.Ships.ContainsKey(1));
        }

        [Test]
        public void AddressGuard_DoesNotApply_WhenEitherAddressIsUnknown()
        {
            var (server, _) = Arena();
            ShipBody held = server.Ships[1];
            server.Disconnect(1); // no addresses recorded at all: the old anonymous rule
            Assert.AreEqual(JoinResult.Joined, server.Join(7, Ship()));
            Assert.AreSame(held, server.Ships[7]);
        }

        [Test]
        public void AddressGuard_YieldsToAKnownIdentity()
        {
            var (server, _) = Arena();
            server.SetIdentity(1, "user-a");
            server.SetRemoteAddress(1, "10.0.0.5");
            ShipBody held = server.Ships[1];
            server.Disconnect(1);
            server.SetIdentity(7, "user-a");
            server.SetRemoteAddress(7, "10.0.0.77"); // a roaming player: the identity wins
            Assert.AreEqual(JoinResult.Joined, server.Join(7, Ship()));
            Assert.AreSame(held, server.Ships[7]);
        }

        [Test]
        public void RemoteAddress_HostOf_StripsThePort()
        {
            Assert.AreEqual("1.2.3.4", RemoteAddress.HostOf("1.2.3.4:7979"));
            Assert.AreEqual("1.2.3.4", RemoteAddress.HostOf("1.2.3.4"));
            Assert.AreEqual("::1", RemoteAddress.HostOf("[::1]:7979"));
            Assert.AreEqual("fe80::1", RemoteAddress.HostOf("fe80::1"));
            Assert.IsNull(RemoteAddress.HostOf(null));
            Assert.IsNull(RemoteAddress.HostOf(""));
            Assert.IsNull(RemoteAddress.HostOf(":7979"));
        }
    }
}
