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
        public void Symmetric_IsFalseOnlyForFacingBlocks()
        {
            Assert.IsTrue(BlockPalette.IsSymmetric(BlockTypes.Core));
            Assert.IsTrue(BlockPalette.IsSymmetric(BlockTypes.Hull));
            Assert.IsTrue(BlockPalette.IsSymmetric(BlockTypes.Armor));
            Assert.IsTrue(BlockPalette.IsSymmetric(BlockTypes.Thruster), "thruster always pushes +y; it has no facing to orient");
            Assert.IsTrue(BlockPalette.IsSymmetric(BlockTypes.RetroThruster), "retro thruster always pushes -y; it has no facing to orient");
            Assert.IsFalse(BlockPalette.IsSymmetric(BlockTypes.Cannon));
            Assert.IsFalse(BlockPalette.IsSymmetric(BlockTypes.Fin));
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
