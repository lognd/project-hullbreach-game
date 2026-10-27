using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Net;
using Hullbreach.Platform;

namespace Hullbreach.Net.Tests
{
    // Server applies the arena edge push as velocity only: ships return, blocks stay undamaged (S41-1).
    public class ArenaBoundsServerTests
    {
        sealed class CaptureLog : IPlatformLog
        {
            public readonly System.Collections.Generic.List<string> Lines = new System.Collections.Generic.List<string>();
            public void Info(string message) => Lines.Add(message);
            public void Warning(string message) => Lines.Add(message);
        }

        static ServerSimulation Make(out CaptureLog log, out Hullbreach.Ship.ShipBody ship)
        {
            log = new CaptureLog();
            var server = new ServerSimulation(new ServerOutbox()) { TickRate = 50f, Log = log };
            server.Join(1, new ShipSnapshot(0, 0,
                new[] { new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0) }, 0, 0, 0, 0, 0, 0));
            server.ConfigureDemoArena(new ArenaConfig(ArenaConfig.Defaults().Bodies, 12f, 30f, 2f, 0.18f,
                new Hullbreach.World.ArenaBounds(float2.zero, 100f, 20f, 8f)));
            ship = server.Ships[1];
            return server;
        }

        [Test]
        public void ShipPastTheEdge_IsPushedBackWithoutDamage()
        {
            var server = Make(out var log, out var ship);
            ship.Position = new float2(120f, 0f);
            ship.Velocity = float2.zero;
            ship.Gravity = new Hullbreach.World.GravityField();
            for (int i = 0; i < 50; i++) server.Tick();

            Assert.Less(ship.Velocity.x, 0f, "push must point back toward the arena");
            foreach (int key in ship.Grid.SortedKeys) { ship.Grid.TryGet(key, out var b); Assert.AreEqual(0, b.Damage); }
            Assert.IsTrue(log.Lines.Exists(l => l.Contains("entered arena edge zone")));
        }

        [Test]
        public void ShipInsideTheArena_IsNotTouched()
        {
            var server = Make(out _, out var ship);
            ship.Position = new float2(10f, 0f);
            ship.Velocity = float2.zero;
            ship.Gravity = new Hullbreach.World.GravityField();
            server.Tick();
            Assert.AreEqual(0f, math.length(ship.Velocity), 1e-5f);
        }

        [Test]
        public void Loader_ReadsBounds_AndRejectsBadOnes()
        {
            var ok = ArenaConfigLoader.Parse("{\"bounds\":{\"radius\":50,\"margin\":10,\"strength\":3}}");
            Assert.IsTrue(ok.IsOk, ok.Error);
            Assert.AreEqual(50f, ok.Value.Bounds.Radius);
            Assert.AreEqual(3f, ok.Value.Bounds.Strength);
            Assert.IsFalse(ArenaConfigLoader.Parse("{\"bounds\":{\"radius\":0}}").IsOk);
            Assert.IsFalse(ArenaConfigLoader.Parse("{\"bounds\":{\"margin\":-1}}").IsOk);
            Assert.IsFalse(ArenaConfigLoader.Parse("{\"bounds\":5}").IsOk);
        }
    }
}
