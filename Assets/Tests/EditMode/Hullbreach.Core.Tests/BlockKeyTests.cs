using System.Collections.Generic;
using NUnit.Framework;
using Hullbreach.Core;

namespace Hullbreach.Core.Tests
{
    public class BlockKeyTests
    {
        [Test]
        public void Pack_ThenUnpack_RoundTrips()
        {
            for (int x = BlockKey.Min; x <= BlockKey.Max; x += 7)
            for (int y = BlockKey.Min; y <= BlockKey.Max; y += 7)
            {
                BlockKey.Unpack(BlockKey.Pack(x, y), out var rx, out var ry);
                Assert.AreEqual(x, rx, $"x round-trip failed for ({x},{y})");
                Assert.AreEqual(y, ry, $"y round-trip failed for ({x},{y})");
            }
        }

        [Test]
        public void Pack_IsInjective()
        {
            // Two different coordinates must never collide, or the grid will
            // silently overwrite blocks.
            var seen = new HashSet<int>();
            for (int x = BlockKey.Min; x <= BlockKey.Max; x += 3)
            for (int y = BlockKey.Min; y <= BlockKey.Max; y += 3)
                Assert.IsTrue(seen.Add(BlockKey.Pack(x, y)),
                    $"collision at ({x},{y})");
        }

        [Test]
        public void Pack_HandlesOriginAndExtremes()
        {
            BlockKey.Unpack(BlockKey.Pack(0, 0), out var x, out var y);
            Assert.AreEqual(0, x);
            Assert.AreEqual(0, y);

            BlockKey.Unpack(BlockKey.Pack(BlockKey.Min, BlockKey.Max), out x, out y);
            Assert.AreEqual(BlockKey.Min, x);
            Assert.AreEqual(BlockKey.Max, y);
        }

        [Test]
        public void Neighbors_AreTheFourOrthogonalCells()
        {
            var into = new int[4];
            BlockKey.Neighbors(BlockKey.Pack(3, 5), into);

            var expected = new HashSet<int>
            {
                BlockKey.Pack(4, 5),
                BlockKey.Pack(2, 5),
                BlockKey.Pack(3, 6),
                BlockKey.Pack(3, 4),
            };

            CollectionAssert.AreEquivalent(expected, into);
        }

        [Test]
        public void InRange_RejectsOutOfBounds()
        {
            Assert.IsTrue(BlockKey.InRange(0, 0));
            Assert.IsTrue(BlockKey.InRange(BlockKey.Min, BlockKey.Max));
            Assert.IsFalse(BlockKey.InRange(BlockKey.Min - 1, 0));
            Assert.IsFalse(BlockKey.InRange(0, BlockKey.Max + 1));
        }
    }
}
