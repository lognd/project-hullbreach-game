using System.Linq;
using NUnit.Framework;
using Hullbreach.Core;
using Hullbreach.Builder;

namespace Hullbreach.Builder.Tests
{
    public class ShipDesignTests
    {
        static BlockGrid BuiltGrid()
        {
            var s = new BuilderSession();
            s.Select(BlockTypes.Core); s.Click(BlockKey.Pack(0, 0));
            s.Select(BlockTypes.Hull); s.Click(BlockKey.Pack(0, 1)); s.Click(BlockKey.Pack(1, 0));
            s.Select(BlockTypes.Thruster); s.Click(BlockKey.Pack(0, -1));
            s.Select(BlockTypes.Cannon); s.Click(BlockKey.Pack(-1, 0)); s.Hover(BlockKey.Pack(-5, 0)); s.Click(BlockKey.Pack(-5, 0));
            Assert.AreEqual(5, s.BlockCount, "fixture grid must be fully built");
            return s.Grid;
        }

        [Test]
        public void RoundTrip_PreservesBlocksAndCore()
        {
            var grid = BuiltGrid();
            string text = ShipDesignFile.Write(ShipDesign.FromGrid("Brick", grid));

            var load = ShipDesignFile.Load(text);
            Assert.IsNull(load.Error);
            Assert.IsTrue(load.IsValid, string.Join("; ", load.Problems.Select(p => p.ToString())));
            Assert.AreEqual("Brick", load.Design.Name);
            Assert.IsTrue(load.Design.TryBuildGrid(out BlockGrid back));

            Assert.AreEqual(grid.Count, back.Count);
            Assert.AreEqual(grid.CoreKey, back.CoreKey);
            foreach (var kvp in grid.All)
            {
                Assert.IsTrue(back.TryGet(kvp.Key, out Block b));
                Assert.AreEqual(kvp.Value.TypeId, b.TypeId);
                Assert.AreEqual(kvp.Value.Modifiers, b.Modifiers);
            }
        }

        [Test]
        public void Write_IsStableAndHasVersionHeader()
        {
            var design = new ShipDesign("A", new[] { new DesignBlock(0, 0, BlockTypes.Core, 0, 0), new DesignBlock(0, 1, BlockTypes.Hull, 0, 0) });
            Assert.AreEqual("hullbreach-design 1\nname A\nblock 0 0 0 0 0\nblock 0 1 1 0 0\n", ShipDesignFile.Write(design));
        }

        [Test]
        public void Load_ToleratesCrlfCommentsAndBlankLines()
        {
            var load = ShipDesignFile.Load("# saved\r\n\r\nhullbreach-design 1\r\nname Mine\r\nblock 0 0 0 0 0\r\n");
            Assert.IsTrue(load.IsValid);
            Assert.AreEqual("Mine", load.Design.Name);
        }

        [Test]
        public void Load_ReportsFloatingBlock_WithoutAlteringDesign()
        {
            var load = ShipDesignFile.Load("hullbreach-design 1\nname x\nblock 0 0 0 0 0\nblock 5 5 1 0 0\n");
            Assert.IsNull(load.Error);
            Assert.AreEqual(2, load.Design.Blocks.Count, "the bad block stays in the design");
            Assert.AreEqual(1, load.Problems.Count);
            Assert.AreEqual(DesignProblemKind.Placement, load.Problems[0].Kind);
            Assert.AreEqual(PlacementVerdict.NotAdjacent, load.Problems[0].Verdict);
            Assert.AreEqual(5, load.Problems[0].X);
            Assert.IsFalse(load.Design.TryBuildGrid(out BlockGrid grid));
            Assert.IsNull(grid);
        }

        [Test]
        public void Load_ReportsClearanceViolation()
        {
            // Thruster at (0,4) exhausts into (0,3); the hull there is reported.
            var load = ShipDesignFile.Load("hullbreach-design 1\nname x\nblock 0 5 0 0 0\nblock 0 4 3 0 0\nblock 0 3 1 0 0\n");
            Assert.AreEqual(1, load.Problems.Count);
            Assert.AreEqual(PlacementVerdict.InsideReservedCell, load.Problems[0].Verdict);
        }

        [Test]
        public void Load_ReportsSecondCore_OutOfRange_UnknownType_Duplicate_Damage_BadValue()
        {
            string text = "hullbreach-design 1\nname x\n" +
                "block 0 0 0 0 0\n" +
                "block 1 0 0 0 0\n" +     // second core
                "block 500 0 1 0 0\n" +   // out of range
                "block 0 1 99 0 0\n" +    // unknown type
                "block 0 0 1 0 0\n" +     // duplicate cell
                "block 0 2 1 0 7\n" +     // damaged (also not adjacent)
                "block 0 3 1 300 0\n";    // bad modifiers
            var kinds = ShipDesignFile.Load(text).Problems.Select(p => p.Kind).ToList();
            CollectionAssert.Contains(kinds, DesignProblemKind.UnknownType);
            CollectionAssert.Contains(kinds, DesignProblemKind.DuplicateCell);
            CollectionAssert.Contains(kinds, DesignProblemKind.DamagedBlock);
            CollectionAssert.Contains(kinds, DesignProblemKind.BadValue);
            var placements = ShipDesignFile.Load(text).Problems.Where(p => p.Kind == DesignProblemKind.Placement).Select(p => p.Verdict).ToList();
            CollectionAssert.Contains(placements, PlacementVerdict.OutOfRange);
            CollectionAssert.Contains(placements, PlacementVerdict.CoreAlreadyPlaced);
        }

        [Test]
        public void Load_ReportsMissingCore_AndEmptyDesign()
        {
            Assert.AreEqual(DesignProblemKind.MissingCore, ShipDesignFile.Load("hullbreach-design 1\nname x\nblock 0 0 1 0 0\n").Problems[0].Kind);
            Assert.AreEqual(DesignProblemKind.MissingCore, ShipDesignFile.Load("hullbreach-design 1\nname x\n").Problems[0].Kind);
        }

        [Test]
        public void Load_BlockOrderInFileDoesNotMatter()
        {
            var load = ShipDesignFile.Load("hullbreach-design 1\nname x\nblock 0 2 1 0 0\nblock 0 1 1 0 0\nblock 0 0 0 0 0\n");
            Assert.IsTrue(load.IsValid);
        }

        [TestCase(null, "empty")]
        [TestCase("", "empty")]
        [TestCase("garbage", "not a hullbreach")]
        [TestCase("hullbreach-design 2\nname x\n", "newer")]
        [TestCase("hullbreach-design x\n", "version")]
        [TestCase("hullbreach-design 1\nblock 0 0 0 0\n", "needs")]
        [TestCase("hullbreach-design 1\nblock 0 0 a 0 0\n", "integer")]
        [TestCase("hullbreach-design 1\nname a\nname b\n", "twice")]
        [TestCase("hullbreach-design 1\nwhat\n", "unknown")]
        public void Load_BadFile_ReturnsErrorNotException(string text, string errorPart)
        {
            var load = ShipDesignFile.Load(text);
            Assert.IsNull(load.Design);
            StringAssert.Contains(errorPart, load.Error);
            Assert.IsFalse(load.IsValid);
        }

        [Test]
        public void Load_TruncatedFile_NamesTheLine()
        {
            var load = ShipDesignFile.Load("hullbreach-design 1\nname x\nblock 0 0 0 0 0\nblock 0 1 1");
            Assert.AreEqual(4, load.ErrorLine);
        }
    }
}
