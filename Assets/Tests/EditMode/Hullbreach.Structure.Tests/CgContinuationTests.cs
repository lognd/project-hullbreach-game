using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Structure;

namespace Hullbreach.Structure.Tests
{
    // Covers CgState: a per-tick iteration budget COMPOUNDS across ticks
    // under a steady load, and a real load change restarts.
    public class CgContinuationTests
    {
        // Plate plus a 1-wide arm (same shape as SolverBenchmarks): a
        // slender member makes CG's width-scaling visible.
        static BlockGrid BuildArmShip(int side, int armLength, out float2 tip)
        {
            var grid = new BlockGrid();
            int coreX = side / 2, coreY = side / 2;
            for (int x = 0; x < side; x++)
            for (int y = 0; y < side; y++)
            {
                var type = (x == coreX && y == coreY) ? BlockTypes.Core : BlockTypes.Hull;
                grid.TryAdd(BlockKey.Pack(x, y), new Block(type));
            }

            int tipX = side - 1;
            for (int i = 1; i <= armLength; i++)
            {
                tipX = side - 1 + i;
                grid.TryAdd(BlockKey.Pack(tipX, coreY), new Block(BlockTypes.Hull));
            }

            tip = new float2(tipX + 0.5f, coreY + 0.5f);
            grid.ClearDirty();
            return grid;
        }

        static StructuralSolver NewSolver(int budget)
        {
            var solver = new StructuralSolver();
            // Buckling is irrelevant to the Krylov question and costs
            // whole seconds on this ship; keep the test about CG.
            solver.BucklingEnabled = false;
            solver.MaxCgIterationsPerTick = budget;
            return solver;
        }

        [Test]
        public void SteadyLoad_BudgetedTicksCostNoMoreIterationsThanOneUnbudgetedSolve()
        {
            var grid = BuildArmShip(6, 24, out float2 tip);
            var forces = new List<(float2, float2)> { (tip, new float2(0f, -50f)) };

            // Reference: one solve with no meaningful per-tick cap.
            var reference = NewSolver(100000);
            reference.Tick(grid, forces, 1f / 60f);
            Assert.IsTrue(reference.Converged, "reference solve did not converge; the test ship is mis-sized");
            int referenceIterations = reference.IterationsThisTick;

            // Budgeted: the same solve, 50 iterations at a time; slices
            // should sum to about the reference count if state carries over.
            var budgeted = NewSolver(50);
            int total = 0;
            int ticks = 0;
            const int maxTicks = 200;
            while (!budgeted.Converged && ticks < maxTicks)
            {
                budgeted.Tick(grid, forces, 1f / 60f);
                total += budgeted.IterationsThisTick;
                ticks++;
            }

            TestContext.Out.WriteLine(
                $"continuation: reference={referenceIterations} budgeted={total} over {ticks} ticks " +
                $"(continued={budgeted.ContinuedFromLastTick} ticksSinceRestart={budgeted.TicksSinceRestart})");

            Assert.IsTrue(budgeted.Converged, $"budgeted solve never converged in {maxTicks} ticks");
            Assert.LessOrEqual(total, (int)Math.Ceiling(referenceIterations * 1.10),
                "budgeted ticks spent more than 10% extra iterations: the Krylov subspace is not being continued");
            Assert.IsTrue(budgeted.TicksSinceRestart >= ticks - 1,
                "the budgeted run restarted mid-way, so it was not one continued CG");
        }

        [Test]
        public void SteadyLoad_SecondTickContinues()
        {
            var grid = BuildArmShip(6, 24, out float2 tip);
            var forces = new List<(float2, float2)> { (tip, new float2(0f, -50f)) };

            var solver = NewSolver(50);
            solver.Tick(grid, forces, 1f / 60f);
            Assert.IsFalse(solver.ContinuedFromLastTick, "the first tick has nothing to continue from");
            Assert.AreEqual(0, solver.TicksSinceRestart);

            solver.Tick(grid, forces, 1f / 60f);
            Assert.IsTrue(solver.ContinuedFromLastTick, "an unchanged load should continue, not restart");
            Assert.AreEqual(1, solver.TicksSinceRestart);
        }

        [Test]
        public void ChangedLoad_ForcesARestart()
        {
            var grid = BuildArmShip(6, 24, out float2 tip);
            var steady = new List<(float2, float2)> { (tip, new float2(0f, -50f)) };
            var changed = new List<(float2, float2)> { (tip, new float2(0f, -80f)) };

            var solver = NewSolver(50);
            solver.Tick(grid, steady, 1f / 60f);
            solver.Tick(grid, steady, 1f / 60f);
            Assert.IsTrue(solver.ContinuedFromLastTick);

            solver.Tick(grid, changed, 1f / 60f);
            Assert.IsFalse(solver.ContinuedFromLastTick,
                "a changed load is a different linear system and must restart the subspace");
            Assert.AreEqual(0, solver.TicksSinceRestart);

            // ...and continuation resumes once the new load is the steady one.
            solver.Tick(grid, changed, 1f / 60f);
            Assert.IsTrue(solver.ContinuedFromLastTick);
            Assert.AreEqual(1, solver.TicksSinceRestart);
        }

        [Test]
        public void TopologyChange_ForcesARestart()
        {
            var grid = BuildArmShip(6, 24, out float2 tip);
            var forces = new List<(float2, float2)> { (tip, new float2(0f, -50f)) };

            var solver = NewSolver(50);
            solver.Tick(grid, forces, 1f / 60f);
            solver.Tick(grid, forces, 1f / 60f);
            Assert.IsTrue(solver.ContinuedFromLastTick);

            // K itself changes here, which CgState cannot detect on its own:
            // StructuralSolver must invalidate on the rebuild it performs.
            solver.MarkTopologyChanged();
            solver.Tick(grid, forces, 1f / 60f);
            Assert.IsFalse(solver.ContinuedFromLastTick,
                "a stiffness rebuild must invalidate the stored Krylov state");
        }

        [Test]
        public void ContinuedSolve_ReachesTheSameDisplacementField()
        {
            var grid = BuildArmShip(6, 24, out float2 tip);
            var forces = new List<(float2, float2)> { (tip, new float2(0f, -50f)) };

            var reference = NewSolver(100000);
            reference.Tick(grid, forces, 1f / 60f);

            var budgeted = NewSolver(50);
            for (int i = 0; i < 200 && !budgeted.Converged; i++)
                budgeted.Tick(grid, forces, 1f / 60f);

            Assert.IsTrue(budgeted.Converged);

            // Converged is converged: continuation must not trade accuracy
            // for iteration count. Compare the published stress field.
            foreach (var kvp in reference.BlockStresses)
            {
                var got = budgeted.BlockStresses[kvp.Key];
                float scale = Math.Max(1e-4f, Math.Abs(kvp.Value.VonMises));
                Assert.That(Math.Abs(got.VonMises - kvp.Value.VonMises) / scale, Is.LessThan(0.02f),
                    $"von Mises disagrees at block {kvp.Key}");
            }
        }
    }
}
