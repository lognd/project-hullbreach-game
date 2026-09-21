using NUnit.Framework;
using Hullbreach.Core;

namespace Hullbreach.Core.Tests
{
    public class BlockTypesTests
    {
        const float Tol = 1e-4f;

        [Test]
        public void EffectiveStiffness_MatchesNominalWhenUndamaged()
        {
            var block = new Block(BlockTypes.Hull, damage: 0);
            Assert.AreEqual(BlockTypes.Get(BlockTypes.Hull).YoungsModulus,
                             BlockTypes.EffectiveStiffness(block), Tol);
        }

        [Test]
        public void EffectiveStiffness_IsFlooredAtFullDamage()
        {
            // Even at full damage the stiffness must stay above zero so the
            // global stiffness matrix never goes singular.
            var block = new Block(BlockTypes.Hull, damage: 255);
            var expectedFloor = BlockTypes.Get(BlockTypes.Hull).YoungsModulus * 0.05f;

            Assert.AreEqual(expectedFloor, BlockTypes.EffectiveStiffness(block), Tol);
            Assert.Greater(BlockTypes.EffectiveStiffness(block), 0f);
        }

        [Test]
        public void EffectiveStiffness_DecreasesMonotonicallyWithDamage()
        {
            var lightlyDamaged = new Block(BlockTypes.Hull, damage: 32);
            var heavilyDamaged = new Block(BlockTypes.Hull, damage: 200);

            Assert.Greater(BlockTypes.EffectiveStiffness(lightlyDamaged),
                           BlockTypes.EffectiveStiffness(heavilyDamaged));
        }

        [Test]
        public void Table_KeepsHullAsTheYieldStressReference()
        {
            Assert.AreEqual(1.0f, BlockTypes.Get(BlockTypes.Hull).YieldStress, Tol);
        }

        [Test]
        public void Table_ArmorIsHeavierAndMoreBrittleThanHull()
        {
            var armor = BlockTypes.Get(BlockTypes.Armor);
            var hull = BlockTypes.Get(BlockTypes.Hull);

            Assert.Greater(armor.Mass, hull.Mass);
            Assert.Greater(armor.SpallStress, hull.SpallStress);
            Assert.Greater(armor.CompressiveStress, hull.CompressiveStress);
            Assert.Less(armor.YieldStress, hull.YieldStress);
        }

        [Test]
        public void Table_FinExistsWithPositiveMass()
        {
            var fin = BlockTypes.Get(BlockTypes.Fin);
            Assert.AreEqual("Fin", fin.Name);
            Assert.Greater(fin.Mass, 0f);
        }
    }
}
