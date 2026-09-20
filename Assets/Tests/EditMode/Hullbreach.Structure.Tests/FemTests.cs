using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Structure;

namespace Hullbreach.Structure.Tests
{
    /// <summary>
    /// End-to-end coverage across NodeLattice/StiffnessAssembly/LoadVector/
    /// CgSolver: the things a unit test on a single file cannot catch, like a
    /// sign error in inertia relief or a solver that silently fails to
    /// converge.
    /// </summary>
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
            // A 3-block horizontal bar, core in the middle, pulled apart from
            // both ends with equal and opposite forces spread over each end
            // face (so the load is self-equilibrated tension, not a single
            // point load whose own local stress concentration would swamp
            // the effect being tested). The core is stiffer than the hull
            // either side of it, so the stiffness DISCONTINUITY at its two
            // interfaces concentrates stress there more than at the bar's
            // free ends.
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
