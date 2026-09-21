using System;

namespace Hullbreach.Structure
{
    /// <summary>
    /// Caller-owned Krylov state that lets <see cref="CgSolver.Solve"/>
    /// CONTINUE one conjugate-gradient run across several ticks instead of
    /// restarting it every tick.
    ///
    /// WHY THIS EXISTS: CgSolver warm-starts the displacement `u`, but `u`
    /// alone is not what makes CG fast. CG's superlinear phase comes from
    /// the Krylov subspace accumulated in the search direction `p` (and the conjugacy
    /// bookkeeping in `rz`), and rebuilding `p` from the preconditioned
    /// residual at the top of every Solve throws that away:
    /// the first iteration of every tick is a blind steepest-descent step,
    /// and a ship too wide to converge inside MaxCgIterationsPerTick never
    /// compounds its per-tick budget. Its residual oscillates around a
    /// plateau across ticks instead of trending to zero, which is exactly
    /// what the 500- and 2000-block SolverBenchmarks cases did.
    ///
    /// WHEN A CONTINUATION IS VALID, and why this is the dangerous part: a
    /// continued CG is a single CG run on ONE fixed linear system. Its
    /// conjugacy relations (p_i^T K p_j = 0) are statements about a
    /// particular K, a particular preconditioner M^-1 and a particular
    /// right-hand side f. Change any of the three between ticks and the
    /// stored `r`/`p`/`rz` describe a system that no longer exists: the
    /// iteration does not merely converge slower, it minimizes the wrong
    /// quadratic and can diverge. So:
    ///   - f: checked numerically by Solve on every call
    ///     (|f_new - f_old| / |f_new| &lt; <see cref="LoadChangeTolerance"/>).
    ///   - K and M^-1: NOT detectable from inside Solve, so the owner must
    ///     call <see cref="Invalidate"/> on any topology rebuild, damage
    ///     rescale, or preconditioner change. StructuralSolver does this in
    ///     the same branch that rebuilds the assembly and the coarse
    ///     operator; a new caller that forgets is the one way to misuse
    ///     this class.
    ///
    /// WHAT IS CARRIED: the residual `r`, the search direction `p` and
    /// r.z, i.e. everything one CG iteration needs from the previous one,
    /// and all three together or none. Two cheaper-looking variants were
    /// tried and are both WRONG. Keeping `p` alone and recomputing
    /// r = f - K u each tick breaks the alpha/beta relations outright
    /// (`p` is conjugate to the residual history that produced it): the
    /// 100-block benchmark diverged to 1e14 within two ticks. Gating the
    /// continuation on agreement between the carried recursive residual
    /// and the true one is also wrong, though for a subtler reason: at
    /// this conditioning in float32 the two disagree by 5% of |f| even
    /// WITHIN a single tick's solve (measured on the 100-block case,
    /// recursive 5e-4 against a true 3.4 at |f| = 58), which is PCG's
    /// attainable-accuracy floor rather than anything continuation did,
    /// so such a gate simply refuses every continuation. See
    /// docs/roadmap.md's Performance section.
    ///
    /// HOW A BAD CONTINUATION IS CAUGHT: every tick that improves on the
    /// best residual since the restart snapshots `u` with it, and a
    /// continuation that then breaks down, blows up
    /// (<see cref="ResidualGrowthSlack"/>) or simply stops improving
    /// (<see cref="StagnationTicks"/>) is rolled back to that snapshot and
    /// suspended for <see cref="SuspensionTicks"/> ticks. So a ship that
    /// should not be continuing (the 500- and 2000-block benchmark ships,
    /// which do not converge inside their budget at all) pays one tick and
    /// then behaves exactly like the old restart-every-tick solver, and
    /// never publishes a displacement worse than that solver's.
    ///
    /// ALLOCATION: buffers are sized once per dof count and reused, so a
    /// steady-state tick allocates nothing here.
    /// </summary>
    public sealed class CgState
    {
        /// <summary>Relative change in the load vector above which Solve
        /// restarts instead of continuing: |f_new - f_old| / |f_new|. 1e-6
        /// is "the same load, re-derived in float" (inertia relief and the
        /// point-force scatter re-run every tick and are not bit-stable),
        /// not "a load that drifted a little": a genuinely drifting load is
        /// a different linear system and gets an honest restart.</summary>
        public float LoadChangeTolerance = 1e-6f;

        /// <summary>Consecutive ticks a continued run may fail to improve
        /// on its best residual before it is rolled back to that best and
        /// restarted. The primary health check, because it is the one
        /// thing that actually separates a ship converging slowly (bounces
        /// upward, but the best keeps falling) from a ship not converging
        /// at all (the best stops moving while the current creeps up).
        /// 3 is two bounces' worth of patience.</summary>
        public int StagnationTicks = 3;

        /// <summary>Emergency bound on a single tick, as a multiple of the
        /// best residual since the restart: a continuation that blows up
        /// this far inside one tick does not get three more ticks to prove
        /// itself. 100x is far outside anything a healthy run does and far
        /// inside the ~1000x an unguarded diverging run reached.</summary>
        public float ResidualGrowthSlack = 100f;

        /// <summary>Restart-only ticks to serve after a rolled-back
        /// continuation before trying again. Not permanent, because a ship
        /// can be temporarily hard (a transient load spike) without being
        /// permanently unsolvable; not zero, because retrying every tick
        /// on a ship that genuinely cannot converge inside its budget
        /// would throw away half of every second tick.</summary>
        public int SuspensionTicks = 8;

        /// <summary>The same emergency bound as
        /// <see cref="ResidualGrowthSlack"/>, but checked once per
        /// ITERATION rather than once per tick, so a continuation that has
        /// genuinely gone bad stops immediately instead of spending the
        /// rest of the budget making `u` worse. Looser than the per-tick
        /// bound because a single iteration's residual is far noisier than
        /// a whole tick's.</summary>
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

        /// <summary>True when the most recent Solve continued the previous
        /// tick's Krylov subspace rather than restarting it.</summary>
        public bool ContinuedFromLastTick { get; internal set; }

        /// <summary>Ticks (Solve calls) since the last restart, 0 on the
        /// tick that restarted. Watch this climb on a ship under a steady
        /// load and drop to 0 the moment the load or the topology
        /// changes.</summary>
        public int TicksSinceRestart { get; internal set; }

        /// <summary>Drops the stored Krylov subspace, so the next Solve
        /// rebuilds `r`/`p` from the current `u` and `f`. Call whenever K
        /// or the preconditioner changed (see the class doc): nothing else
        /// can detect that.</summary>
        public void Invalidate()
        {
            Primed = false;
            ContinuedFromLastTick = false;
            TicksSinceRestart = 0;
            BestResidual = float.MaxValue;
            TicksSinceImprovement = 0;
        }

        /// <summary>(Re)sizes the stored vectors for `n` dofs, dropping any
        /// continuation when the problem size itself changed.</summary>
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

        /// <summary>True when `f` matches the load the stored subspace was
        /// built against to within <see cref="LoadChangeTolerance"/>,
        /// relative to |f|. Allocation-free: one fused pass accumulating
        /// both |f - f_prev|^2 and |f|^2.</summary>
        internal bool LoadUnchanged(float[] f)
        {
            double diff = 0.0, norm = 0.0;
            for (int i = 0; i < f.Length; i++)
            {
                double d = f[i] - PrevF[i];
                diff += d * d;
                norm += (double)f[i] * f[i];
            }

            // |f| == 0 is a degenerate but legal load (a ship with nothing
            // applied): the residual is zero too, so any stored subspace is
            // as good as a restart. Treat an exactly-zero previous diff as
            // unchanged and anything else as changed.
            if (norm <= 0.0) return diff <= 0.0;
            return Math.Sqrt(diff / norm) < LoadChangeTolerance;
        }
    }
}
