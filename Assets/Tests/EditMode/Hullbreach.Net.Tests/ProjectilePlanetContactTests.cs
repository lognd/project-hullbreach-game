using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Net;
using Hullbreach.Platform;
using Hullbreach.World;

namespace Hullbreach.Net.Tests
{
    // Server projectiles stop at planets: despawn on contact, logged; wells never stop them.
    public class ProjectilePlanetContactTests
    {
        sealed class CaptureLog : IPlatformLog
        {
            public readonly System.Collections.Generic.List<string> Lines = new System.Collections.Generic.List<string>();
            public void Info(string message) => Lines.Add(message);
            public void Warning(string message) => Lines.Add(message);
        }

        // A cannon ship at the origin firing straight up (+y) with no other gravity.
        static ServerSimulation Make(out CaptureLog log)
        {
            log = new CaptureLog();
            var server = new ServerSimulation(new ServerOutbox()) { TickRate = 50f, Log = log };
            server.Join(1, new ShipSnapshot(0, 0, new[]
            {
                new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0),
                new SnapshotBlock(0, 1, BlockTypes.Cannon, 0, 0),
            }, 0, 0, 0, 0, 0, 0));
            server.Gravity.Clear();
            server.Ships[1].Position = float2.zero;
            server.SetInput(1, InputMessage.FromFloats(1, 1, 0f, 0f, true));
            return server;
        }

        [Test]
        public void ProjectileIntoPlanet_DespawnsAndLogs()
        {
            var server = Make(out var log);
            server.Gravity.Add(new GravityBody(new float2(0f, 10f), 0f, 2f, 0.3f));
            for (int i = 0; i < 60; i++) server.Tick();

            Assert.IsTrue(log.Lines.Exists(l => l.Contains("hit planet 0")), "contact must be logged");
            foreach (var p in server.Projectiles)
                Assert.Less(p.Position.y, 8f, "no live shot may be inside the planet");
        }

        [Test]
        public void ProjectileAwayFromPlanet_IsNotStopped()
        {
            var server = Make(out var log);
            server.Gravity.Add(new GravityBody(new float2(0f, -10f), 0f, 2f, 0.3f));
            for (int i = 0; i < 20; i++) server.Tick();

            Assert.Greater(server.Projectiles.Count, 0);
            Assert.IsFalse(log.Lines.Exists(l => l.Contains("hit planet")));
        }

        [Test]
        public void TemporaryWell_DoesNotStopProjectile()
        {
            var server = Make(out var log);
            server.Gravity.AddTemporary(new GravityBody(new float2(0f, 10f), 0f, 2f, 0.3f), 10f);
            for (int i = 0; i < 60; i++) server.Tick();

            Assert.IsFalse(log.Lines.Exists(l => l.Contains("hit planet")));
        }
    }
}
