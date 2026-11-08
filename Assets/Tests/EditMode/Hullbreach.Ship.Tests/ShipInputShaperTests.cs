using NUnit.Framework;
using Hullbreach.Ship;

namespace Hullbreach.Ship.Tests
{
    // Dead zone and response curve applied to raw axes before they reach the sim.
    public class ShipInputShaperTests
    {
        const float Tol = 1e-4f;

        [Test]
        public void InsideDeadZone_IsExactlyZero()
        {
            Assert.AreEqual(0f, ShipInputShaper.Shape(0f), 0f);
            Assert.AreEqual(0f, ShipInputShaper.Shape(ShipInputShaper.DeadZone * 0.99f), 0f);
            Assert.AreEqual(0f, ShipInputShaper.Shape(-ShipInputShaper.DeadZone), 0f);
        }

        [Test]
        public void FullDeflection_StaysFull_InBothDirections()
        {
            Assert.AreEqual(1f, ShipInputShaper.Shape(1f), Tol);
            Assert.AreEqual(-1f, ShipInputShaper.Shape(-1f), Tol);
        }

        [Test]
        public void OutOfRange_IsClamped()
        {
            Assert.AreEqual(1f, ShipInputShaper.Shape(7f), Tol);
            Assert.AreEqual(-1f, ShipInputShaper.Shape(-7f), Tol);
        }

        [Test]
        public void JustPastDeadZone_StartsNearZero_NoJump()
        {
            float v = ShipInputShaper.Shape(ShipInputShaper.DeadZone + 0.01f);
            Assert.Greater(v, 0f);
            Assert.Less(v, 0.02f, "the rescale must remove the step at the dead-zone edge");
        }

        [Test]
        public void Response_IsMonotonic_AndOddSymmetric()
        {
            float prev = 0f;
            for (float x = 0.05f; x <= 1f; x += 0.05f)
            {
                float v = ShipInputShaper.Shape(x);
                Assert.GreaterOrEqual(v, prev);
                Assert.AreEqual(-v, ShipInputShaper.Shape(-x), Tol);
                prev = v;
            }
        }

        [Test]
        public void HalfDeflection_IsBelowLinear_ForFineControl()
        {
            Assert.Less(ShipInputShaper.Shape(0.5f), 0.5f);
        }

        [Test]
        public void NaN_ShapesToZero()
        {
            Assert.AreEqual(0f, ShipInputShaper.Shape(float.NaN), 0f);
        }

        [Test]
        public void ShapeInput_ShapesBothAxes_AndKeepsFire()
        {
            var shaped = ShipInputShaper.Shape(1f, -0.05f, true);
            Assert.AreEqual(1f, shaped.ThrustAxis, Tol);
            Assert.AreEqual(0f, shaped.Steer, 0f);
            Assert.IsTrue(shaped.FirePressed);
        }
    }
}
