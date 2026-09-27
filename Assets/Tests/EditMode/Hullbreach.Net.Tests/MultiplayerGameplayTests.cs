using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;

namespace Hullbreach.Net.Tests
{
    public class MultiplayerGameplayTests
    {
        static ShipSnapshot Ship(params SnapshotBlock[] blocks)
            => new ShipSnapshot(0, 0, blocks, 0, 0, 0, 0, 0, 0);

        static SnapshotBlock Core()
            => new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0);

        [Test]
        public void Building_IsServerAuthorized_AndReplicatesTopology()
        {
            var outbox = new ServerOutbox();
            var server = new ServerSimulation(outbox);
            var replica = new ClientReplica();
            const int peer = 11;

            server.Join(peer, Ship(Core()));
            ApplyOutbox(outbox, replica);
            outbox.Clear();

            Assert.IsFalse(server.TryPlaceBlock(peer, 0, 1, BlockTypes.Hull, 0),
                "flight mode must reject build commands");

            server.SetInput(peer, new InputMessage(peer, 1, 0, 0, InputMessage.BuildModeBit));
            Assert.IsTrue(server.TryPlaceBlock(peer, 0, 1, BlockTypes.Hull, 0));
            ApplyOutbox(outbox, replica);
            outbox.Clear();

            Assert.IsTrue(server.Ships[peer].Grid.Contains(BlockKey.Pack(0, 1)));
            Assert.IsTrue(replica.Ships[peer].Grid.Contains(BlockKey.Pack(0, 1)),
                "the reliable block event must update the remote topology");

            Assert.IsTrue(server.TryRemoveBlock(peer, 0, 1));
            ApplyOutbox(outbox, replica);
            Assert.IsFalse(server.Ships[peer].Grid.Contains(BlockKey.Pack(0, 1)));
            Assert.IsFalse(replica.Ships[peer].Grid.Contains(BlockKey.Pack(0, 1)));
            Assert.IsTrue(server.Ships[peer].Grid.Contains(BlockKey.Pack(0, 0)),
                "the core cannot be removed by a build command");
        }

        [Test]
        public void Ships_CollideOnServer_AndTakeContactDamage()
        {
            var outbox = new ServerOutbox();
            var server = new ServerSimulation(outbox) { TickRate = 50f };
            server.Join(1, Ship(Core()));
            server.Join(2, Ship(Core()));

            var a = server.Ships[1];
            var b = server.Ships[2];
            a.Position = new float2(0f, 0f);
            b.Position = new float2(0.8f, 0f);
            a.Velocity = new float2(5f, 0f);
            b.Velocity = new float2(-5f, 0f);
            server.SetInput(1, InputMessage.FromFloats(1, 1, 0f, 0f, false));
            server.SetInput(2, InputMessage.FromFloats(2, 1, 0f, 0f, false));

            server.Tick();

            Assert.Less(a.Velocity.x, 0f, "the left ship should rebound from the impact");
            Assert.Greater(b.Velocity.x, 0f, "the right ship should rebound from the impact");
            Assert.GreaterOrEqual(math.distance(a.Position, b.Position), 0.99f,
                "the authority must separate overlapping block hulls");
            Assert.IsTrue(a.Grid.TryGet(BlockKey.Pack(0, 0), out var blockA));
            Assert.IsTrue(b.Grid.TryGet(BlockKey.Pack(0, 0), out var blockB));
            Assert.Greater(blockA.Damage, 0);
            Assert.Greater(blockB.Damage, 0);
        }

        [Test]
        public void CannonProjectile_IgnoresOwner_AndDamagesOtherShip()
        {
            var outbox = new ServerOutbox();
            var server = new ServerSimulation(outbox) { TickRate = 50f };
            server.Join(1, Ship(
                Core(),
                new SnapshotBlock(0, 1, BlockTypes.Cannon, 0, 0)));
            server.Join(2, Ship(Core()));

            var shooter = server.Ships[1];
            var target = server.Ships[2];
            shooter.Position = float2.zero;
            target.Position = new float2(0f, 4f);
            server.SetInput(1, InputMessage.FromFloats(1, 1, 0f, 0f, true));
            server.SetInput(2, InputMessage.FromFloats(2, 1, 0f, 0f, false));

            for (int i = 0; i < 12; i++) server.Tick();

            Assert.IsTrue(shooter.Grid.TryGet(BlockKey.Pack(0, 0), out var shooterCore));
            Assert.AreEqual(0, shooterCore.Damage, "a projectile must not hit its owner");
            Assert.IsTrue(target.Grid.TryGet(BlockKey.Pack(0, 0), out var targetCore));
            Assert.Greater(targetCore.Damage, 0, "the server should apply projectile damage to the target");
        }

        static void ApplyOutbox(ServerOutbox outbox, ClientReplica replica)
        {
            foreach (var entry in outbox.Entries)
                replica.ApplyReceived(entry.Bytes, entry.Bytes.Length, 0);
        }
    }
}
