using NUnit.Framework;

namespace Hullbreach.Hud.Tests
{
    // Covers both members of CameraRigModel: lead distance and speed zoom.
    public sealed class CameraRigModelTests
    {
        [Test]
        public void LeadDistance_ScalesWithSpeed_AndIsCapped()
        {
            Assert.AreEqual(2f, CameraRigModel.LeadDistance(10f, 0.2f, 12f), 1e-5f);
            Assert.AreEqual(12f, CameraRigModel.LeadDistance(500f, 0.2f, 12f), 1e-5f);
        }

        [Test]
        public void LeadDistance_IsZero_AtRestOrNegativeOrNaN()
        {
            Assert.AreEqual(0f, CameraRigModel.LeadDistance(0f, 0.2f, 12f));
            Assert.AreEqual(0f, CameraRigModel.LeadDistance(-3f, 0.2f, 12f));
            Assert.AreEqual(0f, CameraRigModel.LeadDistance(float.NaN, 0.2f, 12f));
        }

        [Test]
        public void ZoomSize_InterpolatesFromBaseToMax_ThenHolds()
        {
            Assert.AreEqual(12f, CameraRigModel.ZoomSize(0f, 12f, 24f, 40f), 1e-5f);
            Assert.AreEqual(18f, CameraRigModel.ZoomSize(20f, 12f, 24f, 40f), 1e-5f);
            Assert.AreEqual(24f, CameraRigModel.ZoomSize(400f, 12f, 24f, 40f), 1e-5f);
        }

        [Test]
        public void ZoomSize_KeepsBase_WhenFullSpeedIsNotPositive()
        {
            Assert.AreEqual(12f, CameraRigModel.ZoomSize(30f, 12f, 24f, 0f), 1e-5f);
        }

        [Test]
        public void StarOffset_StaysWithinHalfSpanOfCamera()
        {
            for (float cam = -500f; cam <= 500f; cam += 37.3f)
            {
                float x = CameraRigModel.StarOffset(12f, cam, 0.3f, 100f);
                Assert.LessOrEqual(System.Math.Abs(x - cam), 50f + 1e-3f);
            }
        }

        [Test]
        public void StarOffset_DriftsAtParallaxTimesCamera()
        {
            float before = CameraRigModel.StarOffset(40f, 0f, 0.5f, 1000f);
            float after = CameraRigModel.StarOffset(40f, 10f, 0.5f, 1000f);
            // Camera moved +10, star moved +10 - 0.5*10 = +5 on screen terms.
            Assert.AreEqual(5f, after - before, 1e-4f);
        }
    }
}
