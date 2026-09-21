using System;
using NUnit.Framework;
using Hullbreach.Structure;

namespace Hullbreach.Structure.Tests
{
    public class StressCriteriaTests
    {
        const float Tol = 1e-4f;
        const float S = 7f;

        [Test]
        public void UniaxialTension_VonMisesEqualsTheStress()
        {
            Assert.AreEqual(S, StressCriteria.VonMises(S, 0f, 0f), Tol);
        }

        [Test]
        public void PureShear_VonMisesIsRootThreeTimesTheShear()
        {
            Assert.AreEqual(S * (float)Math.Sqrt(3.0),
                            StressCriteria.VonMises(0f, 0f, S), Tol);
        }

        [Test]
        public void EquibiaxialTension_VonMisesEqualsTheStress()
        {
            // Surprising but correct in PLANE STRESS: sigma_zz is 0, so
            // equibiaxial in-plane tension is not hydrostatic and does not
            // cancel. sigma_vm = S, not 0.
            Assert.AreEqual(S, StressCriteria.VonMises(S, S, 0f), Tol);
        }

        [Test]
        public void UniaxialTension_MaxPrincipalIsTheStress()
        {
            StressCriteria.Principal(S, 0f, 0f, out var major, out var minor);
            Assert.AreEqual(S, major, Tol);
            Assert.AreEqual(0f, minor, Tol);
        }

        [Test]
        public void PureShear_PrincipalsAreEqualAndOpposite()
        {
            StressCriteria.Principal(0f, 0f, S, out var major, out var minor);
            Assert.AreEqual(S, major, Tol);
            Assert.AreEqual(-S, minor, Tol);
        }

        [Test]
        public void Compression_LooksIdenticalToDuctileButNotToBrittle()
        {
            // THE WHOLE POINT of running both criteria. Pure compression at -S
            // has the same von Mises as tension at +S (pressure-insensitive),
            // but its max principal is 0, so a brittle block shrugs it off
            // while a ductile one yields exactly as it would in tension.
            Assert.AreEqual(StressCriteria.VonMises(S, 0f, 0f),
                            StressCriteria.VonMises(-S, 0f, 0f), Tol);

            StressCriteria.Principal(-S, 0f, 0f, out var major, out var minor);
            Assert.AreEqual(0f, major, Tol);
            Assert.AreEqual(-S, minor, Tol);
        }

        [Test]
        public void ZeroStress_IsZeroEverywhere()
        {
            Assert.AreEqual(0f, StressCriteria.VonMises(0f, 0f, 0f), Tol);
            StressCriteria.Principal(0f, 0f, 0f, out var major, out var minor);
            Assert.AreEqual(0f, major, Tol);
            Assert.AreEqual(0f, minor, Tol);
        }
    }
}
