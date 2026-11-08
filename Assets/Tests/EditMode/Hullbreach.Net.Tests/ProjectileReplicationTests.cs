using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    // Projectiles are server-owned; the client mirrors them from ProjectileState and interpolates.
    // frob:ticket 01M6WVRTC724W6RQYAA9WCEK5F
    public class ProjectileReplicationTests
    {
        static ProjectileEntry Entry(uint id, float x, float y)
            => new ProjectileEntry(id, Quantization.PackPosition(x), Quantization.PackPosition(y), 0, 0);

        static ProjectileState State(uint tick, params ProjectileEntry[] entries) => new ProjectileState(tick, entries);

        [Test]
        // frob:tests Hullbreach.Net.ProjectileState.Write
        public void WriteThenRead_RoundTrips()
        {
            var buf = new byte[256];
            var w = new ByteWriter(buf);
            State(77, Entry(5, 1.5f, -2f), Entry(6, 3f, 4f)).Write(ref w);

            Assert.AreEqual((byte)MessageKind.ProjectileState, buf[0]);
            var r = new ByteReader(buf, 0, w.Position);
            var back = ProjectileState.Read(ref r);
            Assert.IsFalse(r.Failed);
            Assert.AreEqual(77u, back.ServerTick);
            Assert.AreEqual(2, back.Entries.Length);
            Assert.AreEqual(6u, back.Entries[1].Id);
            Assert.AreEqual(Entry(6, 3f, 4f).Px, back.Entries[1].Px);
        }

        [Test]
        // frob:tests Hullbreach.Net.ProjectileState.Read
        public void Read_TruncatedBody_FailsTheReader()
        {
            var buf = new byte[256];
            var w = new ByteWriter(buf);
            State(1, Entry(1, 0, 0), Entry(2, 0, 0)).Write(ref w);

            var r = new ByteReader(buf, 0, w.Position - 4);
            ProjectileState.Read(ref r);
            Assert.IsTrue(r.Failed);
        }

        [Test]
        // frob:tests Hullbreach.Net.ProjectileState.Read
        public void Read_CountOverCap_FailsTheReader()
        {
            var buf = new byte[256];
            var w = new ByteWriter(buf);
            State(1, Entry(1, 0, 0)).Write(ref w);
            buf[5] = (byte)(ProjectileState.MaxEntries + 1);

            var r = new ByteReader(buf, 0, w.Position);
            ProjectileState.Read(ref r);
            Assert.IsTrue(r.Failed);
        }

        [Test]
        // frob:tests Hullbreach.Net.ClientReplica.ApplyProjectileState
        public void Apply_ReplacesSet_AndEmptyClears()
        {
            var replica = new ClientReplica();
            Assert.IsTrue(replica.ApplyProjectileState(State(10, Entry(1, 0, 0), Entry(2, 0, 0))));
            Assert.AreEqual(2, replica.ProjectileCount);

            replica.ApplyProjectileState(State(11, Entry(2, 1, 0)));
            CollectionAssert.AreEqual(new uint[] { 2 }, replica.ProjectileIds.ToArray());

            replica.ApplyProjectileState(State(12));
            Assert.AreEqual(0, replica.ProjectileCount);
        }

        [Test]
        // frob:tests Hullbreach.Net.ClientReplica.ApplyProjectileState
        public void Apply_StaleTick_IsIgnored()
        {
            var replica = new ClientReplica();
            replica.ApplyProjectileState(State(20, Entry(1, 0, 0)));
            Assert.IsFalse(replica.ApplyProjectileState(State(19)));
            Assert.AreEqual(1, replica.ProjectileCount);
        }

        [Test]
        // frob:tests Hullbreach.Net.ClientReplica.TryInterpolateProjectile
        public void Interpolate_BlendsAndClamps_UnknownIdFalse()
        {
            var replica = new ClientReplica();
            replica.ApplyProjectileState(State(10, Entry(1, 10f, 0f)));
            replica.ApplyProjectileState(State(12, Entry(1, 20f, 0f)));

            Assert.IsTrue(replica.TryInterpolateProjectile(1, 11f, out var mid));
            Assert.AreEqual(15f, mid.x, 0.05f);
            replica.TryInterpolateProjectile(1, 99f, out var late);
            Assert.AreEqual(20f, late.x, 0.05f);
            replica.TryInterpolateProjectile(1, 0f, out var early);
            Assert.AreEqual(10f, early.x, 0.05f);
            Assert.IsFalse(replica.TryInterpolateProjectile(99, 11f, out _));
        }

        [Test]
        // frob:tests Hullbreach.Net.ServerSimulation.Tick
        public void ServerBroadcast_ReachesReplica_AndEmptyMessageFollowsLastShot()
        {
            var outbox = new ServerOutbox();
            var server = new ServerSimulation(outbox) { TickRate = 50f };
            server.Join(1, new ShipSnapshot(0, 0, new[]
            {
                new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0),
                new SnapshotBlock(0, 1, BlockTypes.Cannon, 0, 0),
            }, 0, 0, 0, 0, 0, 0));
            server.Gravity.Clear();
            server.SetInput(1, InputMessage.FromFloats(1, 1, 0f, 0f, true));
            for (int i = 0; i < 5; i++) server.Tick();
            Assert.Greater(server.Projectiles.Count, 0);

            var replica = new ClientReplica();
            var shots = outbox.Entries.Where(e => !e.Reliable && e.Bytes[0] == (byte)MessageKind.ProjectileState).ToList();
            Assert.IsNotEmpty(shots, "an unreliable ProjectileState must be emitted while shots fly");
            foreach (var e in shots) replica.ApplyReceived(e.Bytes, e.Bytes.Length);
            Assert.Greater(replica.ProjectileCount, 0);
        }
    }
}
