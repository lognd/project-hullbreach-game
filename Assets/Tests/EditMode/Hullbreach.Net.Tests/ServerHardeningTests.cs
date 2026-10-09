using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Net;
using Hullbreach.Structure;

namespace Hullbreach.Net.Tests
{
    // INV-003 and the server-authority fixes: untrusted designs and inputs,
    // collision-free NetIds, ship removal, fire latch, hit radius.
    public class ServerHardeningTests
    {
        static SnapshotBlock Core() => new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0);
        static SnapshotBlock Hull(int x, int y) => new SnapshotBlock((sbyte)x, (sbyte)y, BlockTypes.Hull, 0, 0);

        static ShipSnapshot Ship(params SnapshotBlock[] blocks)
            => new ShipSnapshot(0, 0, blocks, 0, 0, 0, 0, 0, 0);

        static void Deliver(ServerOutbox outbox, int peer, ClientReplica replica)
        {
            foreach (var e in outbox.Entries)
                if (e.Peer == peer) replica.ApplyReceived(e.Bytes, e.Bytes.Length, 0);
        }

        [Test]
        public void Join_RejectsBadPeerIds_Duplicates_AndLeavesNoShipBehind()
        {
            var outbox = new ServerOutbox();
            var server = new ServerSimulation(outbox);
            Assert.AreEqual(JoinResult.InvalidPeerId, server.Join(0, Ship(Core())));
            Assert.AreEqual(JoinResult.InvalidPeerId, server.Join(-3, Ship(Core())));
            Assert.AreEqual(JoinResult.InvalidPeerId, server.Join(ServerSimulation.MaxPeerId + 1, Ship(Core())));
            Assert.AreEqual(JoinResult.InvalidPeerId, server.Join(70000, Ship(Core())));
            Assert.AreEqual(0, server.Ships.Count);
            Assert.AreEqual(0, outbox.Entries.Count);

            Assert.AreEqual(JoinResult.Joined, server.Join(4, Ship(Core(), Hull(0, 1))));
            var original = server.Ships[4];
            Assert.AreEqual(JoinResult.AlreadyJoined, server.Join(4, Ship(Core())), "a rejoin must not replace the live ship");
            Assert.AreSame(original, server.Ships[4]);
            Assert.AreEqual(2, original.Grid.Count);
        }

        [Test]
        public void Join_RejectsInvalidDesigns()
        {
            var server = new ServerSimulation(new ServerOutbox());
            Assert.AreEqual(JoinResult.InvalidDesign, server.Join(1, Ship()), "no blocks");
            Assert.AreEqual(JoinResult.InvalidDesign, server.Join(1, Ship(Hull(0, 0))), "no core");
            Assert.AreEqual(JoinResult.InvalidDesign, server.Join(1, Ship(Core(), new SnapshotBlock(1, 0, BlockTypes.Core, 0, 0))), "two cores");
            Assert.AreEqual(JoinResult.InvalidDesign, server.Join(1, Ship(Core(), new SnapshotBlock(0, 1, (byte)BlockTypes.Count, 0, 0))), "unknown type");
            Assert.AreEqual(JoinResult.InvalidDesign, server.Join(1, Ship(Core(), Hull(0, 1), Hull(0, 1))), "duplicate cell");
            Assert.AreEqual(JoinResult.InvalidDesign, server.Join(1, Ship(Core(), Hull(5, 5))), "disconnected block");

            var tooMany = new SnapshotBlock[ShipSnapshot.MaxBlocks + 1];
            for (int i = 0; i < tooMany.Length; i++) tooMany[i] = Hull(i % 200 - 100, i / 200);
            tooMany[0] = Core();
            Assert.AreEqual(JoinResult.InvalidDesign, server.Join(1, Ship(tooMany)), "over the block cap");
            Assert.AreEqual(0, server.Ships.Count);
        }

        [Test]
        public void Join_StartsIntact_AndClampsClaimedVelocity()
        {
            var server = new ServerSimulation(new ServerOutbox());
            var damaged = new ShipSnapshot(0, 0,
                new[] { Core(), new SnapshotBlock(0, 1, BlockTypes.Hull, 0, 250) },
                0, 0, 0, short.MaxValue, short.MaxValue, 0);
            Assert.AreEqual(JoinResult.Joined, server.Join(1, damaged));
            var ship = server.Ships[1];
            Assert.IsTrue(ship.Grid.TryGet(BlockKey.Pack(0, 1), out var hull));
            Assert.AreEqual(0, hull.Damage, "damage is server state, never client-supplied");
            Assert.LessOrEqual(math.length(ship.Velocity), DesignValidator.MaxSpawnSpeed + 1e-3f);
        }

        [Test]
        public void InputAxis_MinusOneTwentyEight_ClampsToMinusOne()
        {
            var input = new InputMessage(1, 1, sbyte.MinValue, sbyte.MinValue, 0);
            Assert.AreEqual(-1f, input.ThrustAxisFloat);
            Assert.AreEqual(-1f, input.SteerFloat);
        }

        [Test]
        public void SolverScales_ComeFromTheSharedCalibration()
        {
            Assert.AreEqual(0.06f, StructuralSolver.DefaultLoadScale);
            Assert.AreEqual(40f, StructuralSolver.DefaultMaterialStiffnessScale);
        }

        [Test]
        public void DemoShip_AtFullThrust_DoesNotBreakItself()
        {
            var outbox = new ServerOutbox();
            var server = new ServerSimulation(outbox) { TickRate = 50f };
            server.Join(1, Ship(Core(), Hull(0, 1), new SnapshotBlock(0, -1, BlockTypes.Thruster, 0, 0)));
            outbox.Clear();
            for (int tick = 1; tick <= 100; tick++)
            {
                server.SetInput(1, InputMessage.FromFloats(1, (uint)tick, 1f, 0f, false));
                server.Tick();
            }
            var reliable = outbox.Entries.Where(e => e.Reliable).Select(e => (MessageKind)e.Bytes[0]).ToList();
            CollectionAssert.DoesNotContain(reliable, MessageKind.BlockDestroyed);
            CollectionAssert.DoesNotContain(reliable, MessageKind.BlockDamaged);
        }

        [Test]
        public void FirePress_FollowedByReleaseInTheSameTick_StillFires()
        {
            var server = new ServerSimulation(new ServerOutbox()) { TickRate = 50f };
            server.Join(1, Ship(Core(), new SnapshotBlock(0, 1, BlockTypes.Cannon, 0, 0)));
            server.SetInput(1, InputMessage.FromFloats(1, 1, 0f, 0f, true));
            server.SetInput(1, InputMessage.FromFloats(1, 2, 0f, 0f, false)); // release lands before the tick
            server.Tick();
            Assert.AreEqual(1, server.Projectiles.Count, "the press edge must survive the following release");
        }

        [Test]
        public void StaleInput_DoesNotOverwriteNewerInput()
        {
            var server = new ServerSimulation(new ServerOutbox());
            server.Join(1, Ship(Core()));
            server.SetInput(1, new InputMessage(1, 10, 0, 0, 0));
            server.SetInput(1, new InputMessage(1, 3, 0, 0, InputMessage.BuildModeBit)); // delayed packet
            Assert.IsFalse(server.TryPlaceBlock(1, 0, 1, BlockTypes.Hull, 0), "the old build-mode packet must be ignored");
        }

        [Test]
        public void SplitWithTwoPeers_FragmentIdNeverCollidesWithAPeerShip()
        {
            var outbox = new ServerOutbox();
            var server = new ServerSimulation(outbox) { TickRate = 50f };
            var replica = new ClientReplica();
            server.Join(1, Ship(Core(), Hull(0, 1), Hull(0, 2)));
            server.Join(2, new ShipSnapshot(0, 0, new[] { Core(), Hull(0, 1) }, Quantization.PackPosition(40f), 0, 0, 0, 0, 0));
            server.DebugDestroyBlock(1, 0, 1);
            Deliver(outbox, 1, replica);

            Assert.IsTrue(replica.Ships.ContainsKey(1));
            Assert.IsTrue(replica.Ships.ContainsKey(2));
            Assert.IsTrue(replica.Ships[1].Grid.Contains(BlockKey.Pack(0, 0)), "the peer ship must survive the fragment spawn");
            Assert.IsTrue(replica.Ships[2].Grid.Contains(BlockKey.Pack(0, 1)), "the other peer ship must be untouched");
            var fragments = replica.Ships.Keys.Where(id => id != 1 && id != 2).ToList();
            Assert.AreEqual(1, fragments.Count);
            Assert.GreaterOrEqual(fragments[0], ServerSimulation.FirstFragmentId);
        }

        [Test]
        public void Leave_BroadcastsShipRemoved_AndTimeoutSurfacesTheDroppedPeer()
        {
            var outbox = new ServerOutbox();
            var server = new ServerSimulation(outbox) { TickRate = 50f, TimeoutSeconds = 0.1f };
            var watcher = new ClientReplica();
            server.Join(1, Ship(Core()));
            server.Join(2, new ShipSnapshot(0, 0, new[] { Core() }, Quantization.PackPosition(40f), 0, 0, 0, 0, 0));
            Deliver(outbox, 1, watcher);
            outbox.Clear();
            Assert.IsTrue(watcher.Ships.ContainsKey(2));

            server.Leave(2);
            Deliver(outbox, 1, watcher);
            Assert.IsFalse(watcher.Ships.ContainsKey(2), "remote replicas must drop a ship that left");
            outbox.Clear();

            for (int i = 0; i < 10; i++) server.Tick();
            Assert.IsFalse(server.Ships.ContainsKey(1));
            Assert.IsTrue(server.TryDequeueDroppedPeer(out int dropped));
            Assert.AreEqual(1, dropped);
            Assert.IsFalse(server.TryDequeueDroppedPeer(out _));
        }

        [Test]
        public void ProjectileHit_HonorsRadius_ForNearMisses()
        {
            var ship = new Hullbreach.Ship.ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core, 0, 0));
            ship.RebuildDerivedViews();

            // Centre 0.2 outside the cell [0,1]x[0,1].
            var point = new float2(1.2f, 0.5f);
            Assert.IsFalse(ProjectileHitTest.TryHitShipCell(ship, point, 0f, out _), "a point misses");
            Assert.IsFalse(ProjectileHitTest.TryHitShipCell(ship, point, 0.1f, out _));
            Assert.IsTrue(ProjectileHitTest.TryHitShipCell(ship, point, 0.25f, out int key));
            Assert.AreEqual(BlockKey.Pack(0, 0), key);
        }
    }
}
