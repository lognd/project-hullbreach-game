using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Net;
using Hullbreach.Ship;
using Hullbreach.World;

namespace Hullbreach.Net.Tests
{
    // The server applies the same ArenaBounds rule a client ShipBody does.
    public class ServerArenaTests
    {
        const int Peer = 1;

        static ShipSnapshot Design(float x, float vx) => new ShipSnapshot(
            0, 0,
            new[]
            {
                new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0),
                new SnapshotBlock(1, 0, BlockTypes.Hull, 0, 0),
            },
            Quantization.PackPosition(x), 0, 0, Quantization.PackPosition(vx), 0, 0);

        [Test]
        public void ShipOutsideTheArena_IsReturnedInsideWithoutDamage()
        {
            var server = new ServerSimulation(new ServerOutbox())
            {
                TickRate = 50f,
                TimeoutSeconds = 60f,
                Arena = new ArenaBounds(float2.zero, radius: 40f),
            };
            server.Join(Peer, Design(x: 45f, vx: 5f));
            var ship = server.Ships[Peer];

            for (int i = 0; i < 50 * 6; i++)
            {
                server.SetInput(Peer, InputMessage.FromFloats((ushort)Peer, (uint)i, 0f, 0f, false));
                server.Tick();
            }

            Assert.LessOrEqual(math.length(ship.LocalToWorld(ship.Grid.Mass.CenterOfMass)), 40f);
            foreach (var kv in ship.Grid.All) Assert.AreEqual(0, kv.Value.Damage);
        }

        [Test]
        public void ServerAndClientShipBody_AgreeOnTheSameSteps()
        {
            var arena = new ArenaBounds(float2.zero, radius: 40f);
            var server = new ServerSimulation(new ServerOutbox()) { TickRate = 50f, TimeoutSeconds = 60f, Arena = arena };
            server.Join(Peer, Design(x: 45f, vx: 5f));

            // The predicting client's body, built from the same design.
            var client = new ShipBody { Arena = arena, Position = new float2(45f, 0f), Velocity = new float2(5f, 0f) };
            client.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core, 0));
            client.Grid.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Hull, 0));
            client.RebuildDerivedViews();

            for (int i = 0; i < 100; i++)
            {
                server.SetInput(Peer, InputMessage.FromFloats((ushort)Peer, (uint)i, 0f, 0f, false));
                server.Tick();
                client.Step(default, 1f / 50f);
            }

            var serverShip = server.Ships[Peer];
            Assert.AreEqual(client.Position.x, serverShip.Position.x, 0.05f);
            Assert.AreEqual(client.Velocity.x, serverShip.Velocity.x, 0.05f);
        }
    }
}
