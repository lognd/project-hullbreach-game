using System.Collections.Generic;
using NUnit.Framework;
using Hullbreach.Builder;
using Hullbreach.Core;
using Hullbreach.Ship;
using Hullbreach.Structure;

namespace Hullbreach.Net.Tests
{
    // Plain straight full thrust in an empty arena: no enemy, no steering.
    // Runs the same per-tick rule the server and ShipStructure use
    // (Solver.Tick, then DamageModel accumulate / detach, buckled blocks out).
    public class WingStressTests
    {
        const float Dt = 1f / 50f;

        sealed class Run
        {
            public float FirstLossSeconds = -1f;
            public float FirstDamageSeconds = -1f;
            public int Start, End;
        }

        static Run Fly(BlockGrid design, float seconds, float steer = 0f)
        {
            var body = new ShipBody { Tuning = FlightTuning.Default() };
            foreach (var kv in design.All) body.Grid.TryAdd(kv.Key, kv.Value);
            body.RebuildDerivedViews();
            var solver = new StructuralSolver { LoadScale = StructuralSolver.DefaultLoadScale, MaterialStiffnessScale = StructuralSolver.DefaultMaterialStiffnessScale };
            var failing = new Dictionary<int, bool>();
            var run = new Run { Start = body.Grid.Count };
            int ticks = (int)(seconds / Dt);
            for (int i = 0; i < ticks; i++)
            {
                float t = (i + 1) * Dt;
                body.Step(new ShipInput(1f, steer, false), Dt);
                solver.Tick(body.Grid, body.AppliedForcesThisStep, Dt);

                var detach = new List<int>();
                var damage = new List<int>();
                foreach (var kv in solver.BlockStresses)
                {
                    bool wasFailing = failing.TryGetValue(kv.Key, out var f) && f;
                    body.Grid.TryGet(kv.Key, out var b);
                    if (DamageModel.ShouldDetach(kv.Value.DuctileRatio, kv.Value.BrittleRatio, wasFailing, b.Damage)) detach.Add(kv.Key);
                    bool over = kv.Value.DuctileRatio >= 1f || kv.Value.BrittleRatio >= 1f;
                    failing[kv.Key] = over;
                    if (over) damage.Add(kv.Key);
                }
                foreach (int key in damage)
                {
                    if (!body.Grid.TryGet(key, out var b)) continue;
                    var s = solver.BlockStresses[key];
                    body.Grid.TrySet(key, b.WithDamage(DamageModel.Accumulate(b.Damage, DamageModel.DamageRatio(s.DuctileRatio, s.BrittleRatio), Dt)));
                    if (run.FirstDamageSeconds < 0f) run.FirstDamageSeconds = t;
                }
                detach.AddRange(solver.BuckledBlocks);
                if (detach.Count == 0) continue;
                foreach (int key in detach) { body.Grid.TryRemove(key); failing.Remove(key); }
                var stranded = new List<int>();
                Connectivity.FindDetached(body.Grid, stranded);
                foreach (int key in stranded) body.Grid.TryRemove(key);
                if (run.FirstLossSeconds < 0f) run.FirstLossSeconds = t;
                solver.MarkTopologyChanged();
                body.RebuildDerivedViews();
            }
            run.End = body.Grid.Count;
            return run;
        }

        static BlockGrid Wide()
        {
            var g = new BlockGrid();
            void P(int x, int y, byte t) => g.TryAdd(BlockKey.Pack(x, y), new Block(t));
            P(0, 0, BlockTypes.Core);
            P(0, 1, BlockTypes.Hull); P(0, -1, BlockTypes.Hull); P(0, 2, BlockTypes.Cannon);
            P(1, 1, BlockTypes.Fin); P(-1, 1, BlockTypes.Fin);
            for (int x = 1; x <= 3; x++) { P(x, 0, BlockTypes.Armor); P(-x, 0, BlockTypes.Armor); }
            for (int x = 4; x <= 6; x++) { P(x, 0, BlockTypes.Hull); P(-x, 0, BlockTypes.Hull); }
            foreach (int x in new[] { 5, 6 }) { P(x, -1, BlockTypes.Thruster); P(-x, -1, BlockTypes.Thruster); }
            return g;
        }

        [Test]
        public void BadWing_WarnsWithDamageBeforeItBreaks_AndStillBreaks()
        {
            var run = Fly(Wide(), 4f);
            TestContext.Progress.WriteLine($"wide wing: first damage {run.FirstDamageSeconds:F2}s, first loss {run.FirstLossSeconds:F2}s, {run.Start} -> {run.End} blocks");
            Assert.Greater(run.FirstDamageSeconds, 0f, "overstress must show as damage first");
            Assert.Greater(run.FirstLossSeconds, 0.8f, "the first block must not go in under a second (was 0.14 s)");
            Assert.Greater(run.FirstLossSeconds - run.FirstDamageSeconds, 0.5f, "at least half a second of visible warning");
            Assert.Less(run.End, run.Start, "an obviously bad design still fails");
        }

        [Test]
        public void EveryStarterTemplate_SurvivesSixSecondsOfFullThrust_StraightAndSteering()
        {
            foreach (var t in ShipTemplates.All)
            {
                foreach (float steer in new[] { 0f, 1f })
                {
                    Assert.IsTrue(ShipDesignCodec.TryBuildGrid(t.Blocks, out var grid, out string why), why);
                    var run = Fly(grid, 6f, steer);
                    Assert.AreEqual(run.Start, run.End, t.Id + " steer " + steer + " lost blocks");
                    Assert.Less(run.FirstDamageSeconds, 0f, t.Id + " steer " + steer + " took structural damage");
                }
            }
        }
    }
}
