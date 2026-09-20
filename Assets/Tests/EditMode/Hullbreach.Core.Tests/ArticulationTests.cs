using System.Collections.Generic;
using NUnit.Framework;
using Hullbreach.Core;

namespace Hullbreach.Core.Tests
{
    public class ArticulationTests
    {
        [Test]
        public void MiddleOfALine_IsAnArticulationPoint()
        {
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            g.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Hull));
            g.TryAdd(BlockKey.Pack(2, 0), new Block(BlockTypes.Hull));

            var cuts = new HashSet<int>();
            Articulation.Compute(g, cuts);

            Assert.IsTrue(cuts.Contains(BlockKey.Pack(1, 0)), "middle must be a cut vertex");
            Assert.IsFalse(cuts.Contains(BlockKey.Pack(0, 0)), "an end is never a cut vertex");
            Assert.IsFalse(cuts.Contains(BlockKey.Pack(2, 0)), "an end is never a cut vertex");
        }

        [Test]
        public void CycleHasNoArticulationPoints()
        {
            // 2x2 cells form a 4-cycle; removing any one leaves it connected,
            // so the fast path applies to every block here.
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            g.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Hull));
            g.TryAdd(BlockKey.Pack(0, 1), new Block(BlockTypes.Hull));
            g.TryAdd(BlockKey.Pack(1, 1), new Block(BlockTypes.Hull));

            var cuts = new HashSet<int>();
            Articulation.Compute(g, cuts);

            Assert.IsEmpty(cuts);
        }

        [Test]
        public void SingleBlock_IsNotAnArticulationPoint()
        {
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));

            var cuts = new HashSet<int>();
            Articulation.Compute(g, cuts);

            Assert.IsEmpty(cuts);
        }
    }
}
