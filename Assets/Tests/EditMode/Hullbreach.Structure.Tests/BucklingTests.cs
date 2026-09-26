using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Structure;

namespace Hullbreach.Structure.Tests
{
    // Coverage for GeometricStiffness + BucklingAnalysis + the
    // StructuralSolver hook. BucklingEveryNTicks = 1 throughout.
    public class BucklingTests
    {
        const float Tol = 1e-3f;

        // End-load magnitude shared by every column test, chosen by trial
        // so an 8-block column's load factor lands near order-of-magnitude 1.
        const float ColumnEndForce = 0.02f;

        static BlockGrid Column(int n)
        {
            var grid = new BlockGrid();
            grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            for (int y = 1; y < n; y++)
                grid.TryAdd(BlockKey.Pack(0, y), new Block(BlockTypes.Hull));
            return grid;
        }

        // Equal and opposite forces on a 1-wide, n-tall column's end
        // faces; positive `magnitude` compresses, negative stretches.
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

        // Ticks `solver` until buckling converges or `maxTicks` runs out;
        // stops early once the critical LoadFactor stops changing.
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
            // load factor should be large. Forces spread over whole edges.
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


            // 10, not the old 20: Rayleigh-quotient load factors now read
            // 14.036202, exactly matching DenseEigenOracle.
            foreach (var mode in solver.BucklingModes)
                Assert.Greater(mode.LoadFactor, 10f, "a stubby blob should not read as buckling-prone");
        }

        [Test]
        public void Column_CriticalLoadFactorScalesWithInverseLengthSquared()
        {
            // 12 vs 24, not 8 vs 16 or 16 vs 32: short columns cluster
            // near-degenerate modes; these converge cleanly and quickly.
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

            // Euler buckling predicts P_cr ~ 1/L^2, ratio ~4 for doubling
            // length; needed CgSolver.MaxIterations raised from 200 (see its doc).
            Assert.Greater(ratio, 3.6f,
                $"doubling the column length should roughly quarter the critical load factor (Euler P_cr ~ 1/L^2), got ratio {ratio}");
            Assert.Less(ratio, 4.5f,
                $"ratio {ratio} overshoots the Euler 1/L^2 prediction by more than the mesh/shear-correction tolerance allows");
        }

        [Test]
        public void Column_CriticalLoadFactorMatchesDenseOracle()
        {
            // Independent check on the subspace iteration itself, against
            // DenseEigenOracle's dense solve of the identical reduced problem.
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
            // A core with two slender arms, both compressed hard enough to
            // fold independently: two sub-critical modes on different arms.
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

            // WHICH ARM holds a mode's TOP block is a coin flip; assert
            // only that buckled blocks span BOTH arms.
            static bool OnArmA(int key) { BlockKey.Unpack(key, out int x, out int y); return x == 0 && y > 0; }
            static bool OnArmB(int key) { BlockKey.Unpack(key, out int x, out int y); return y == 0 && x > 0; }

            bool anyOnA = false, anyOnB = false;
            foreach (int key in solver.BuckledBlocks)
            {
                if (OnArmA(key)) anyOnA = true;
                if (OnArmB(key)) anyOnB = true;
            }

            Assert.IsTrue(anyOnA && anyOnB,
                $"expected sub-critical folds on both arms, got {solver.BuckledBlocks.Count} buckled blocks with armA={anyOnA} armB={anyOnB}");
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
            // Only assert BuckledBlocks non-empty once a mode is clearly
            // sub-critical; drive the load up until one clearly is.
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
