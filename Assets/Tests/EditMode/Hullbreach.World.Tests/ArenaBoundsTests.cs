using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.World;

namespace Hullbreach.World.Tests
{
    // Pins the soft arena edge (S41-1): no push inside, a ramp in the margin, full push beyond.
    public class ArenaBoundsTests
    {
        static readonly ArenaBounds Bounds = new ArenaBounds(float2.zero, 100f, 20f, 8f);

        [Test]
        public void WellInside_NoPush()
        {
            Assert.AreEqual(0f, math.length(Bounds.PushAcceleration(new float2(50f, 0f))));
            Assert.IsFalse(Bounds.InPushZone(new float2(80f, 0f)));
        }

        [Test]
        public void InsideMargin_PushRampsUpTowardCenter()
        {
            float2 a = Bounds.PushAcceleration(new float2(90f, 0f));
            Assert.AreEqual(-4f, a.x, 1e-4f);
            Assert.AreEqual(0f, a.y, 1e-4f);
        }

        [Test]
        public void PastTheEdge_PushIsFullStrengthAndInward()
        {
            float2 a = Bounds.PushAcceleration(new float2(0f, 300f));
            Assert.AreEqual(-8f, a.y, 1e-4f);
        }

        [Test]
        public void ZeroMargin_IsAHardSwitchAtRadius()
        {
            var b = new ArenaBounds(float2.zero, 10f, 0f, 5f);
            Assert.IsFalse(b.InPushZone(new float2(9.9f, 0f)));
            Assert.AreEqual(5f, math.length(b.PushAcceleration(new float2(10.5f, 0f))), 1e-4f);
        }
    }
}
