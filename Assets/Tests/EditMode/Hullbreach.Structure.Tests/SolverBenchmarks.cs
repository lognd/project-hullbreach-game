using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Structure;

namespace Hullbreach.Structure.Tests
{
    // Benchmarks-as-tests: build a few ship sizes, run real ticks, and
    // report DOF/iterations/ms/sweeps to TestContext.
    public class SolverBenchmarks
    {
        // Per-tick allocation ceiling for the steady-state hot path;
        // generous enough to only catch a REGRESSION, not exact grid walks.
        const long AllocBudgetBytesPerTick = 4096;

        // Builds a roughly square plate plus a 1-wide arm split to either
        // side along x, so the ship is both wide and has a slender member.
        static BlockGrid BuildShip(int totalBlocks, out float2 thrusterPoint)
        {
            int plateBlocks = (int)(totalBlocks * 0.7f);
            int side = Math.Max(2, (int)Math.Sqrt(plateBlocks));
            int armBlocks = Math.Max(0, totalBlocks - side * side);
            int armEachSide = armBlocks / 2;

            var grid = new BlockGrid();
            int coreX = side / 2;
            int coreY = side / 2;

            for (int x = 0; x < side; x++)
            for (int y = 0; y < side; y++)
            {
                int key = BlockKey.Pack(x, y);
                var type = (x == coreX && y == coreY) ? BlockTypes.Core : BlockTypes.Hull;
                grid.TryAdd(key, new Block(type));
            }

            // Arm extends the plate along +x/-x at y = coreY: most of the
            // "width" CG has to cross information over.
            int armTipX = side - 1;
            for (int i = 1; i <= armEachSide && side + i - 1 <= BlockKey.Max; i++)
            {
                int x = side - 1 + i;
                grid.TryAdd(BlockKey.Pack(x, coreY), new Block(BlockTypes.Hull));
                armTipX = x;
            }
            int armBaseX = 0;
            for (int i = 1; i <= armEachSide && -i >= BlockKey.Min; i++)
            {
                int x = -i;
                grid.TryAdd(BlockKey.Pack(x, coreY), new Block(BlockTypes.Hull));
                armBaseX = x;
            }

            thrusterPoint = new float2(armTipX + 0.5f, coreY + 0.5f);
            return grid;
        }

        static void RunBenchmark(int totalBlocks, int ticks = 20)
        {
            var grid = BuildShip(totalBlocks, out float2 thrusterPoint);

            // The real game loop clears TopologyDirty elsewhere; skipping
            // this would force a full K rebuild every tick.
            grid.ClearDirty();

            var solver = new StructuralSolver();
            solver.BucklingEnabled = true;
            solver.BucklingEveryNTicks = 4;

            // Production default: measures REAL per-tick behavior,
            // including a ship too wide to fully settle within budget.

            var forces = new List<(float2, float2)>
            {
                (thrusterPoint, new float2(0f, -50f)),
            };

            // Per-tick wall clock for min/median/max: an average hides the
            // periodic spike on buckling-eligible ticks this benchmark catches.
            var tickMicros = new double[ticks];

            var sw = System.Diagnostics.Stopwatch.StartNew();
            long allocBefore = 0, allocAfter = 0;
            long maxTickAlloc = 0;
            int maxIterations = 0;

            for (int tick = 0; tick < ticks; tick++)
            {
                // Skip the one-time K rebuild (tick 0) and BucklingAnalysis's
                // first Reset; measuring only the last ticks avoids both.
                bool measureAlloc = tick >= ticks - 2;
                if (measureAlloc) allocBefore = GC.GetAllocatedBytesForCurrentThread();

                double tickStart = sw.Elapsed.TotalMilliseconds;
                solver.Tick(grid, forces, 1f / 60f);
                double tickMs = sw.Elapsed.TotalMilliseconds - tickStart;
                tickMicros[tick] = tickMs;

                if (measureAlloc)
                {
                    allocAfter = GC.GetAllocatedBytesForCurrentThread();
                    long tickAlloc = allocAfter - allocBefore;
                    if (tickAlloc > maxTickAlloc) maxTickAlloc = tickAlloc;
                }

                maxIterations = Math.Max(maxIterations, solver.IterationsThisTick);

                TestContext.Out.WriteLine(
                    $"blocks={totalBlocks} tick={tick} dof={solver.DofCount} " +
                    $"iterations={solver.IterationsThisTick} converged={solver.Converged} " +
                    $"residual={solver.ResidualNorm:g4} ms={tickMs:F2} " +
                    $"continued={solver.ContinuedFromLastTick} ticksSinceRestart={solver.TicksSinceRestart}");
            }

            sw.Stop();

            // STEADY STATE ONLY for the distribution: tick 0 pays the
            // one-time K/coarse rebuild, reported separately.
            var steady = new double[ticks - 1];
            Array.Copy(tickMicros, 1, steady, 0, ticks - 1);
            Array.Sort(steady);
            double median = steady.Length % 2 == 1
                ? steady[steady.Length / 2]
                : 0.5 * (steady[steady.Length / 2 - 1] + steady[steady.Length / 2]);

            TestContext.Out.WriteLine(
                $"TICKMS blocks={totalBlocks} rebuildTick0={tickMicros[0]:F2} " +
                $"min={steady[0]:F2} median={median:F2} max={steady[steady.Length - 1]:F2} " +
                $"(steady-state ticks 1..{ticks - 1}, ms)");

            TestContext.Out.WriteLine(
                $"SUMMARY blocks={totalBlocks} ticks={ticks} totalMs={sw.ElapsedMilliseconds} " +
                $"avgMsPerTick={sw.ElapsedMilliseconds / (double)ticks:F3} " +
                $"maxIterationsPerTick={maxIterations} maxAllocBytesPerTick={maxTickAlloc} " +
                $"criticalLoadFactor={solver.CriticalLoadFactor}");

            // Sanity, not a performance requirement: MEASURES iterations
            // rather than demanding convergence; only rules out NaN/Infinity.
            Assert.IsTrue(float.IsFinite(solver.ResidualNorm), "residual is NaN/Infinity: likely a projection or assembly bug, not just slow convergence");
            Assert.LessOrEqual(maxTickAlloc, AllocBudgetBytesPerTick,
                "steady-state Tick allocated far more than the boxed-enumerator floor; see class doc");
        }

        [Test]
        public void Benchmark_100Blocks()
        {
            RunBenchmark(100);
        }

        [Test]
        public void Benchmark_500Blocks()
        {
            RunBenchmark(500);
        }

        // Excluded from the default run_tests.sh run (multiple seconds);
        // run explicitly with --filter "TestCategory=Slow".
        [Test]
        [Category("Slow")]
        public void Benchmark_2000Blocks()
        {
            RunBenchmark(2000);
        }
    }
}
