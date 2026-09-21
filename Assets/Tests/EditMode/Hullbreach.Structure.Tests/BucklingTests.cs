using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Structure;

namespace Hullbreach.Structure.Tests
{
    /// <summary>
    /// Coverage for GeometricStiffness + BucklingAnalysis + the
    /// StructuralSolver hook. Grids are kept small (a handful to a few dozen
    /// blocks) so the whole file runs in a couple of seconds even though
    /// buckling needs many ticks of subspace iteration to converge:
    /// BucklingEveryNTicks is set to 1 throughout so each Tick call spends
    /// its (default) 2 sweeps, instead of waiting out the production
    /// default of one attempt every 4 ticks.
    /// </summary>
    public class BucklingTests
    {
        const float Tol = 1e-3f;

        /// <summary>End-load magnitude shared by every column test, chosen by
        /// trial so an 8-block column's critical load factor lands within an
        /// order of magnitude of 1 (see the class doc on why the ratio test
        /// does not actually need this to be precise).</summary>
        const float ColumnEndForce = 0.02f;

        static BlockGrid Column(int n)
        {
            var grid = new BlockGrid();
            grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            for (int y = 1; y < n; y++)
                grid.TryAdd(BlockKey.Pack(0, y), new Block(BlockTypes.Hull));
            return grid;
        }

        /// <summary>Equal and opposite forces spread over the two end faces
        /// of a 1-wide, n-tall column, self-equilibrated so no inertia-relief
        /// artifact swamps the effect under test. Positive `magnitude`
        /// compresses the column (pushes the ends together); negative
        /// stretches it.</summary>
        static List<(float2, float2)> ColumnEndForces(int n, float magnitude)
        {
            return new List<(float2, float2)>
            {
                (new float2(0.05f, (float)n - 0.02f), new float2(0f, -magnitude)),
                (new float2(0.95f, (float)n - 0.02f), new float2(0f, -magnitude)),
                (new float2(0.05f, 0.02f), new float2(0f, magnitude)),
                (new float2(0.95f, 0.02f), new float2(0f, magnitude)),
            };
        }

        /// <summary>Ticks `solver` until it publishes a converged buckling
        /// analysis (BucklingEveryNTicks is expected to already be 1) or
        /// `maxTicks` is exhausted, whichever comes first: a tick-count
        /// bound, not a wall-clock one, matching BucklingAnalysis's own
        /// convergence contract. STOPS EARLY once the critical mode's
        /// LoadFactor has stopped changing for a few consecutive ticks:
        /// running the full `maxTicks` unconditionally (as this used to)
        /// meant every test paid for its worst-case tick budget even when
        /// the subspace settled in a fraction of it, which is most of why
        /// this file used to take well over a minute: see the class doc.
        /// </summary>
        static void RunUntilConverged(StructuralSolver solver, BlockGrid grid,
                                      List<(float2, float2)> forces, int maxTicks = 60)
        {
            float prev = float.NaN;
            int stable = 0;
            for (int i = 0; i < maxTicks; i++)
            {
                solver.Tick(grid, forces, 1f / 60f);
                if (solver.BucklingModes.Count == 0) continue;

                float cur = solver.BucklingModes[0].LoadFactor;
                stable = cur == prev ? stable + 1 : 0;
                prev = cur;
                if (stable >= 3) break;
            }
        }

        [Test]
        public void SmallBlob_HasNoLowLoadFactor()
        {
            // A 2x2 blob has no slender member to buckle: any positive
            // load factor should be large, not something combat-scale loads
            // would ever cross. Forces are spread over whole opposite edges
            // (not a single diagonal corner-to-corner point pair) so the
            // load reads as uniform compression rather than a concentrated
            // point load, which would create its own local stress
            // singularity unrelated to genuine buckling.
            var grid = new BlockGrid();
            grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            grid.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Hull));
            grid.TryAdd(BlockKey.Pack(0, 1), new Block(BlockTypes.Hull));
            grid.TryAdd(BlockKey.Pack(1, 1), new Block(BlockTypes.Hull));

            var solver = new StructuralSolver { BucklingEveryNTicks = 1, BucklingMaxSweepsPerTick = 8, BucklingModeCount = 3 };
            const float p = ColumnEndForce;
            var forces = new List<(float2, float2)>
            {
                (new float2(0.05f, 1.98f), new float2(0f, -p)),
                (new float2(1.95f, 1.98f), new float2(0f, -p)),
                (new float2(0.05f, 0.02f), new float2(0f, p)),
                (new float2(1.95f, 0.02f), new float2(0f, p)),
            };

            RunUntilConverged(solver, grid, forces, 60);

            foreach (var mode in solver.BucklingModes)
                Assert.Greater(mode.LoadFactor, 20f, "a stubby blob should not read as buckling-prone");
        }

        [Test]
        public void Column_CriticalLoadFactorScalesWithInverseLengthSquared()
        {
            // 16 vs 32, not 8 vs 16: subspace iteration on a coarse Q8 mesh
            // can cluster a short column's low buckling modes close enough
            // together that the block rotates between them for a long time
            // before settling (a documented characteristic of subspace
            // iteration with near-degenerate eigenvalues, not a correctness
            // bug); the longer, more slender columns below converge cleanly
            // well within the tick budgets used here.
            // 12 vs 24, not 16 vs 32: with the CgSolver fix below both pairs
            // reproduce the Euler trend, and the smaller pair converges in a
            // fraction of the tick budget, keeping this file's total runtime
            // well under the old multi-minute cost of running CG near its
            // (previously too-low) iteration cap.
            var grid12 = Column(12);
            var solver12 = new StructuralSolver { BucklingEveryNTicks = 1, BucklingMaxSweepsPerTick = 8, BucklingModeCount = 2 };
            RunUntilConverged(solver12, grid12, ColumnEndForces(12, ColumnEndForce), 40);

            var grid24 = Column(24);
            var solver24 = new StructuralSolver { BucklingEveryNTicks = 1, BucklingMaxSweepsPerTick = 8, BucklingModeCount = 2 };
            RunUntilConverged(solver24, grid24, ColumnEndForces(24, ColumnEndForce), 40);

            Assert.Greater(solver12.BucklingModes.Count, 0, "the 12-block column should have a positive load factor");
            Assert.Greater(solver24.BucklingModes.Count, 0, "the 24-block column should have a positive load factor");

            float lambda12 = solver12.BucklingModes[0].LoadFactor;
            float lambda24 = solver24.BucklingModes[0].LoadFactor;
            float ratio = lambda12 / lambda24;

            // Euler buckling on a slender beam predicts P_cr ~ 1/L^2, i.e. a
            // ratio near (24/12)^2 = 4 for doubling the length. Root cause of
            // the old, loosened "ratio > 1.5" assertion was CgSolver's
            // MaxIterations cap (200) being far below what these columns
            // actually need (measured ~650 CG iterations for a 32-block
            // column's 326 dof; see CgSolver's MaxIterations doc), which
            // under-converged K^-1 and biased the subspace iteration's
            // Rayleigh quotients enough to flatten the trend toward 1/L and
            // even invert it for longer columns. With that fixed the ratio
            // tracks the Euler exponent within the tolerance below.
            Assert.Greater(ratio, 3.6f,
                $"doubling the column length should roughly quarter the critical load factor (Euler P_cr ~ 1/L^2), got ratio {ratio}");
            Assert.Less(ratio, 4.5f,
                $"ratio {ratio} overshoots the Euler 1/L^2 prediction by more than the mesh/shear-correction tolerance allows");
        }

        [Test]
        public void Column_CriticalLoadFactorMatchesDenseOracle()
        {
            // Independent check on the SUBSPACE ITERATION itself (as opposed
            // to the trend test above, which is sensitive to the geometric
            // stiffness and load case too): take the converged stress state
            // StructuralSolver already produced, rebuild K/K_G against it
            // directly, and compare BucklingAnalysis's own iteration against
            // DenseEigenOracle's independent dense Cholesky+Jacobi solve of
            // the identical reduced eigenproblem. N=8 is skipped here (see
            // Column_CriticalLoadFactorScalesWithInverseLengthSquared's
            // class doc on short columns): its lowest two buckling modes
            // sit close enough together that which one the subspace settles
            // on first is a coin flip unrelated to either implementation
            // being wrong.
            foreach (int n in new[] { 12, 16 })
            {
                var grid = Column(n);
                var solver = new StructuralSolver { BucklingEveryNTicks = 1, BucklingMaxSweepsPerTick = 8, BucklingModeCount = 2 };
                RunUntilConverged(solver, grid, ColumnEndForces(n, ColumnEndForce), 40);
                Assert.Greater(solver.BucklingModes.Count, 0, $"N={n} should have a positive load factor");

                var assembly = new StiffnessAssembly();
                assembly.Rebuild(grid);
                var kg = new GeometricStiffness();
                kg.AttachSparsity(assembly);
                kg.Rebuild(grid, assembly, solver.BlockStresses);

                var rigid = new float[3][];
                rigid[0] = new float[assembly.DofCount];
                rigid[1] = new float[assembly.DofCount];
                rigid[2] = new float[assembly.DofCount];
                LoadVector.RigidBodyModes(assembly.NodeRestPositions, rigid);

                float oracleLambda = DenseEigenOracle.SmallestPositiveLambda(assembly, kg, rigid);
                float analysisLambda = solver.BucklingModes[0].LoadFactor;

                Assert.Less(Math.Abs(oracleLambda - analysisLambda) / oracleLambda, 0.10f,
                    $"N={n}: subspace iteration ({analysisLambda}) should agree with the dense oracle ({oracleLambda}) within 10%");
            }
        }

        [Test]
        public void Column_UnderTension_HasNoBucklingModes()
        {
            var grid = Column(16);
            var solver = new StructuralSolver { BucklingEveryNTicks = 1, BucklingMaxSweepsPerTick = 8, BucklingModeCount = 2 };
            var forces = ColumnEndForces(16, -ColumnEndForce); // negative = tension

            for (int i = 0; i < 30; i++) solver.Tick(grid, forces, 1f / 60f);

            Assert.AreEqual(0, solver.BucklingModes.Count);
            Assert.AreEqual(0, solver.BuckledBlocks.Count);
            Assert.IsTrue(float.IsPositiveInfinity(solver.CriticalLoadFactor));
        }

        [Test]
        public void TwoSeparateArms_UnderCompression_BuckleIndependently()
        {
            // A core with two slender single-wide arms going in different
            // directions. Both are compressed hard enough that each folds on
            // its own: the ship should read at least two sub-critical
            // modes, and their top-participation blocks should land on
            // different arms (a ship folding in two places at once).
            const int armLength = 8;
            var grid = new BlockGrid();
            grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            for (int y = 1; y <= armLength; y++)
                grid.TryAdd(BlockKey.Pack(0, y), new Block(BlockTypes.Hull)); // arm A: +y
            for (int x = 1; x <= armLength; x++)
                grid.TryAdd(BlockKey.Pack(x, 0), new Block(BlockTypes.Hull)); // arm B: +x

            const float p = 0.05f;
            var forces = new List<(float2, float2)>
            {
                // Squeeze arm A's tip toward the core.
                (new float2(0.05f, (float)armLength - 0.02f + 1f), new float2(0f, -p)),
                (new float2(0.95f, (float)armLength - 0.02f + 1f), new float2(0f, -p)),
                // Squeeze arm B's tip toward the core.
                (new float2((float)armLength - 0.02f + 1f, 0.05f), new float2(-p, 0f)),
                (new float2((float)armLength - 0.02f + 1f, 0.95f), new float2(-p, 0f)),
                // React equally at the core so the load is self-equilibrated.
                (new float2(0.05f, 0.02f), new float2(0f, p)),
                (new float2(0.95f, 0.02f), new float2(0f, p)),
                (new float2(0.02f, 0.05f), new float2(p, 0f)),
                (new float2(0.02f, 0.95f), new float2(p, 0f)),
            };

            var solver = new StructuralSolver { BucklingEveryNTicks = 1, BucklingMaxSweepsPerTick = 8, BucklingModeCount = 4 };
            RunUntilConverged(solver, grid, forces, 80);

            var subCritical = new List<BucklingMode>();
            foreach (var mode in solver.BucklingModes)
                if (mode.LoadFactor <= 1f) subCritical.Add(mode);

            Assert.GreaterOrEqual(subCritical.Count, 2,
                "two independently slender arms under enough compression should give at least two sub-critical modes");

            static int TopBlock(BucklingMode m)
            {
                int best = 0;
                float bestFrac = -1f;
                foreach (var kvp in m.BlockParticipation)
                    if (kvp.Value > bestFrac) { bestFrac = kvp.Value; best = kvp.Key; }
                return best;
            }

            static bool OnArmA(int key) { BlockKey.Unpack(key, out int x, out _); return x == 0; }

            int topA = TopBlock(subCritical[0]);
            int topB = TopBlock(subCritical[1]);

            // "On different arms" is exactly "on arm A" disagreeing between
            // the two: the core block (0,0) is on both by these
            // definitions, but a mode's TOP block is never the joint itself.
            bool oneOnEachArm = OnArmA(topA) != OnArmA(topB);
            Assert.IsTrue(oneOnEachArm,
                $"expected the two sub-critical modes' top blocks on different arms, got {topA} and {topB}");
        }

        [Test]
        public void ModeShape_IsOrthogonalToRigidBodyModes()
        {
            var grid = Column(16);
            var solver = new StructuralSolver { BucklingEveryNTicks = 1, BucklingMaxSweepsPerTick = 8, BucklingModeCount = 2 };
            RunUntilConverged(solver, grid, ColumnEndForces(10, ColumnEndForce));

            Assert.Greater(solver.BucklingModes.Count, 0);

            // Rebuild the same rigid modes StructuralSolver itself uses, off
            // an independent assembly over the identical grid.
            var assembly = new StiffnessAssembly();
            assembly.Rebuild(grid);
            var rigid = new float[3][];
            rigid[0] = new float[assembly.DofCount];
            rigid[1] = new float[assembly.DofCount];
            rigid[2] = new float[assembly.DofCount];
            LoadVector.RigidBodyModes(assembly.NodeRestPositions, rigid);

            var shape = solver.BucklingModes[0].Shape;
            Assert.AreEqual(assembly.DofCount, shape.Length);

            foreach (var mode in rigid)
            {
                float dot = 0f, modeNorm = 0f, shapeNorm = 0f;
                for (int i = 0; i < shape.Length; i++)
                {
                    dot += shape[i] * mode[i];
                    modeNorm += mode[i] * mode[i];
                    shapeNorm += shape[i] * shape[i];
                }
                float cos = dot / (Mathf_Sqrt(modeNorm) * Mathf_Sqrt(shapeNorm) + 1e-12f);
                Assert.AreEqual(0f, cos, 0.05f, "buckling mode shape must be orthogonal to every rigid-body mode");
            }
        }

        static float Mathf_Sqrt(float v) => (float)System.Math.Sqrt(v);

        [Test]
        public void SolverHook_PopulatesModesUnderHighLoadAndNotUnderLowLoad()
        {
            var gridHigh = Column(16);
            var solverHigh = new StructuralSolver { BucklingEveryNTicks = 1, BucklingMaxSweepsPerTick = 8, BucklingModeCount = 2 };
            RunUntilConverged(solverHigh, gridHigh, ColumnEndForces(10, ColumnEndForce));

            Assert.Greater(solverHigh.BucklingModes.Count, 0);
            // Only assert BuckledBlocks non-empty when the load actually put
            // a mode sub-critical; the load above is calibrated so the first
            // mode is not far from 1, but to keep this test robust to that
            // calibration, drive the load up further until one clearly is.
            var solverVeryHigh = new StructuralSolver { BucklingEveryNTicks = 1, BucklingMaxSweepsPerTick = 8, BucklingModeCount = 2 };
            RunUntilConverged(solverVeryHigh, gridHigh, ColumnEndForces(10, ColumnEndForce * 50f));
            Assert.Greater(solverVeryHigh.BucklingModes.Count, 0);
            Assert.LessOrEqual(solverVeryHigh.BucklingModes[0].LoadFactor, 1f,
                "a 50x-scaled end load should be past this column's critical load");
            Assert.Greater(solverVeryHigh.BuckledBlocks.Count, 0);

            var gridLow = Column(16);
            var solverLow = new StructuralSolver { BucklingEveryNTicks = 1, BucklingMaxSweepsPerTick = 8, BucklingModeCount = 2 };
            var tinyForces = ColumnEndForces(10, ColumnEndForce * 1e-4f);
            RunUntilConverged(solverLow, gridLow, tinyForces);
            Assert.AreEqual(0, solverLow.BuckledBlocks.Count,
                "a negligible load should not cross any critical load factor");
        }

        [Test]
        public void Analysis_IsBitDeterministic()
        {
            var gridA = Column(16);
            var gridB = Column(16);
            var forces = ColumnEndForces(10, ColumnEndForce * 50f);

            var solverA = new StructuralSolver { BucklingEveryNTicks = 1, BucklingMaxSweepsPerTick = 8, BucklingModeCount = 2 };
            var solverB = new StructuralSolver { BucklingEveryNTicks = 1, BucklingMaxSweepsPerTick = 8, BucklingModeCount = 2 };

            for (int i = 0; i < 20; i++)
            {
                solverA.Tick(gridA, forces, 1f / 60f);
                solverB.Tick(gridB, forces, 1f / 60f);
            }

            Assert.Greater(solverA.BucklingModes.Count, 0);
            Assert.AreEqual(solverA.BucklingModes.Count, solverB.BucklingModes.Count);
            for (int i = 0; i < solverA.BucklingModes.Count; i++)
                Assert.AreEqual(solverA.BucklingModes[i].LoadFactor, solverB.BucklingModes[i].LoadFactor,
                    "identical inputs must give bit-identical load factors within one machine");

            CollectionAssert.AreEqual(solverA.BuckledBlocks, solverB.BuckledBlocks);
        }
    }
}
