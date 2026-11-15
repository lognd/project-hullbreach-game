using Hullbreach.Core;
using Hullbreach.Platform;
using NUnit.Framework;

namespace Hullbreach.Net.Tests
{
    // Pins the mid-match build cooldown (S34-3): building during a fight costs time, building before it is free.
    // frob:ticket 01M3DG5Y25YGZ3DV7JSDB4BNMF
    public class BuildCooldownTests
    {
        static SnapshotBlock Core() => new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0);

        static ShipSnapshot Ship() => new ShipSnapshot(0, 0, new[] { Core() }, 0, 0, 0, 0, 0, 0);

        static ServerSimulation Server(float cooldown, bool live)
        {
            var server = new ServerSimulation(new ServerOutbox()) { TickRate = 50f, BuildCooldownSeconds = cooldown, MatchFeed = new MatchEndFeed() };
            server.Join(1, Ship());
            if (live) server.Join(2, Ship());
            Assert.AreEqual(live, server.Match != null);
            // Keep both peers alive and in build mode; the tick loop times out peers without input.
            return server;
        }

        static void Build(ServerSimulation server, int peer, uint tick) =>
            server.SetInput(peer, new InputMessage((ushort)peer, tick, 0, 0, InputMessage.BuildModeBit));

        static void Pump(ServerSimulation server, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                Build(server, 1, (uint)i);
                Build(server, 2, (uint)i);
                server.Tick();
            }
        }

        [Test]
        public void LiveMatch_SecondPlacementWaitsForTheCooldown()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::Hullbreach.Net.ServerSimulation.TryPlaceBlock kind="unit"
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::Hullbreach.Net.ServerSimulation.BuildCooldownRemaining kind="unit"
            ServerSimulation server = Server(cooldown: 1.0f, live: true);
            Build(server, 1, 1);
            Assert.IsTrue(server.TryPlaceBlock(1, 0, 1, BlockTypes.Hull, 0));
            Assert.AreEqual(1.0f, server.BuildCooldownRemaining(1), 0.001f);
            Assert.IsFalse(server.TryPlaceBlock(1, 0, 2, BlockTypes.Hull, 0), "inside the cooldown");
            Assert.AreEqual(1.0f, server.BuildCooldownRemaining(1), 0.001f, "a refused placement does not extend the wait");

            Pump(server, 49);
            Assert.IsFalse(server.TryPlaceBlock(1, 0, 2, BlockTypes.Hull, 0), "one tick short");
            Pump(server, 1);
            Assert.AreEqual(0f, server.BuildCooldownRemaining(1));
            Assert.IsTrue(server.TryPlaceBlock(1, 0, 2, BlockTypes.Hull, 0));
        }

        [Test]
        public void CooldownIsPerPeer_AndRefusalDoesNotDistrust()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::Hullbreach.Net.ServerSimulation.TryPlaceBlock kind="unit"
            ServerSimulation server = Server(cooldown: 1.0f, live: true);
            Build(server, 1, 1);
            Build(server, 2, 1);
            Assert.IsTrue(server.TryPlaceBlock(1, 0, 1, BlockTypes.Hull, 0));
            float trust = server.TrustOf(1);
            for (int i = 0; i < 10; i++) Assert.IsFalse(server.TryPlaceBlock(1, 0, 2, BlockTypes.Hull, 0));
            Assert.AreEqual(trust, server.TrustOf(1));
            Assert.IsTrue(server.TryPlaceBlock(2, 0, 1, BlockTypes.Hull, 0), "the other peer is unaffected");
        }

        [Test]
        public void BeforeTheMatch_BuildingIsNotLimited()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::Hullbreach.Net.ServerSimulation.TryPlaceBlock kind="unit"
            ServerSimulation server = Server(cooldown: 1.0f, live: false);
            Build(server, 1, 1);
            Assert.IsTrue(server.TryPlaceBlock(1, 0, 1, BlockTypes.Hull, 0));
            Assert.IsTrue(server.TryPlaceBlock(1, 0, 2, BlockTypes.Hull, 0));
            Assert.AreEqual(0f, server.BuildCooldownRemaining(1));
        }

        [Test]
        public void ZeroCooldown_DisablesTheLimit()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::Hullbreach.Net.ServerSimulation.TryPlaceBlock kind="unit"
            ServerSimulation server = Server(cooldown: 0f, live: true);
            Build(server, 1, 1);
            Assert.IsTrue(server.TryPlaceBlock(1, 0, 1, BlockTypes.Hull, 0));
            Assert.IsTrue(server.TryPlaceBlock(1, 0, 2, BlockTypes.Hull, 0));
        }

        [Test]
        public void UnknownPeer_HasNoCooldown()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::Hullbreach.Net.ServerSimulation.BuildCooldownRemaining kind="unit"
            Assert.AreEqual(0f, Server(1.0f, true).BuildCooldownRemaining(99));
        }
    }
}
