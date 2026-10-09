using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Structure;

namespace Hullbreach.Structure.Tests
{
    // End-to-end coverage across NodeLattice/StiffnessAssembly/LoadVector/
    // CgSolver: catches what a single-file unit test cannot.
    public class FemTests
    {
        const float Tol = 1e-3f;

        static BlockGrid TwoByThreeShip()
        {
            var grid = new BlockGrid();
            grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 3; y++)
            {
                if (x == 0 && y == 0) continue;
                grid.TryAdd(BlockKey.Pack(x, y), new Block(BlockTypes.Hull));
            }
            return grid;
        }

        static float[] Scatter(LoadVector loads, StiffnessAssembly assembly, float2 point, float2 force, out bool ok)
        {
            var f = new float[assembly.DofCount];
            ok = loads.AddPointForce(f, point, force);
            return f;
        }

        static float2 Sum(float[] f)
        {
            float2 s = float2.zero;
            for (int i = 0; i < f.Length; i += 2) s += new float2(f[i], f[i + 1]);
            return s;
        }

        [Test]
        public void AddPointForce_OnAbsentBlock_ConservesForceOverPresentNodes()
        {
            var grid = TwoByThreeShip();
            var assembly = new StiffnessAssembly();
            assembly.Rebuild(grid);
            var loads = new LoadVector(assembly);

            // Block (2,0) is absent but shares its x=2 edge nodes with (1,0).
            var f = Scatter(loads, assembly, new float2(2.1f, 0.5f), new float2(5f, -3f), out bool ok);

            Assert.IsTrue(ok);
            Assert.AreEqual(5f, Sum(f).x, 1e-3f);
            Assert.AreEqual(-3f, Sum(f).y, 1e-3f);
        }

        [Test]
        public void AddPointForce_OffStructureOrNonFinite_IsRejectedAndLeavesTargetUntouched()
        {
            var grid = TwoByThreeShip();
            var assembly = new StiffnessAssembly();
            assembly.Rebuild(grid);
            var loads = new LoadVector(assembly);

            foreach (var p in new[] { new float2(40.5f, 40.5f), new float2(float.NaN, 0.5f), new float2(float.PositiveInfinity, 0f), new float2(1e30f, 0f) })
            {
                var f = Scatter(loads, assembly, p, new float2(5f, -3f), out bool ok);
                Assert.IsFalse(ok, $"point {p} should be rejected");
                Assert.AreEqual(float2.zero, Sum(f));
            }
        }

        [Test]
        public void InertiaRelief_LeavesLoadOrthogonalToRigidModes()
        {
            var grid = TwoByThreeShip();
            var assembly = new StiffnessAssembly();
            assembly.Rebuild(grid);

            var loads = new LoadVector(assembly);
            var f = new float[assembly.DofCount];
            loads.QuasiStatic = f;

            // An off-axis point force, guaranteed to have nonzero net force
            // and torque before relief is applied.
            loads.AddPointForce(f, new float2(1.7f, 0.3f), new float2(5f, -3f));

            loads.ApplyInertiaRelief(f, grid, out _, out _);

            var modes = new float[3][];
            modes[0] = new float[assembly.DofCount];
            modes[1] = new float[assembly.DofCount];
            modes[2] = new float[assembly.DofCount];
            LoadVector.RigidBodyModes(assembly.NodeRestPositions, modes);

            foreach (var mode in modes)
            {
                float dot = 0f;
                for (int i = 0; i < f.Length; i++) dot += f[i] * mode[i];
                Assert.AreEqual(0f, dot, 1e-2f, "load must be orthogonal to every rigid-body mode");
            }
        }

        [Test]
        public void CoreAtCenter_HasHigherStressThanTheEnds()
        {
            // A 3-block bar, core in the middle, pulled apart from both
            // ends: the stiffness DISCONTINUITY at the core concentrates stress.
            var grid = new BlockGrid();
            grid.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Core));
            grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Hull));
            grid.TryAdd(BlockKey.Pack(2, 0), new Block(BlockTypes.Hull));

            var solver = new StructuralSolver();
            var forces = new List<(float2, float2)>
            {
                (new float2(2.95f, 0.05f), new float2(5f, 0f)),
                (new float2(2.95f, 0.95f), new float2(5f, 0f)),
                (new float2(0.05f, 0.05f), new float2(-5f, 0f)),
                (new float2(0.05f, 0.95f), new float2(-5f, 0f)),
            };
            solver.Tick(grid, forces, 1f / 60f);

            var centerStress = solver.BlockStresses[BlockKey.Pack(1, 0)].VonMises;
            var endStressA = solver.BlockStresses[BlockKey.Pack(0, 0)].VonMises;
            var endStressB = solver.BlockStresses[BlockKey.Pack(2, 0)].VonMises;

            Assert.Greater(centerStress, endStressA);
            Assert.Greater(centerStress, endStressB);
        }

        [Test]
        public void Cg_ConvergesOnASmallShip()
        {
            var grid = TwoByThreeShip();
            var assembly = new StiffnessAssembly();
            assembly.Rebuild(grid);

            var loads = new LoadVector(assembly);
            var f = new float[assembly.DofCount];
            loads.QuasiStatic = f;
            loads.AddPointForce(f, new float2(1.9f, 2.9f), new float2(3f, 2f));
            loads.ApplyInertiaRelief(f, grid, out _, out _);

            var modes = new float[3][];
            modes[0] = new float[assembly.DofCount];
            modes[1] = new float[assembly.DofCount];
            modes[2] = new float[assembly.DofCount];
            LoadVector.RigidBodyModes(assembly.NodeRestPositions, modes);

            var u = new float[assembly.DofCount];
            var solver = new CgSolver { MaxIterations = 500, Tolerance = 1e-5f };
            solver.Solve(assembly, f, u, modes);

            var residual = new float[assembly.DofCount];
            assembly.Multiply(u, residual);
            float residualNorm = 0f;
            float fNorm = 0f;
            for (int i = 0; i < residual.Length; i++)
            {
                float r = f[i] - residual[i];
                residualNorm += r * r;
                fNorm += f[i] * f[i];
            }
            residualNorm = math.sqrt(residualNorm);
            fNorm = math.sqrt(math.max(1f, fNorm));

            Assert.Less(residualNorm / fNorm, 1e-3f);
            Assert.Less(solver.LastIterationCount, solver.MaxIterations);
        }

        [Test]
        public void NodeMap_IsDeterministicAcrossTwoBuilds()
        {
            var gridA = TwoByThreeShip();
            var gridB = TwoByThreeShip();

            var mapA = new Dictionary<int, int>();
            var mapB = new Dictionary<int, int>();
            NodeLattice.BuildNodeMap(gridA, mapA);
            NodeLattice.BuildNodeMap(gridB, mapB);

            Assert.AreEqual(mapA.Count, mapB.Count);
            foreach (var kvp in mapA)
            {
                Assert.IsTrue(mapB.TryGetValue(kvp.Key, out int denseB));
                Assert.AreEqual(kvp.Value, denseB);
            }
        }
    }
}
