using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;

namespace Hullbreach.Core.Tests
{
    /// <summary>
    /// Every expected value here is derived analytically, so a failure means
    /// the code is wrong rather than the test being stale.
    /// </summary>
    public class MassPropertiesTests
    {
        const float Tol = 1e-4f;

        static float2 Center(int x, int y) => new float2(x + 0.5f, y + 0.5f);

        static float UnitSquareInertia(float mass)
            => MassProperties.RectangleInertia(mass, 1f, 1f);   // = mass / 6

        [Test]
        public void Empty_HasZeroMassAndNoNaN()
        {
            var m = new MassProperties();
            Assert.AreEqual(0f, m.Total, Tol);
            Assert.AreEqual(0f, m.CenterOfMass.x, Tol);
            Assert.AreEqual(0f, m.CenterOfMass.y, Tol);
        }

        [Test]
        public void RectangleInertia_MatchesClosedForm()
        {
            // I = m (w^2 + h^2) / 12; a unit square of mass 1 gives 1/6.
            Assert.AreEqual(1f / 6f, UnitSquareInertia(1f), Tol);
            Assert.AreEqual(8f * (16f + 4f) / 12f,
                            MassProperties.RectangleInertia(8f, 4f, 2f), Tol);
        }

        [Test]
        public void SingleBlockAtOrigin_HasCenterAtBlockCenter()
        {
            var m = new MassProperties();
            m.Add(1f, Center(0, 0), UnitSquareInertia(1f));

            Assert.AreEqual(1f, m.Total, Tol);
            Assert.AreEqual(0.5f, m.CenterOfMass.x, Tol);
            Assert.AreEqual(0.5f, m.CenterOfMass.y, Tol);
            Assert.AreEqual(1f / 6f, m.InertiaAboutCenterOfMass, Tol);
        }

        [Test]
        public void FourByTwoSlab_MatchesAnalyticSlab()
        {
            // 8 unit blocks of mass 1 filling [0,4] x [0,2].
            // Total mass 8, center (2,1), and because the assembly IS a solid
            // 4x2 rectangle, its inertia must equal the closed form for one:
            //     I = m (w^2 + h^2) / 12 = 8 * 20 / 12 = 40/3
            var m = new MassProperties();
            for (int x = 0; x < 4; x++)
            for (int y = 0; y < 2; y++)
                m.Add(1f, Center(x, y), UnitSquareInertia(1f));

            Assert.AreEqual(8f, m.Total, Tol);
            Assert.AreEqual(2f, m.CenterOfMass.x, Tol);
            Assert.AreEqual(1f, m.CenterOfMass.y, Tol);
            Assert.AreEqual(40f / 3f, m.InertiaAboutCenterOfMass, Tol);
        }

        [Test]
        public void Remove_ExactlyUndoesAdd()
        {
            // This is the property that makes removal O(1). If it does not hold,
            // the accumulators are not being kept about a fixed origin.
            var m = new MassProperties();
            for (int x = 0; x < 4; x++)
            for (int y = 0; y < 2; y++)
                m.Add(1f, Center(x, y), UnitSquareInertia(1f));

            m.Add(1f, Center(9, 9), UnitSquareInertia(1f));
            m.Remove(1f, Center(9, 9), UnitSquareInertia(1f));

            Assert.AreEqual(8f, m.Total, Tol);
            Assert.AreEqual(2f, m.CenterOfMass.x, Tol);
            Assert.AreEqual(1f, m.CenterOfMass.y, Tol);
            Assert.AreEqual(40f / 3f, m.InertiaAboutCenterOfMass, Tol);
        }

        [Test]
        public void DoublingMass_HalvesAcceleration()
        {
            // S39 acceptance criterion 2, expressed directly: with the same
            // applied force, a = F/m must halve when m doubles.
            var light = new MassProperties();
            light.Add(1f, Center(0, 0), UnitSquareInertia(1f));

            var heavy = new MassProperties();
            heavy.Add(1f, Center(0, 0), UnitSquareInertia(1f));
            heavy.Add(1f, Center(1, 0), UnitSquareInertia(1f));

            const float force = 10f;
            Assert.AreEqual((force / light.Total) / 2f, force / heavy.Total, Tol);
        }
    }
}
