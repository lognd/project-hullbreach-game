using System;
using System.Collections.Generic;
using Hullbreach.Core;
using Hullbreach.Structure;

namespace Hullbreach.Ship
{
    // Build-phase verdict for a whole design.
    // frob:doc docs/reference/hullbreach-ship.md#shipstresspreview
    public enum StressVerdict
    {
        // Every block is under CreakRatio at full thrust and full steer.
        Safe = 0,
        // Some block is between CreakRatio and FailRatio: sound, but one hit from trouble.
        Marginal = 1,
        // Some block is over FailRatio: the design breaks under ordinary flight input.
        WillBreak = 2,
    }

    // What the build UI shows: a verdict, the weakest block, per-block risk
    // for the overlay tint, and how long the worst block would last.
    // frob:doc docs/reference/hullbreach-ship.md#shipstresspreview
    public sealed class StressPreviewResult
    {
        // frob:doc docs/reference/hullbreach-ship.md#shipstresspreview
        public StressVerdict Verdict;
        // Highest risk of any block; 1.0 is the failure line.
        // frob:doc docs/reference/hullbreach-ship.md#shipstresspreview
        public float PeakRisk;
        // BlockKey of the block with PeakRisk, or -1 for an empty grid.
        // frob:doc docs/reference/hullbreach-ship.md#shipstresspreview
        public int WeakestKey = -1;
        // Blocks with risk >= 1.
        // frob:doc docs/reference/hullbreach-ship.md#shipstresspreview
        public int OverloadedCount;
        // Seconds of full thrust the worst block survives; infinity unless WillBreak.
        // frob:doc docs/reference/hullbreach-ship.md#shipstresspreview
        public float SecondsToFail = float.PositiveInfinity;
        // Per-block risk 0..1+ (max over the load cases), keyed by BlockKey. 1.0 = at the limit.
        // frob:doc docs/reference/hullbreach-ship.md#shipstresspreview
        public readonly Dictionary<int, float> Risk = new Dictionary<int, float>();

        // HUD line for the READY button / toast; empty when Safe.
        // frob:doc docs/reference/hullbreach-ship.md#shipstresspreview
        public string Warning
        {
            get
            {
                switch (Verdict)
                {
                    case StressVerdict.WillBreak:
                        return "This design will snap under full thrust (" + OverloadedCount + " block" + (OverloadedCount == 1 ? "" : "s") + " overloaded).";
                    case StressVerdict.Marginal:
                        return "This design is close to its limit under full thrust.";
                    default:
                        return string.Empty;
                }
            }
        }
    }

    // Solves the REAL structural model once per load case (full forward
    // thrust, thrust + steer each way, full reverse) with every thruster and
    // fin already at its target, so a design can be judged before it flies.
    // Engine-free; reuse one instance across edits (it keeps its solver).
    // frob:doc docs/reference/hullbreach-ship.md#shipstresspreview
    public sealed class ShipStressPreview
    {
        // frob:doc docs/reference/hullbreach-ship.md#shipstresspreview
        public const float SolveDt = 0.02f;

        // Cold-start CG needs more than the per-tick budget; this many ticks of it.
        const int MaxSolveTicks = 12;

        // Subspace buckling publishes after a few ticks of warm iteration.
        const int BucklingTicks = 6;

        readonly StructuralSolver _solver = new StructuralSolver
        {
            LoadScale = StructuralSolver.DefaultLoadScale,
            MaterialStiffnessScale = StructuralSolver.DefaultMaterialStiffnessScale,
            MaxCgIterationsPerTick = 1500,
        };

        // Buckling is the expensive part (a subspace iteration, ~40 ms on its
        // own tick). Off by default for per-edit use; turn it on for the READY check.
        // frob:doc docs/reference/hullbreach-ship.md#shipstresspreview
        public bool IncludeBuckling;

        static readonly (float thrust, float steer)[] Cases =
        {
            (1f, 0f), (1f, 1f), (1f, -1f), (-1f, 0f),
        };

        // frob:doc docs/reference/hullbreach-ship.md#shipstresspreview
        public StressPreviewResult Evaluate(BlockGrid grid)
        {
            var result = new StressPreviewResult();
            if (grid == null || grid.Count == 0) return result;

            var tuning = FlightTuning.Default();
            // Instant ramps: the preview asks about the limit, not the spool-up.
            tuning.ThrusterRampUpScale = 1000f;
            tuning.ThrusterRampDownScale = 1000f;
            tuning.FinRampUpSeconds = 0.0001f;
            tuning.FinRampDownSeconds = 0.0001f;

            // One body for every case: throttles are instant, so each Step
            // re-targets them, and K (same topology) is assembled only once.
            var body = new ShipBody { Tuning = tuning };
            foreach (var kv in grid.All) body.Grid.TryAdd(kv.Key, kv.Value);
            body.RebuildDerivedViews();
            if (body.Grid.Mass.Total <= 0f) return result;

            _solver.MarkTopologyChanged();
            for (int c = 0; c < Cases.Length; c++)
            {
                body.Step(new ShipInput(Cases[c].thrust, Cases[c].steer, false), SolveDt);
                if (body.AppliedForcesThisStep.Count == 0) continue;

                // Buckling only on the first case (forward thrust), and only when asked.
                bool buckle = IncludeBuckling && c == 0;
                _solver.BucklingEnabled = buckle;
                _solver.BucklingEveryNTicks = 1;
                int ticks = 0;
                do
                {
                    _solver.Tick(body.Grid, body.AppliedForcesThisStep, SolveDt);
                    ticks++;
                }
                while (ticks < MaxSolveTicks && (!_solver.Converged || (buckle && ticks < BucklingTicks)));

                foreach (var kv in _solver.BlockStresses)
                {
                    float risk = DamageModel.DamageRatio(kv.Value.DuctileRatio, kv.Value.BrittleRatio);
                    if (buckle) risk = Math.Max(risk, kv.Value.BucklingRatio);
                    if (!result.Risk.TryGetValue(kv.Key, out float prior) || risk > prior) result.Risk[kv.Key] = risk;
                }
                if (buckle)
                    foreach (int key in _solver.BuckledBlocks)
                        result.Risk[key] = Math.Max(result.Risk.TryGetValue(key, out float r) ? r : 0f, 1f);
            }

            foreach (var kv in result.Risk)
            {
                if (kv.Value >= DamageModel.FailRatio) result.OverloadedCount++;
                if (kv.Value > result.PeakRisk || result.WeakestKey < 0) { result.PeakRisk = kv.Value; result.WeakestKey = kv.Key; }
            }

            result.Verdict = result.PeakRisk >= DamageModel.FailRatio ? StressVerdict.WillBreak
                : result.PeakRisk >= DamageModel.CreakRatio ? StressVerdict.Marginal
                : StressVerdict.Safe;
            if (result.Verdict == StressVerdict.WillBreak)
                result.SecondsToFail = DamageModel.SecondsToFail(result.PeakRisk, SolveDt);
            return result;
        }
    }
}
