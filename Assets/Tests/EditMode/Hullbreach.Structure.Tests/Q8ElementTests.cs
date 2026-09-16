using System;
using NUnit.Framework;
using Hullbreach.Structure;

namespace Hullbreach.Structure.Tests
{
    /// <summary>
    /// These are cheap and decisive. Write them BEFORE the solver: a KHat with
    /// the wrong null space is always a bug, and finding it here takes minutes
    /// whereas finding it through wrong-looking stress takes days.
    /// </summary>
    public class Q8ElementTests
    {
        const float Tol = 1e-4f;
        const float Nu = 0.3f;
        const float H = 1f;

        static float[,] KHat()
        {
            var k = new float[Q8Element.DofCount, Q8Element.DofCount];
            Q8Element.UnitStiffness(Nu, H, k);
            return k;
        }

        static float Quadratic(float[,] k, float[] v)
        {
            float sum = 0f;
            for (int i = 0; i < v.Length; i++)
            for (int j = 0; j < v.Length; j++)
                sum += v[i] * k[i, j] * v[j];
            return sum;
        }

        [Test]
        public void ShapeFunctions_SumToOneEverywhere()
        {
            // Partition of unity. Fails if any shape function is mistyped.
            var n = new float[8];
            foreach (var xi in new[] { -1f, -0.3f, 0f, 0.7f, 1f })
            foreach (var eta in new[] { -1f, -0.5f, 0f, 0.4f, 1f })
            {
                Q8Element.ShapeFunctions(xi, eta, n);
                float sum = 0f;
                foreach (var v in n) sum += v;
                Assert.AreEqual(1f, sum, Tol, $"at ({xi},{eta})");
            }
        }

        [Test]
        public void ShapeFunctions_AreKroneckerDeltaAtNodes()
        {
            // N_i must be 1 at node i and 0 at every other node.
            var n = new float[8];
            for (int node = 0; node < 8; node++)
            {
                Q8Element.ShapeFunctions(Q8Element.ReferenceNodes[node, 0],
                                         Q8Element.ReferenceNodes[node, 1], n);
                for (int i = 0; i < 8; i++)
                    Assert.AreEqual(i == node ? 1f : 0f, n[i], Tol,
                        $"N{i} evaluated at node {node}");
            }
        }

        [Test]
        public void UnitStiffness_IsSymmetric()
        {
            // K = integral of B^T D B is symmetric by construction. An
            // asymmetry means a bug in B or in the quadrature loop.
            var k = KHat();
            for (int i = 0; i < Q8Element.DofCount; i++)
            for (int j = 0; j < Q8Element.DofCount; j++)
                Assert.AreEqual(k[i, j], k[j, i], Tol, $"K[{i},{j}] != K[{j},{i}]");
        }

        [Test]
        public void UnitStiffness_AnnihilatesRigidBodyModes()
        {
            // A free element has exactly three zero-energy modes in 2D.
            // Rigid motion produces no strain, so it must produce no energy.
            var k = KHat();

            foreach (var mode in RigidModes())
                Assert.AreEqual(0f, Quadratic(k, mode), Tol,
                    "rigid-body mode stored energy");
        }

        [Test]
        public void UnitStiffness_IsPositiveSemiDefinite()
        {
            var k = KHat();
            var rng = new Random(12345);
            var v = new float[Q8Element.DofCount];

            for (int trial = 0; trial < 200; trial++)
            {
                for (int i = 0; i < v.Length; i++)
                    v[i] = (float)(rng.NextDouble() * 2.0 - 1.0);

                Assert.GreaterOrEqual(Quadratic(k, v), -Tol,
                    "stiffness must never store negative energy");
            }
        }

        [Test]
        public void UnitStiffness_StoresEnergyForAStretch()
        {
            // Uniform stretch in x is a genuine deformation, so it MUST cost
            // energy. Together with the rigid-mode test this pins the null
            // space to exactly the three modes it should be.
            var k = KHat();
            var v = new float[Q8Element.DofCount];
            for (int i = 0; i < 8; i++)
                v[2 * i] = Q8Element.ReferenceNodes[i, 0];   // u_x = x

            Assert.Greater(Quadratic(k, v), Tol);
        }

        // TODO [C2, optional but stronger]: replace the two tests above with a
        //       symmetric eigensolver (Jacobi rotation is ~40 lines) and assert
        //       EXACTLY three eigenvalues are zero. More zeros means a spurious
        //       mechanism, which is what 2x2 reduced integration would give you.

        static float[][] RigidModes()
        {
            var tx = new float[Q8Element.DofCount];
            var ty = new float[Q8Element.DofCount];
            var rot = new float[Q8Element.DofCount];

            for (int i = 0; i < 8; i++)
            {
                float x = Q8Element.ReferenceNodes[i, 0];
                float y = Q8Element.ReferenceNodes[i, 1];

                tx[2 * i] = 1f;                 // translate x
                ty[2 * i + 1] = 1f;             // translate y
                rot[2 * i] = -y;                // infinitesimal rotation
                rot[2 * i + 1] = x;
            }
            return new[] { tx, ty, rot };
        }
    }
}
