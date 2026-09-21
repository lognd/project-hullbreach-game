using System;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Structure;

namespace Hullbreach.Structure.Tests
{
    /// <summary>
    /// Covers CoarsePreconditioner's three load-bearing guarantees: the
    /// combined M^-1 stays a symmetric operator (required for PCG's
    /// convergence theory to still apply), the coarse correction is exactly
    /// zero on the rigid-body modes (the property that avoids the
    /// Cholesky-of-a-singular-Kc NaN a previous attempt hit, see
    /// docs/roadmap.md), and it actually cuts iteration count on a case
    /// built to need many (a slender 1-wide arm, see CgSolver's doc on why
    /// width drives iteration count).
    /// </summary>
    public class CoarsePreconditionerTests
    {
        /// <summary>Local Euclidean dot product: CgSolver.Dot is internal
        /// (a different assembly than this edit-mode test project), so this
        /// is a tiny, test-only duplicate rather than exposing solver
        /// internals just for a test helper.</summary>
        static float Dot(float[] a, float[] b)
        {
            float s = 0f;
            for (int i = 0; i < a.Length; i++) s += a[i] * b[i];
            return s;
        }

        static BlockGrid BuildArm(int length)
        {
            var grid = new BlockGrid();
            for (int x = 0; x < length; x++)
                grid.TryAdd(BlockKey.Pack(x, 0), new Block(x == 0 ? BlockTypes.Core : BlockTypes.Hull));
            grid.ClearDirty();
            return grid;
        }

        [Test]
        public void CombinedPreconditioner_IsSymmetric()
        {
            var grid = BuildArm(32);
            var asm = new StiffnessAssembly();
            asm.Rebuild(grid);
            int n = asm.DofCount;

            var coarse = new CoarsePreconditioner();
            coarse.Rebuild(asm);

            var diag = new float[n];
            asm.Diagonal(diag);

            var rnd = new System.Random(7);
            var v = new float[n];
            var w = new float[n];
            for (int i = 0; i < n; i++) { v[i] = (float)(rnd.NextDouble() * 2 - 1); w[i] = (float)(rnd.NextDouble() * 2 - 1); }

            // M^-1 v (Jacobi + coarse, same combination CgSolver applies).
            var mInvV = new float[n];
            for (int i = 0; i < n; i++) mInvV[i] = diag[i] > 1e-12f ? v[i] / diag[i] : v[i];
            coarse.ApplyAdditive(v, mInvV);

            var mInvW = new float[n];
            for (int i = 0; i < n; i++) mInvW[i] = diag[i] > 1e-12f ? w[i] / diag[i] : w[i];
            coarse.ApplyAdditive(w, mInvW);

            float vtMw = Dot(v, mInvW);
            float wtMv = Dot(w, mInvV);

            Assert.AreEqual(vtMw, wtMv, Math.Max(1e-3f, Math.Abs(vtMw) * 1e-4f),
                "v.(M^-1 w) should equal w.(M^-1 v): M^-1 must stay symmetric for PCG's guarantees to hold");
        }

        [Test]
        public void CoarseCorrection_AnnihilatesRigidModes()
        {
            var grid = BuildArm(16);
            var asm = new StiffnessAssembly();
            asm.Rebuild(grid);
            int n = asm.DofCount;

            var coarse = new CoarsePreconditioner();
            coarse.Rebuild(asm);

            var modes = new float[3][] { new float[n], new float[n], new float[n] };
            LoadVector.RigidBodyModes(asm.NodeRestPositions, modes);

            foreach (var mode in modes)
            {
                var z = new float[n]; // zero Jacobi term: isolate the coarse piece
                coarse.ApplyAdditive(mode, z);

                float maxAbs = 0f;
                for (int i = 0; i < n; i++) maxAbs = Math.Max(maxAbs, Math.Abs(z[i]));

                // Scale-relative, and TIGHT: the correction now projects
                // its own input and output onto the complement of these
                // exact modes (see ApplyAdditive), so a rigid mode maps to
                // zero by construction rather than by Kc^+'s eigenvalue
                // floor happening to drop the right directions. The old
                // 5e-3 bound was a data-dependent accommodation of that
                // floor's leftovers and sat only a few percent above what
                // Mono actually produced (0.0496 measured against a 0.0456
                // effective threshold on this grid); 1e-4 relative is a
                // real assertion about the construction instead.
                float modeNorm = (float)Math.Sqrt(Dot(mode, mode));
                Assert.Less(maxAbs, 1e-4f * Math.Max(1f, modeNorm),
                    "coarse correction of a rigid mode should be ~0: it must be projected off them by construction");
            }
        }

        [Test]
        public void CoarsePreconditioner_CutsIterationsOnASlenderArm()
        {
            var grid = BuildArm(32);
            var asm = new StiffnessAssembly();
            asm.Rebuild(grid);
            int n = asm.DofCount;

            var modes = new float[3][] { new float[n], new float[n], new float[n] };
            LoadVector.RigidBodyModes(asm.NodeRestPositions, modes);

            var f = new float[n];
            f[n - 2] = -1f; // point load at the far end of the arm

            var jacobiSolver = new CgSolver { MaxIterations = 20000, Tolerance = 1e-3f };
            var uJacobi = new float[n];
            jacobiSolver.Solve(asm, f, uJacobi, modes);
            Assert.IsTrue(jacobiSolver.Converged, "Jacobi-only baseline should still converge given enough iterations");

            var coarse = new CoarsePreconditioner();
            coarse.Rebuild(asm);

            var coarseSolver = new CgSolver { MaxIterations = 20000, Tolerance = 1e-3f };
            var uCoarse = new float[n];
            coarseSolver.Solve(asm, f, uCoarse, modes, coarse);
            Assert.IsTrue(coarseSolver.Converged, "coarse-augmented solve should converge within the same generous cap");

            TestContext.Out.WriteLine($"jacobiIters={jacobiSolver.LastIterationCount} coarseIters={coarseSolver.LastIterationCount}");
            Assert.LessOrEqual(coarseSolver.LastIterationCount * 3, jacobiSolver.LastIterationCount,
                "the coarse correction should need at least 3x fewer iterations than plain Jacobi on a slender arm");
        }
    }
}
