using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;

namespace Hullbreach.Net.Tests
{
    // A late or dropped input packet must not cut the thrust for a tick: the
    // server holds the last axes briefly, then lets go.
    public class ServerInputHoldTests
    {
        const int Peer = 5;

        static ServerSimulation ServerWithThruster()
        {
            var server = new ServerSimulation(new ServerOutbox()) { TickRate = 50f, EmitPoseMessages = false };
            server.Join(Peer, new ShipSnapshot(0, 0, new[]
            {
                new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0),
                new SnapshotBlock(0, -1, BlockTypes.Thruster, 0, 0),
            }, 0, 0, 0, 0, 0, 0));
            return server;
        }

        [Test]
        public void SingleInput_ThrustsOnTheTicksAfterIt_UntilTheHoldExpires()
        {
            var server = ServerWithThruster();
            server.SetInput(Peer, InputMessage.FromFloats(Peer, 1, 1f, 0f, false));

            server.Tick();
            float speedAfter1 = math.length(server.Ships[Peer].Velocity);
            server.Tick();
            float speedAfter2 = math.length(server.Ships[Peer].Velocity);

            Assert.Greater(speedAfter1, 0f);
            Assert.Greater(speedAfter2, speedAfter1, "the second tick still has no new packet but must keep thrusting");
        }

        [Test]
        public void AfterTheHoldWindow_TheThrottleRampsDown()
        {
            var server = ServerWithThruster();
            server.SetInput(Peer, InputMessage.FromFloats(Peer, 1, 1f, 0f, false));

            int ticks = (int)(ServerSimulation.InputHoldSeconds * 50f) + 2;
            for (int i = 0; i < ticks; i++) server.Tick();

            Assert.Greater(server.Ships[Peer].Throttle(BlockKey.Pack(0, -1)), 0f);
            for (int i = 0; i < 200; i++) server.Tick();
            Assert.AreEqual(0f, server.Ships[Peer].Throttle(BlockKey.Pack(0, -1)), 1e-4f,
                "a long-silent client must stop thrusting");
        }

        [Test]
        public void NewInput_ReplacesTheHeldOne()
        {
            var server = ServerWithThruster();
            server.SetInput(Peer, InputMessage.FromFloats(Peer, 1, 1f, 0f, false));
            server.Tick();
            server.SetInput(Peer, InputMessage.FromFloats(Peer, 2, 0f, 0f, false));
            for (int i = 0; i < 100; i++) server.Tick();

            Assert.AreEqual(0f, server.Ships[Peer].Throttle(BlockKey.Pack(0, -1)), 1e-4f);
        }
    }
}
