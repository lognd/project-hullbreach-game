using Hullbreach.Hud;
using NUnit.Framework;

namespace Hullbreach.Hud.Tests
{
    // Covers every public member of HullWarningModel, including the exact
    // CRITICAL pulse formula (D7).
    public sealed class HullWarningModelTests
    {
        [Test]
        public void Ok_HeadlineAndColor_NoDetails()
        {
            var model = HullWarningModel.Build(HullWarning.Ok, 0.12f, 0, string.Empty, 0f);
            Assert.AreEqual("Hull: OK   (max ratio 0.12)", model.Headline);
            Assert.IsFalse(model.ShowDetails);
            Assert.AreEqual(string.Empty, model.Detail);
            Assert.AreEqual(string.Empty, model.Hint);
            Assert.AreEqual(HudColor.WarningOkGreen.G, model.Color.G);
        }

        [Test]
        public void Strain_HeadlineHintAndColor()
        {
            var model = HullWarningModel.Build(HullWarning.Strain, 0.55f, 0, string.Empty, 0f);
            Assert.AreEqual("Hull: STRAIN   (max ratio 0.55)", model.Headline);
            Assert.IsTrue(model.ShowDetails);
            Assert.AreEqual("ease off thrust", model.Hint);
            Assert.AreEqual("load factor low", model.Detail);
            Assert.AreEqual(HudColor.WarningStrainYellow.R, model.Color.R);
        }

        [Test]
        public void Strain_DetailNamesCriticalBlockCountAndWorst()
        {
            var model = HullWarningModel.Build(HullWarning.Strain, 0.6f, 2, "Hull", 0f);
            Assert.AreEqual("2 block(s) in the red, worst: Hull", model.Detail);
        }

        [Test]
        public void Critical_SingleBlock_HintIsEaseOffThrust()
        {
            var model = HullWarningModel.Build(HullWarning.Critical, 0.9f, 1, "Hull", 0f);
            Assert.AreEqual("ease off thrust", model.Hint);
        }

        [Test]
        public void Critical_MultipleBlocksWithWorstName_HintIsBraceTheArm()
        {
            var model = HullWarningModel.Build(HullWarning.Critical, 0.9f, 2, "Hull", 0f);
            Assert.AreEqual("brace the arm", model.Hint);
        }

        [Test]
        public void Critical_NoCriticalBlocks_DetailIsLoadFactorLow()
        {
            var model = HullWarningModel.Build(HullWarning.Critical, 0.9f, 0, string.Empty, 0f);
            Assert.AreEqual("load factor low", model.Detail);
            Assert.AreEqual("ease off thrust", model.Hint);
        }

        [Test]
        public void Critical_HeadlineFormatsMaxRatio()
        {
            var model = HullWarningModel.Build(HullWarning.Critical, 0.987f, 1, "Hull", 0f);
            Assert.AreEqual("Hull: CRITICAL   (max ratio 0.99)", model.Headline);
        }

        [Test]
        public void Critical_PulseMatchesSinFormulaAtZeroTime()
        {
            // pulse = 0.55 + 0.45*sin(0) = 0.55
            var model = HullWarningModel.Build(HullWarning.Critical, 0.9f, 1, "Hull", 0f);
            Assert.AreEqual(1f, model.Color.R, 1e-6f);
            Assert.AreEqual(0.15f * 0.55f, model.Color.G, 1e-5f);
            Assert.AreEqual(0.15f * 0.55f, model.Color.B, 1e-5f);
        }

        [Test]
        public void Critical_PulseVariesWithUnscaledTime()
        {
            float t = System.MathF.PI / 24f; // sin(t*12) = sin(pi/2) = 1
            var model = HullWarningModel.Build(HullWarning.Critical, 0.9f, 1, "Hull", t);
            float expectedPulse = 0.55f + 0.45f * 1f;
            Assert.AreEqual(0.15f * expectedPulse, model.Color.G, 1e-4f);
        }
    }
}
