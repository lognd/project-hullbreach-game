using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    // The visual offset that hides reconcile snaps by decaying to zero.
    public class CorrectionSmootherTests
    {
        [Test]
        public void SmallCorrection_ShowsAsOffset_ThenDecaysToZero()
        {
            var s = new CorrectionSmoother();
            s.Add(new float2(0.5f, 0f), 0f);
            Assert.AreEqual(0.5f, s.PositionOffset.x, 1e-5f, "visual starts where it was, not at the new pose");

            float prev = s.PositionOffset.x;
            for (int i = 0; i < 10; i++)
            {
                s.Advance(0.02f);
                Assert.Less(s.PositionOffset.x, prev, "offset shrinks every step");
                prev = s.PositionOffset.x;
            }
            for (int i = 0; i < 100; i++) s.Advance(0.02f);
            Assert.AreEqual(0f, s.PositionOffset.x, 0f, "fully settled, not asymptotic");
        }

        [Test]
        public void LargeCorrection_Snaps_NoOffset()
        {
            var s = new CorrectionSmoother();
            s.Add(new float2(CorrectionSmoother.SnapDistance * 2f, 0f), 0f);
            Assert.AreEqual(0f, s.PositionOffset.x, 0f);
        }

        [Test]
        public void LargeRotationCorrection_Snaps()
        {
            var s = new CorrectionSmoother();
            s.Add(float2.zero, CorrectionSmoother.SnapAngle * 2f);
            Assert.AreEqual(0f, s.RotationOffset, 0f);
        }

        [Test]
        public void Corrections_Accumulate()
        {
            var s = new CorrectionSmoother();
            s.Add(new float2(0.1f, 0f), 0.01f);
            s.Add(new float2(0.1f, 0f), 0.01f);
            Assert.AreEqual(0.2f, s.PositionOffset.x, 1e-5f);
            Assert.AreEqual(0.02f, s.RotationOffset, 1e-5f);
        }

        [Test]
        public void Advance_IsFrameRateIndependent()
        {
            var a = new CorrectionSmoother();
            var b = new CorrectionSmoother();
            a.Add(new float2(1f, 0f), 0f);
            b.Add(new float2(1f, 0f), 0f);
            a.Advance(0.1f);
            for (int i = 0; i < 10; i++) b.Advance(0.01f);
            Assert.AreEqual(a.PositionOffset.x, b.PositionOffset.x, 1e-4f);
        }

        [Test]
        public void Clear_DropsTheOffset()
        {
            var s = new CorrectionSmoother();
            s.Add(new float2(0.3f, 0.3f), 0.1f);
            s.Clear();
            Assert.AreEqual(0f, math.length(s.PositionOffset), 0f);
            Assert.AreEqual(0f, s.RotationOffset, 0f);
        }
    }
}
