using System;

namespace Hullbreach.Structure
{
    // Caller-owned Krylov state that lets CgSolver.Solve CONTINUE a CG run
    // across ticks; see docs/reference/hullbreach-structure.md#cgstate.
    // frob:doc docs/reference/hullbreach-structure.md#cgstate
    public sealed class CgState
    {
        // 1e-6 is "the same load, re-derived in float", not "drifted a
        // little": a genuinely drifting load gets an honest restart.
        // frob:doc docs/reference/hullbreach-structure.md#cgstate
        public float LoadChangeTolerance = 1e-6f;

        // Separates a ship converging slowly from one not converging at
        // all; 3 is two bounces' worth of patience.
        // frob:doc docs/reference/hullbreach-structure.md#cgstate
        public int StagnationTicks = 3;

        // 100x is outside anything healthy, but inside the ~1000x an
        // unguarded diverging run reached.
        // frob:doc docs/reference/hullbreach-structure.md#cgstate
        public float ResidualGrowthSlack = 100f;

        // Not permanent (a transient spike is not permanently unsolvable);
        // not zero (retrying every tick wastes budget on a hard ship).
        // frob:doc docs/reference/hullbreach-structure.md#cgstate
        public int SuspensionTicks = 8;

        // Same idea as ResidualGrowthSlack but checked per ITERATION;
        // looser, since one iteration's residual is noisier.
        // frob:doc docs/reference/hullbreach-structure.md#cgstate
        public float DivergenceFactor = 1e3f;

        internal int Dof = -1;
        internal float BestResidual = float.MaxValue;
        internal int TicksSinceImprovement;
        internal float[] UBest = Array.Empty<float>();
        internal int SuspendedTicks;
        internal float[] R = Array.Empty<float>();
        internal float Rz;
        internal float[] P = Array.Empty<float>();
        internal float[] PrevF = Array.Empty<float>();
        internal bool Primed;

        // frob:doc docs/reference/hullbreach-structure.md#cgstate
        public bool ContinuedFromLastTick { get; internal set; }

        // Watch this climb under a steady load and drop to 0 when the
        // load or topology changes.
        // frob:doc docs/reference/hullbreach-structure.md#cgstate
        public int TicksSinceRestart { get; internal set; }

        // Call whenever K or the preconditioner changed: nothing else can
        // detect that.
        // frob:doc docs/reference/hullbreach-structure.md#cgstate
        public void Invalidate()
        {
            Primed = false;
            ContinuedFromLastTick = false;
            TicksSinceRestart = 0;
            BestResidual = float.MaxValue;
            TicksSinceImprovement = 0;
        }

        // Drops any continuation when the problem size itself changed.
        internal void Ensure(int n)
        {
            if (Dof == n) return;
            R = new float[n];
            P = new float[n];
            UBest = new float[n];
            PrevF = new float[n];
            Dof = n;
            Invalidate();
        }

        // Allocation-free: one fused pass accumulating both |f - f_prev|^2
        // and |f|^2.
        internal bool LoadUnchanged(float[] f)
        {
            double diff = 0.0, norm = 0.0;
            for (int i = 0; i < f.Length; i++)
            {
                double d = f[i] - PrevF[i];
                diff += d * d;
                norm += (double)f[i] * f[i];
            }

            // |f| == 0 is a legal load (residual is zero too, so any
            // stored subspace is as good as a restart).
            if (norm <= 0.0) return diff <= 0.0;
            return Math.Sqrt(diff / norm) < LoadChangeTolerance;
        }
    }
}
