using System.Collections.Generic;
using NUnit.Framework;
using Hullbreach.Structure;

namespace Hullbreach.Structure.Tests
{
    // Node sharing is the single most likely place for a silent bug: if
    // adjacent blocks do not share node ids, the mesh falls apart quietly.
    public class NodeLatticeTests
    {
        static HashSet<int> NodesOf(int x, int y)
        {
            var buf = new int[NodeLattice.NodesPerElement];
            NodeLattice.NodesOf(x, y, buf);
            return new HashSet<int>(buf);
        }

        [Test]
        public void SingleBlock_HasEightDistinctNodes()
        {
            Assert.AreEqual(8, NodesOf(0, 0).Count);
        }

        [Test]
        public void HorizontallyAdjacentBlocks_ShareExactlyThreeNodes()
        {
            // The shared edge contributes 2 corners + 1 midside.
            var a = NodesOf(0, 0);
            a.IntersectWith(NodesOf(1, 0));
            Assert.AreEqual(3, a.Count);
        }

        [Test]
        public void VerticallyAdjacentBlocks_ShareExactlyThreeNodes()
        {
            var a = NodesOf(0, 0);
            a.IntersectWith(NodesOf(0, 1));
            Assert.AreEqual(3, a.Count);
        }

        [Test]
        public void DiagonallyAdjacentBlocks_ShareExactlyOneNode()
        {
            // Only the single touching corner.
            var a = NodesOf(0, 0);
            a.IntersectWith(NodesOf(1, 1));
            Assert.AreEqual(1, a.Count);
        }

        [Test]
        public void DistantBlocks_ShareNothing()
        {
            var a = NodesOf(0, 0);
            a.IntersectWith(NodesOf(5, 5));
            Assert.IsEmpty(a);
        }

        [Test]
        public void ElementCenter_IsNotANode()
        {
            // Q8 is serendipity: the center belongs to Q9, not here.
            var center = NodeLattice.PackNode(1, 1);
            Assert.IsFalse(NodesOf(0, 0).Contains(center));
        }

        [Test]
        public void PackNode_IsInjective()
        {
            var seen = new HashSet<int>();
            for (int dx = -40; dx <= 40; dx++)
            for (int dy = -40; dy <= 40; dy++)
                Assert.IsTrue(seen.Add(NodeLattice.PackNode(dx, dy)),
                    $"node id collision at ({dx},{dy})");
        }
    }
}
