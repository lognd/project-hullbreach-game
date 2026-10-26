using NUnit.Framework;

namespace Hullbreach.Match.Tests
{
    // The ready gate starts only when both peers are present and ready, and survives a disconnect.
    public sealed class ReadyGateTests
    {
        static ReadyGate Full()
        {
            var gate = new ReadyGate();
            gate.AddPeer(1);
            gate.AddPeer(2);
            return gate;
        }

        [Test]
        public void OneReadyPeer_DoesNotStart()
        {
            var gate = Full();
            Assert.AreEqual(ReadyResult.Accepted, gate.SetReady(1, true));
            Assert.AreEqual(ReadyGateState.Waiting, gate.State);
            Assert.IsTrue(gate.IsReady(1));
            Assert.IsFalse(gate.IsReady(2));
        }

        [Test]
        public void BothReady_Starts()
        {
            var gate = Full();
            gate.SetReady(1, true);
            gate.SetReady(2, true);
            Assert.AreEqual(ReadyGateState.Started, gate.State);
        }

        [Test]
        public void UnreadyingBeforeTheOtherPeer_PreventsTheStart()
        {
            var gate = Full();
            gate.SetReady(1, true);
            gate.SetReady(1, false);
            gate.SetReady(2, true);
            Assert.AreEqual(ReadyGateState.Waiting, gate.State);
        }

        [Test]
        public void ReadyFromAnUnknownPeer_IsRejected()
        {
            var gate = Full();
            Assert.AreEqual(ReadyResult.UnknownPeer, gate.SetReady(9, true));
            Assert.AreEqual(ReadyGateState.Waiting, gate.State);
        }

        [Test]
        public void LoneReadyPeer_NeverStartsAlone()
        {
            var gate = new ReadyGate();
            gate.AddPeer(1);
            gate.SetReady(1, true);
            Assert.AreEqual(ReadyGateState.Waiting, gate.State);
        }

        [Test]
        public void DisconnectBeforeReady_DropsThePeer_AndTheGateRefills()
        {
            var gate = Full();
            gate.SetReady(1, true);
            Assert.IsTrue(gate.RemovePeer(2));
            Assert.AreEqual(1, gate.PeerCount);
            Assert.AreEqual(ReadyGateState.Waiting, gate.State);
            Assert.IsTrue(gate.AddPeer(3));
            Assert.IsFalse(gate.IsReady(3));
            gate.SetReady(3, true);
            Assert.AreEqual(ReadyGateState.Started, gate.State, "peer 1 stayed ready across the leave");
        }

        [Test]
        public void ReadyPeerLeaving_LosesItsReadyFlag()
        {
            var gate = Full();
            gate.SetReady(1, true);
            gate.RemovePeer(1);
            gate.AddPeer(1);
            Assert.IsFalse(gate.IsReady(1));
            gate.SetReady(2, true);
            Assert.AreEqual(ReadyGateState.Waiting, gate.State);
        }

        [Test]
        public void Started_IsFinal()
        {
            var gate = Full();
            gate.SetReady(1, true);
            gate.SetReady(2, true);
            Assert.AreEqual(ReadyResult.AlreadyStarted, gate.SetReady(1, false));
            Assert.IsFalse(gate.RemovePeer(1));
            Assert.IsFalse(gate.AddPeer(3));
            Assert.AreEqual(ReadyGateState.Started, gate.State);
        }

        [Test]
        public void AddingAPeerTwice_IsRefused()
        {
            var gate = new ReadyGate();
            Assert.IsTrue(gate.AddPeer(1));
            Assert.IsFalse(gate.AddPeer(1));
            Assert.AreEqual(1, gate.PeerCount);
        }
    }
}
