using System.Collections.Generic;
using NUnit.Framework;
using Hullbreach.Core;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    // Drives ServerHost over LoopbackTransport: the headless loop of S47, no Unity.
    public class ServerHostTests
    {
        const float TickRate = NetRig.TickRate;

        // Well above rest, well below what a second of full thrust reaches.
        const float MovedSpeed = 0.01f;

        LoopbackTransport _hub;
        ITransport _serverEndpoint;
        ITransport _clientA;
        ITransport _clientB;
        int _serverId, _idA, _idB;
        ServerHost _host;
        List<string> _log;

        static ShipSnapshot Design(SnapshotBlock[] blocks) => new ShipSnapshot(0, 0, blocks, 0, 0, 0, 0, 0, 0);

        static readonly SnapshotBlock[] Rocket =
        {
            new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0),
            new SnapshotBlock(0, 1, BlockTypes.Hull, 0, 0),
            new SnapshotBlock(0, -1, BlockTypes.Thruster, 0, 0),
        };

        [SetUp]
        public void SetUp()
        {
            _log = new List<string>();
            NetLog.Sink = _log.Add;
            // The tests read the client endpoints themselves, so the rig must not pump them into replicas.
            var rig = new NetRig(LinkProfile.Ideal, seed: 3, pumpClients: false);
            _hub = rig.Hub;
            _serverEndpoint = rig.ServerEndpoint;
            _clientA = rig.ClientA;
            _clientB = rig.ClientB;
            _serverId = rig.ServerId;
            _idA = rig.IdA;
            _idB = rig.IdB;
            _host = rig.Host;
        }

        [TearDown]
        public void TearDown() => NetLog.Sink = null;

        static void Send(ITransport from, int to, byte[] payload) => from.SendUnreliable(to, payload);

        static byte[] InputBytes(ushort netId, float thrust) => NetRig.InputBytes(netId, 0, thrust, 0f);

        // Plays `ticks` fixed ticks of host time; `perTick` is where clients send their input (ServerSimulation
        // applies an input for one tick only, so a held key means one message per tick).
        void PlayTicks(int ticks, System.Action perTick = null)
        {
            for (int i = 0; i < ticks; i++)
            {
                perTick?.Invoke();
                _hub.Tick();
                _host.Advance(1.0 / TickRate);
            }
        }

        // Delivers everything in flight and returns the MessageKind byte of each payload client `c` received.
        List<MessageKind> Drain(ITransport c)
        {
            _hub.Tick();
            var kinds = new List<MessageKind>();
            var buf = new byte[2048];
            while (c.TryReceive(out _, buf, out int length)) kinds.Add((MessageKind)buf[0]);
            return kinds;
        }

        [Test]
        public void Advance_RunsOneFixedTickPerTickInterval()
        {
            _host.Join(_idA, Design(Rocket));

            for (int i = 0; i < 10; i++) Assert.AreEqual(5, _host.Advance(0.1));
            Assert.AreEqual(50, _host.TicksRun, "exactly N * TickRate ticks for N seconds, however the time is chunked");

            Assert.AreEqual(0, _host.Advance(0.019), "less than one interval is not a tick");
            Assert.AreEqual(1, _host.Advance(0.001), "the remainder completes the interval");
        }

        [Test]
        public void Advance_StepsEveryShipBodyEachTick()
        {
            _host.Join(_idA, Design(Rocket));
            _host.Join(_idB, Design(Rocket));

            PlayTicks(50, () => Send(_clientA, _serverId, InputBytes((ushort)_idA, 1f)));

            var ships = _host.Simulation.Ships;
            Assert.Greater(ships[_idA].Velocity.y, MovedSpeed, "peer A held full thrust, so its body must have accelerated");
            Assert.AreEqual(0f, ships[_idB].Velocity.y, 1e-4f, "peer B sent no input, so its body only coasts");
        }

        [Test]
        public void Advance_ReturnsSnapshotsThenPoseUpdatesOverTheTransport()
        {
            _host.Join(_idA, Design(Rocket));
            _host.Join(_idB, Design(Rocket));

            var first = Drain(_clientA);
            CollectionAssert.AreEqual(new[] { MessageKind.ShipSnapshot, MessageKind.ShipSnapshot }, first,
                "each client gets one snapshot per ship, its own included");

            PlayTicks(5);
            var poses = Drain(_clientA);
            // ServerSimulation sends each ship's pose to its owner only today; see the ticket filed from this work.
            Assert.AreEqual(5, poses.Count, "one ShipState per tick for the client's own ship");
            Assert.That(poses, Is.All.EqualTo(MessageKind.ShipState));
        }

        [Test]
        public void Input_IsAppliedToTheSendersShipEvenWhenItClaimsAnotherNetId()
        {
            _host.Join(_idA, Design(Rocket));
            _host.Join(_idB, Design(Rocket));

            // Peer B lies about its identity: the transport peer id must win.
            PlayTicks(50, () => Send(_clientB, _serverId, InputBytes((ushort)_idA, 1f)));

            var ships = _host.Simulation.Ships;
            Assert.Greater(ships[_idB].Velocity.y, MovedSpeed);
            Assert.AreEqual(0f, ships[_idA].Velocity.y, 1e-4f);
        }

        [Test]
        public void Advance_TicksTheStructuralSolverSoAnOverloadedShipTakesDamage()
        {
            // A long hull spar with the core at one end and the engine at the other: the thrust must bend it.
            var blocks = new List<SnapshotBlock> { new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0) };
            for (int y = 1; y <= 12; y++) blocks.Add(new SnapshotBlock(0, (sbyte)y, BlockTypes.Hull, 0, 0));
            blocks.Add(new SnapshotBlock(0, 13, BlockTypes.Thruster, 0, 0));
            _host.Join(_idA, Design(blocks.ToArray()));
            Drain(_clientA);

            int damageEvents = 0;
            PlayTicks(250, () =>
            {
                Send(_clientA, _serverId, InputBytes((ushort)_idA, 1f));
            });
            foreach (var kind in Drain(_clientA))
                if (kind == MessageKind.BlockDamaged || kind == MessageKind.BlockDestroyed) damageEvents++;

            Assert.Greater(damageEvents, 0, "only StructuralSolver.Tick can produce damage here; none means it never ran");
        }

        [Test]
        public void Advance_BoundsCatchUpAfterAStall()
        {
            _host.Join(_idA, Design(Rocket));

            Assert.AreEqual(ServerHost.MaxCatchUpTicks, _host.Advance(60.0));
            Assert.AreEqual(0, _host.Advance(0.0), "skipped ticks are forgotten, not replayed later");
            StringAssert.Contains("stalled", _log[_log.Count - 1]);
        }

        [Test]
        public void MalformedPayloads_AreDroppedAndLoggedWithoutStoppingTheLoop()
        {
            _host.Join(_idA, Design(Rocket));

            Send(_clientA, _serverId, new byte[0]);
            Send(_clientA, _serverId, new byte[] { (byte)MessageKind.Input, 1, 2 });
            Send(_clientA, _serverId, new byte[] { 250, 0, 0 });
            Send(_clientA, _serverId, new byte[ShipState.ByteSize] { (byte)MessageKind.ShipState, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });

            Assert.DoesNotThrow(() => PlayTicks(1));
            PlayTicks(50, () => Send(_clientA, _serverId, InputBytes((ushort)_idA, 1f)));

            Assert.Greater(_host.Simulation.Ships[_idA].Velocity.y, MovedSpeed, "valid input after the garbage still applied");
            Assert.AreEqual(4, _log.FindAll(m => m.Contains("dropped")).Count, "each bad payload is logged once");
        }

        [Test]
        public void Run_TicksUntilToldToStop_UsingOnlyTheInjectedClock()
        {
            _host.Join(_idA, Design(Rocket));
            double now = 0.0;

            _host.Run(
                clockSeconds: () => now,
                sleepSeconds: s => now += s,
                shouldStop: () => now >= 1.0);

            Assert.That(_host.TicksRun, Is.InRange(48, 50), "one second at 50 Hz, give or take the last interval");
        }
    }
}
