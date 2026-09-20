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
            // The same 0.95 ratio must NOT start a fresh failure -- that is
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
            // Same magnitude stress, tension vs. compression, with a
            // compressive limit far above the spall (tensile) limit -- armor
            // shrugs off compression but not tension.
            float spall = 1f;
            float compressive = 4f;

            float tensionRatio = StressCriteria.BrittleRatio(major: 2f, minor: 0f, spall, compressive);
            float compressionRatio = StressCriteria.BrittleRatio(major: 0f, minor: -2f, spall, compressive);

            Assert.Greater(tensionRatio, compressionRatio);
        }
    }
}
