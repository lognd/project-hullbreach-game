using NUnit.Framework;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    // Pins the arena config loader (S40-3): defaults, overrides, and errors instead of throws.
    public class ArenaConfigTests
    {
        [Test]
        public void EmptyObject_YieldsDefaults()
        {
            var r = ArenaConfigLoader.Parse("{}");
            Assert.IsTrue(r.IsOk, r.Error);
            var d = ArenaConfig.Defaults();
            Assert.AreEqual(2, r.Value.Bodies.Count);
            Assert.AreEqual(d.Bodies[0].Mu, r.Value.Bodies[0].Mu);
            Assert.AreEqual(d.SpawnAltitude, r.Value.SpawnAltitude);
        }

        [Test]
        public void Defaults_MatchTheOriginalHardcodedConstants()
        {
            var d = ArenaConfig.Defaults();
            Assert.AreEqual(-60f, d.Bodies[0].Y);
            Assert.AreEqual(900f, d.Bodies[0].Mu);
            Assert.AreEqual(18f, d.Bodies[0].Radius);
            Assert.AreEqual(45f, d.Bodies[1].X);
            Assert.AreEqual(120f, d.Bodies[1].Mu);
            Assert.AreEqual(0.18f, d.SpawnAngleStep);
        }

        [Test]
        public void Overrides_AreApplied_AndOmittedKeysKeepDefaults()
        {
            var r = ArenaConfigLoader.Parse(
                "{\"bodies\":[{\"x\":1,\"y\":2,\"mu\":500,\"radius\":10}],\"spawn\":{\"altitude\":40}}");
            Assert.IsTrue(r.IsOk, r.Error);
            Assert.AreEqual(1, r.Value.Bodies.Count);
            Assert.AreEqual(500f, r.Value.Bodies[0].Mu);
            Assert.AreEqual(0.2f, r.Value.Bodies[0].Restitution);
            Assert.AreEqual(40f, r.Value.SpawnAltitude);
            Assert.AreEqual(12f, r.Value.SpawnLateral);
        }

        [TestCase("")]
        [TestCase("not json")]
        [TestCase("[]")]
        [TestCase("{\"bodies\":[]}")]
        [TestCase("{\"bodies\":{}}")]
        [TestCase("{\"bodies\":[3]}")]
        [TestCase("{\"bodies\":[{\"radius\":5}]}")]
        [TestCase("{\"bodies\":[{\"mu\":5}]}")]
        [TestCase("{\"bodies\":[{\"mu\":-1,\"radius\":5}]}")]
        [TestCase("{\"bodies\":[{\"mu\":1,\"radius\":0}]}")]
        [TestCase("{\"bodies\":[{\"mu\":1,\"radius\":5,\"restitution\":2}]}")]
        [TestCase("{\"bodies\":[{\"mu\":\"big\",\"radius\":5}]}")]
        [TestCase("{\"spawn\":5}")]
        [TestCase("{\"spawn\":{\"altitude\":0}}")]
        public void BadInput_ReturnsError_NotException(string json)
        {
            var r = ArenaConfigLoader.Parse(json);
            Assert.IsFalse(r.IsOk);
            Assert.IsNotEmpty(r.Error);
        }

        [Test]
        public void MissingFile_ReturnsError()
        {
            var r = ArenaConfigLoader.LoadFile("/nonexistent/dir/arena.json");
            Assert.IsFalse(r.IsOk);
            Assert.IsNotEmpty(r.Error);
        }

        [Test]
        public void ConfigureDemoArena_WithCustomConfig_BuildsThatField()
        {
            var sim = new ServerSimulation(new ServerOutbox());
            var r = ArenaConfigLoader.Parse("{\"bodies\":[{\"mu\":500,\"radius\":10}]}");
            sim.ConfigureDemoArena(r.Value);
            Assert.AreEqual(1, sim.Gravity.Count);
            sim.ConfigureDemoArena();
            Assert.AreEqual(2, sim.Gravity.Count);
        }
    }
}
