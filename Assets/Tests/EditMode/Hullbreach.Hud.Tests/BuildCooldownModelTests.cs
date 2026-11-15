using NUnit.Framework;

namespace Hullbreach.Hud.Tests
{
    // Covers every member of BuildCooldownModel.
    public sealed class BuildCooldownModelTests
    {
        [Test]
        public void Remaining_CountsDown_AndClampsAtZero()
        {
            Assert.AreEqual(1.0f, BuildCooldownModel.Remaining(0.5f, 1.5f), 1e-5f);
            Assert.AreEqual(0f, BuildCooldownModel.Remaining(9f, 1.5f));
        }

        [Test]
        public void Remaining_IsZero_ForNoCooldownOrNaN()
        {
            Assert.AreEqual(0f, BuildCooldownModel.Remaining(0.1f, 0f));
            Assert.AreEqual(0f, BuildCooldownModel.Remaining(float.NaN, 1.5f));
        }

        [Test]
        public void Line_ReadyOrCountdown()
        {
            Assert.AreEqual("Build ready", BuildCooldownModel.Line(0f));
            Assert.AreEqual("Build cooldown 0.8s", BuildCooldownModel.Line(0.75f));
        }

        [Test]
        public void Fill_IsClampedFraction()
        {
            Assert.AreEqual(0f, BuildCooldownModel.Fill(0f, 1.5f));
            Assert.AreEqual(0.5f, BuildCooldownModel.Fill(0.75f, 1.5f), 1e-5f);
            Assert.AreEqual(1f, BuildCooldownModel.Fill(5f, 1.5f));
            Assert.AreEqual(0f, BuildCooldownModel.Fill(1f, 0f));
        }
    }
}
