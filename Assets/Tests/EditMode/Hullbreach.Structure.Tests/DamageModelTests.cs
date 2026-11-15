using NUnit.Framework;
using Hullbreach.Structure;

namespace Hullbreach.Structure.Tests
{
    public class DamageModelTests
    {
        [Test]
        public void Hysteresis_StaysFailingBelowFailRatioIfAlreadyFailing()
        {
            // 0.95 is above RecoverRatio (0.9), so an already-failing block
            // stays failing even though it is below FailRatio (1.0).
            Assert.IsTrue(DamageModel.ShouldDetach(0.95f, 0f, alreadyFailing: true));
        }

        [Test]
        public void Hysteresis_DoesNotStartFailingAtTheSameRatio()
        {
            // The same 0.95 ratio must NOT start a fresh failure: that is
            // the whole point of the hysteresis band.
            Assert.IsFalse(DamageModel.ShouldDetach(0.95f, 0f, alreadyFailing: false));
        }

        [Test]
        public void Hysteresis_RecoversOnceBelowRecoverRatio()
        {
            Assert.IsFalse(DamageModel.ShouldDetach(0.85f, 0f, alreadyFailing: true));
        }

        [Test]
        public void BrittleRatio_FailsImmediatelyRegardlessOfHysteresis()
        {
            Assert.IsTrue(DamageModel.ShouldDetach(0f, 1.0f, alreadyFailing: false));
        }

        [Test]
        public void BrittleRatio_IsAsymmetricInCompressionVsTension()
        {
            // Same magnitude, tension vs compression: armor shrugs off
            // compression but not tension.
            float spall = 1f;
            float compressive = 4f;

            float tensionRatio = StressCriteria.BrittleRatio(major: 2f, minor: 0f, spall, compressive);
            float compressionRatio = StressCriteria.BrittleRatio(major: 0f, minor: -2f, spall, compressive);

            Assert.Greater(tensionRatio, compressionRatio);
        }

        [Test]
        public void Accumulate_AlwaysMakesProgressWhileOverstressed()
        {
            // A byte step per 20 ms tick would round to zero at ratio 1.05.
            Assert.Greater(DamageModel.Accumulate(0, 1.05f, 0.02f), 0);
            Assert.AreEqual(0, DamageModel.Accumulate(0, 1.0f, 0.02f));
            Assert.AreEqual(255, DamageModel.Accumulate(254, 9f, 0.02f));
        }

        [Test]
        public void SecondsToFail_ScalesWithOverload()
        {
            Assert.AreEqual(float.PositiveInfinity, DamageModel.SecondsToFail(0.99f));
            float mild = DamageModel.SecondsToFail(1.3f);
            float bad = DamageModel.SecondsToFail(2.6f);
            Assert.Greater(mild, bad);
            Assert.That(bad, Is.InRange(0.6f, 2.5f), "an obviously bad block lasts about a second");
            Assert.That(mild, Is.InRange(1.5f, 6f));
            Assert.LessOrEqual(DamageModel.SecondsToFail(1.01f), 5.5f, "even a hair over the limit fails within ~5 s");
        }

        [Test]
        public void ShouldDetach_WithDamage_NeedsSaturatedDamage()
        {
            Assert.IsFalse(DamageModel.ShouldDetach(2.5f, 0f, true, 100));
            Assert.IsTrue(DamageModel.ShouldDetach(2.5f, 0f, true, 255));
            Assert.IsFalse(DamageModel.ShouldDetach(0.5f, 0f, true, 255), "load gone: it survives");
            Assert.IsTrue(DamageModel.ShouldDetach(0.95f, 0f, true, 255), "hysteresis band still holds");
            Assert.IsFalse(DamageModel.ShouldDetach(0.95f, 0f, false, 255));
        }

        [Test]
        public void ShouldDetach_WithDamage_ExtremeBrittleOverloadStillSnaps()
        {
            Assert.IsFalse(DamageModel.ShouldDetach(0f, 1.5f, false, 0), "moderate brittle overload goes through damage");
            Assert.IsTrue(DamageModel.ShouldDetach(0f, DamageModel.BrittleSnapRatio, false, 0));
        }

        [Test]
        public void DamageRatio_IsTheWorseCriterion()
        {
            Assert.AreEqual(2f, DamageModel.DamageRatio(1f, 2f));
            Assert.AreEqual(3f, DamageModel.DamageRatio(3f, 0.1f));
        }
    }
}
