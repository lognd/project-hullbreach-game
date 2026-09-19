using System.Linq;
using NUnit.Framework;
using Hullbreach.Core;
using Hullbreach.Builder;

namespace Hullbreach.Builder.Tests
{
    public class BlockPaletteTests
    {
        [Test]
        public void All_ListsEveryBlockTypeWithItsMass()
        {
            var entries = BlockPalette.All().ToList();
            Assert.AreEqual(BlockTypes.Count, entries.Count);

            foreach (var entry in entries)
            {
                var type = BlockTypes.Get(entry.TypeId);
                Assert.AreEqual(type.Name, entry.Name);
                Assert.AreEqual(type.Mass, entry.Mass, 1e-6f);
            }
        }

        [Test]
        public void Symmetric_IsFalseOnlyForThrusterAndCannon()
        {
            Assert.IsTrue(BlockPalette.IsSymmetric(BlockTypes.Core));
            Assert.IsTrue(BlockPalette.IsSymmetric(BlockTypes.Hull));
            Assert.IsTrue(BlockPalette.IsSymmetric(BlockTypes.Armor));
            Assert.IsFalse(BlockPalette.IsSymmetric(BlockTypes.Thruster));
            Assert.IsFalse(BlockPalette.IsSymmetric(BlockTypes.Cannon));
        }

        [Test]
        public void Cost_IsNonNegativeForEveryEntry()
        {
            foreach (var entry in BlockPalette.All())
            {
                Assert.GreaterOrEqual(entry.Cost, 0);
            }
        }
    }
}
