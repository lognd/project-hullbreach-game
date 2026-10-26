using System.IO;
using System.Text;
using NUnit.Framework;
using Hullbreach.Core;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    // Pins the BlockGrid design format (S35-1): lossless round trip and errors instead of throws.
    public class BlockGridDesignTests
    {
        const string Core = "{\"x\":0,\"y\":0,\"type\":\"Core\"}";

        static string Design(params string[] blocks) =>
            "{\"version\":1,\"blocks\":[" + string.Join(",", blocks) + "]}";

        static BlockGrid Sample()
        {
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            g.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Hull));
            g.TryAdd(BlockKey.Pack(-1, 0), new Block(BlockTypes.Armor));
            g.TryAdd(BlockKey.Pack(0, -1), new Block(BlockTypes.Thruster, 2));
            g.TryAdd(BlockKey.Pack(0, 1), new Block(BlockTypes.RetroThruster, 3, 200));
            g.TryAdd(BlockKey.Pack(1, 1), new Block(BlockTypes.Cannon, 1));
            g.TryAdd(BlockKey.Pack(-1, 1), new Block(BlockTypes.Fin));
            return g;
        }

        [Test]
        public void RoundTrip_PreservesCellsTypesAndModifiers_ButNotDamage()
        {
            var src = Sample();
            var r = BlockGridDesign.Parse(BlockGridDesign.Serialize(src));
            Assert.IsTrue(r.IsOk, r.Error);
            Assert.AreEqual(src.Count, r.Value.Count);
            Assert.AreEqual(src.CoreKey, r.Value.CoreKey);
            foreach (var kv in src.All)
            {
                Assert.IsTrue(r.Value.TryGet(kv.Key, out Block b));
                Assert.AreEqual(kv.Value.TypeId, b.TypeId);
                Assert.AreEqual(kv.Value.Modifiers, b.Modifiers);
                Assert.AreEqual(0, b.Damage);
            }
            Assert.AreEqual(src.Mass.Total, r.Value.Mass.Total, 1e-5f);
        }

        [Test]
        public void Serialize_IsDeterministic_AndStable()
        {
            string a = BlockGridDesign.Serialize(Sample());
            Assert.AreEqual(a, BlockGridDesign.Serialize(Sample()));
            Assert.AreEqual(a, BlockGridDesign.Serialize(BlockGridDesign.Parse(a).Value));
            StringAssert.StartsWith("{\"version\":1,\"blocks\":[", a);
        }

        [Test]
        public void RoundTrip_CornerCells()
        {
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(BlockKey.Min, BlockKey.Min), new Block(BlockTypes.Core));
            g.TryAdd(BlockKey.Pack(BlockKey.Min + 1, BlockKey.Min), new Block(BlockTypes.Hull));
            var r = BlockGridDesign.Parse(BlockGridDesign.Serialize(g));
            Assert.IsTrue(r.IsOk, r.Error);
            Assert.IsTrue(r.Value.Contains(BlockKey.Pack(BlockKey.Min, BlockKey.Min)));
        }

        [Test]
        public void MinimalDesign_IsAccepted_ModsDefaultsToZero()
        {
            var r = BlockGridDesign.Parse(Design(Core));
            Assert.IsTrue(r.IsOk, r.Error);
            Assert.AreEqual(1, r.Value.Count);
        }

        [Test]
        public void SaveFile_ThenLoadFile_RoundTrips()
        {
            string path = Path.Combine(Path.GetTempPath(), "hb-design-" + System.Guid.NewGuid().ToString("N") + ".json");
            try
            {
                Assert.IsNull(BlockGridDesign.SaveFile(path, Sample()));
                var r = BlockGridDesign.LoadFile(path);
                Assert.IsTrue(r.IsOk, r.Error);
                Assert.AreEqual(Sample().Count, r.Value.Count);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Test]
        public void MissingFile_AndUnwritablePath_AreErrors()
        {
            var r = BlockGridDesign.LoadFile(Path.Combine(Path.GetTempPath(), "hb-none", "nope.json"));
            Assert.IsFalse(r.IsOk);
            StringAssert.Contains("cannot read design", r.Error);
            Assert.IsNotNull(BlockGridDesign.SaveFile(Path.Combine(Path.GetTempPath(), "hb-none", "x", "y.json"), Sample()));
            Assert.IsFalse(BlockGridDesign.LoadFile(null).IsOk);
        }

        [TestCase("", "empty")]
        [TestCase("not json", "not a JSON object")]
        [TestCase("[]", "not a JSON object")]
        [TestCase("{}", "version is required")]
        [TestCase("{\"version\":\"1\",\"blocks\":[]}", "unsupported design version")]
        [TestCase("{\"version\":2,\"blocks\":[]}", "unsupported design version")]
        [TestCase("{\"version\":1}", "blocks is required")]
        [TestCase("{\"version\":1,\"blocks\":{}}", "blocks must be an array")]
        [TestCase("{\"version\":1,\"blocks\":[]}", "no blocks")]
        [TestCase("{\"version\":1,\"blocks\":[3]}", "blocks[0] must be an object")]
        public void BadEnvelope_ReturnsError(string json, string fragment)
        {
            var r = BlockGridDesign.Parse(json);
            Assert.IsFalse(r.IsOk);
            StringAssert.Contains(fragment, r.Error);
        }

        [TestCase("{\"x\":0,\"type\":\"Hull\"}", "blocks[1].y")]
        [TestCase("{\"x\":0.5,\"y\":1,\"type\":\"Hull\"}", "blocks[1].x")]
        [TestCase("{\"x\":128,\"y\":0,\"type\":\"Hull\"}", "blocks[1].x")]
        [TestCase("{\"x\":0,\"y\":-129,\"type\":\"Hull\"}", "blocks[1].y")]
        [TestCase("{\"x\":\"1\",\"y\":0,\"type\":\"Hull\"}", "blocks[1].x")]
        [TestCase("{\"x\":1,\"y\":0}", "blocks[1].type")]
        [TestCase("{\"x\":1,\"y\":0,\"type\":7}", "blocks[1].type")]
        [TestCase("{\"x\":1,\"y\":0,\"type\":\"Warp\"}", "not a known block type")]
        [TestCase("{\"x\":1,\"y\":0,\"type\":\"hull\"}", "not a known block type")]
        [TestCase("{\"x\":1,\"y\":0,\"type\":\"Hull\",\"mods\":256}", "blocks[1].mods")]
        [TestCase("{\"x\":1,\"y\":0,\"type\":\"Hull\",\"mods\":-1}", "blocks[1].mods")]
        [TestCase("{\"x\":0,\"y\":0,\"type\":\"Hull\"}", "overlaps")]
        [TestCase("{\"x\":1,\"y\":0,\"type\":\"Core\"}", "second core")]
        public void BadBlock_ReturnsError(string block, string fragment)
        {
            var r = BlockGridDesign.Parse(Design(Core, block));
            Assert.IsFalse(r.IsOk);
            StringAssert.Contains(fragment, r.Error);
        }

        [Test]
        public void NoCore_IsAnError()
        {
            var r = BlockGridDesign.Parse(Design("{\"x\":0,\"y\":0,\"type\":\"Hull\"}"));
            Assert.IsFalse(r.IsOk);
            StringAssert.Contains("no core", r.Error);
        }

        [Test]
        public void DisconnectedBlock_IsAnError_NamingTheCell()
        {
            var r = BlockGridDesign.Parse(Design(Core, "{\"x\":5,\"y\":5,\"type\":\"Hull\"}"));
            Assert.IsFalse(r.IsOk);
            StringAssert.Contains("not connected", r.Error);
            StringAssert.Contains("5,5", r.Error);
        }

        [Test]
        public void DiagonalOnly_CountsAsDisconnected()
        {
            var r = BlockGridDesign.Parse(Design(Core, "{\"x\":1,\"y\":1,\"type\":\"Hull\"}"));
            Assert.IsFalse(r.IsOk);
        }

        [Test]
        public void TooManyBlocks_IsAnError()
        {
            var sb = new StringBuilder("{\"version\":1,\"blocks\":[");
            for (int i = 0; i <= BlockGridDesign.MaxBlocks; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"x\":0,\"y\":0,\"type\":\"Hull\"}");
            }
            sb.Append("]}");
            var r = BlockGridDesign.Parse(sb.ToString());
            Assert.IsFalse(r.IsOk);
            StringAssert.Contains("more than", r.Error);
        }

        [Test]
        public void OversizedText_IsRefusedBeforeParsing()
        {
            var r = BlockGridDesign.Parse(new string(' ', BlockGridDesign.MaxJsonChars + 1));
            Assert.IsFalse(r.IsOk);
            StringAssert.Contains("larger than", r.Error);
        }

        [Test]
        public void DeepNesting_IsRefused_WithoutRecursingIntoIt()
        {
            var r = BlockGridDesign.Parse(new string('[', 200000));
            Assert.IsFalse(r.IsOk);
            StringAssert.Contains("nested", r.Error);
            // Brackets inside strings do not count toward depth.
            Assert.IsTrue(BlockGridDesign.Parse(Design("{\"x\":0,\"y\":0,\"type\":\"Core\",\"note\":\"[[[[[[[[[[\"}")).IsOk);
        }
    }
}
