using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Structure;

namespace Hullbreach.Structure.Tests
{
    /// <summary>
    /// Benchmarks-as-tests: build a few ship sizes, run real ticks under a
    /// thruster load, and report the numbers that matter for real-time
    /// (DOF count, CG iterations/tick, ms/tick, buckling sweeps) to
    /// TestContext rather than a separate profiling harness, so they run
    /// on every `run_tests.sh` invocation (except the 2000-block case, see
    /// below) and rot the moment someone changes the hot path without
    /// re-measuring.
    ///
    /// WHY A PLATE + A LONG ARM: a compact blob's CG iteration count barely
    /// grows with block count (see CgSolver's doc: iterations scale with
    /// ship WIDTH, i.e. the longest path information has to cross, not
    /// area). A wide plate alone would hide that. Appending a long, 1-wide
    /// arm to a plate of the same total block count makes the width-scaling
    /// wall visible without needing a separate no-plate control case.
    ///
    /// ALLOCATION: assertions here are informational about the STEADY-STATE
    /// hot path (ticks 2..10, after the one-time topology rebuild on tick
    /// 1 and any one-time JIT warmup), not an absolute zero: BlockGrid.All
    /// is typed as IEnumerable&lt;KeyValuePair&lt;int,Block&gt;&gt;, and
    /// BlockGrid lives in Hullbreach.Core, out of this ticket's scope, so
    /// the interface-typed foreach here and in StiffnessAssembly/
    /// GeometricStiffness boxes a Dictionary enumerator once per grid walk.
    /// That is a small, CONSTANT number of bytes per tick (does not scale
    /// with ship size beyond the number of grid walks per tick), not
    /// unbounded growth, so the assertion below is a tight cap rather than
    /// a literal zero; see docs/roadmap.md's Performance section for
    /// removing it (a BlockGrid enumerator struct in Hullbreach.Core).
    /// </summary>
    public class SolverBenchmarks
    {
        /// <summary>Per-tick allocation ceiling for the steady-state hot
        /// path, generous relative to the handful of boxed-enumerator
        /// allocations described in the class doc (each well under 100
        /// bytes) so this catches a REGRESSION (e.g. a reintroduced
        /// per-tick `new float[]`) without being sensitive to exactly how
        /// many grid walks a given tick happens to do.</summary>
        const long AllocBudgetBytesPerTick = 4096;

        /// <summary>
        /// Builds a ship with a roughly square plate (most of `totalBlocks`,
        /// so width scaling is visible without an impractically long arm
        /// given BlockKey's +-128 coordinate range) plus a 1-wide arm split
        /// to either side of the plate along x, so the ship is both wide
        /// (many CG iterations) and has a slender member (the eventual
        /// buckling target). The core sits at the plate's center.
        /// </summary>
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

            // Arm extends the plate along +x and -x at y = coreY, a 1-wide
            // slender member whose length is most of the "width" CG has to
            // cross information over.
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

            // In the real game loop, the ship branch clears TopologyDirty
            // once other systems have reacted to a build-time change (see
            // StructuralSolver's class doc on why StructuralSolver itself
            // never does this). A test harness that skips this step forces
            // StructuralSolver to treat EVERY tick as a topology change and
            // fully re-assemble K from scratch, which would make this
            // benchmark measure assembly cost instead of the steady-state
            // CG/preconditioner cost it is meant to report.
            grid.ClearDirty();

            var solver = new StructuralSolver();
            solver.BucklingEnabled = true;
            solver.BucklingEveryNTicks = 4;

            // Production default (see StructuralSolver.MaxCgIterationsPerTick):
            // this benchmark measures REAL per-tick behavior, including a
            // ship too wide for the current preconditioner to fully settle
            // within budget (see the SUMMARY line's maxIterationsPerTick
            // and the finite-residual assertion below, not a hard
            // convergence one, for why that is expected here).

            var forces = new List<(float2, float2)>
            {
                (thrusterPoint, new float2(0f, -50f)),
            };

            // Per-tick wall clock, kept for the min/median/max summary
            // below: an average hides exactly the thing this benchmark
            // exists to catch, a periodic spike on the buckling-eligible
            // ticks (see docs/roadmap.md's Performance section). Measured
            // in ticks of the Stopwatch, not whole milliseconds, because a
            // settled 100-block tick now costs well under one.
            var tickMicros = new double[ticks];

            var sw = System.Diagnostics.Stopwatch.StartNew();
            long allocBefore = 0, allocAfter = 0;
            long maxTickAlloc = 0;
            int maxIterations = 0;

            for (int tick = 0; tick < ticks; tick++)
            {
                // Skip not just the one-time K/topology rebuild (tick 0) but
                // also the tick BucklingAnalysis.Reset first fires (the
                // first time RunBuckling ever sees a DOF-count change, at
                // tick BucklingEveryNTicks): that Reset allocates its whole
                // subspace block once, same category of one-time cost as
                // rebuilding K, not a steady-state per-tick allocation.
                // Measuring only the LAST couple of ticks guarantees both
                // one-time costs are already behind us.
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
            // one-time K/coarse rebuild, which is reported separately
            // because it is a different question (see the roadmap's
            // rebuild-cost numbers) from what a tick costs in flight.
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

            // Sanity, not a performance requirement: the whole point of this
            // benchmark existing is to MEASURE how many iterations the
            // current preconditioner needs (see the SUMMARY line), which
            // for a slender arm under plain Jacobi PCG can legitimately
            // exceed a generous cap (this is the scaling wall CgSolver's
            // doc describes). A finite residual rules out the failure mode
            // that actually indicates a bug (NaN/Infinity from a singular
            // or mis-projected system); demanding full convergence here
            // would make this test track the preconditioner's quality
            // instead of reporting it.
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

        /// <summary>Excluded from the default `run_tests.sh` run (see
        /// tools/plaincs/run_tests.sh's filter) because 2000 blocks x 10
        /// ticks x the CG cost of a wide ship is multiple seconds; run it
        /// explicitly with:
        ///   tools/plaincs/run_tests.sh --filter "TestCategory=Slow"
        /// </summary>
        [Test]
        [Category("Slow")]
        public void Benchmark_2000Blocks()
        {
            RunBenchmark(2000);
        }
    }
}
