using System;

namespace Hullbreach.Structure
{
    // Jacobi-preconditioned conjugate gradient, warm-started; see
    // docs/reference/hullbreach-structure.md#cgsolver for the analysis.
    // frob:doc docs/reference/hullbreach-structure.md#cgsolver
    public sealed class CgSolver
    {
        // 4000 is cheap per call and CG still exits at Tolerance; see the
        // reference page for the measurement behind this cap.
        // frob:doc docs/reference/hullbreach-structure.md#cgsolver
        public int MaxIterations = 4000;

        // RELATIVE to |f|: exits once |r| <= Tolerance * |f|; see the
        // reference page for why an absolute floor broke buckling.
        // frob:doc docs/reference/hullbreach-structure.md#cgsolver
        public float Tolerance = 1e-5f;

        // Watch this grow with ship size: it is the scaling wall, made visible.
        // frob:doc docs/reference/hullbreach-structure.md#cgsolver
        public int LastIterationCount { get; private set; }

        // StructuralSolver uses this to decide whether a tick's partial
        // solve is trustworthy enough to run buckling against.
        // frob:doc docs/reference/hullbreach-structure.md#cgsolver
        public float LastResidualNorm { get; private set; }

        // False means the returned `u` is a partial, still-improving warm
        // start, not a converged displacement field.
        // frob:doc docs/reference/hullbreach-structure.md#cgsolver
        public bool Converged { get; private set; }

        // frob:doc docs/reference/hullbreach-structure.md#cgsolver
        public bool ContinuedFromLastTick { get; private set; }

        // Scratch buffers reused across Solve calls to avoid per-tick GC
        // pressure; resized only when n or the mode count changes.
        int _scratchN = -1;
        int _scratchModeCount = -1;
        float[] _diag = Array.Empty<float>();
        float[] _r = Array.Empty<float>();
        float[] _kp = Array.Empty<float>();
        float[] _z = Array.Empty<float>();
        float[] _p = Array.Empty<float>();
        float[][] _scratchModes = Array.Empty<float[]>();

        void EnsureScratch(int n, int modeCount)
        {
            if (_scratchN == n && _scratchModeCount == modeCount) return;

            _diag = new float[n];
            _r = new float[n];
            _kp = new float[n];
            _z = new float[n];
            _p = new float[n];

            _scratchModes = new float[modeCount][];
            for (int i = 0; i < modeCount; i++) _scratchModes[i] = new float[n];

            _scratchN = n;
            _scratchModeCount = modeCount;
        }

        // Orthonormalizes `modes` via Gram-Schmidt; internal so
        // BucklingAnalysis can reuse it (NO-DUPLICATION).
        internal static void Orthonormalize(float[][] modes)
        {
            for (int i = 0; i < modes.Length; i++)
            {
                var mi = modes[i];
                for (int j = 0; j < i; j++)
                {
                    var mj = modes[j];
                    float dot = Dot(mi, mj);
                    for (int k = 0; k < mi.Length; k++)
                        mi[k] -= dot * mj[k];
                }

                float norm = (float)Math.Sqrt(Dot(mi, mi));
                if (norm > 1e-8f)
                {
                    for (int k = 0; k < mi.Length; k++)
                        mi[k] /= norm;
                }
            }
        }

        // Shared with BucklingAnalysis's Rayleigh-Ritz projection.
        internal static float Dot(float[] a, float[] b)
        {
            float s = 0f;
            for (int i = 0; i < a.Length; i++) s += a[i] * b[i];
            return s;
        }

        // Removes the component of `v` along each orthonormal mode.
        // Shared with BucklingAnalysis for the same reason CG needs it.
        internal static void Project(float[] v, float[][] modes)
        {
            foreach (var m in modes)
            {
                float dot = Dot(v, m);
                for (int i = 0; i < v.Length; i++)
                    v[i] -= dot * m[i];
            }
        }

        // Standard PCG with M = diag(K), optionally deflated via `coarse`.
        // Pass a caller-owned CgState to continue a prior run (read its doc first).
        // frob:doc docs/reference/hullbreach-structure.md#cgsolver
        public void Solve(StiffnessAssembly k, float[] f, float[] u, float[][] rigidModes, CoarsePreconditioner coarse = null, CgState state = null)
        {
            int n = k.DofCount;
            EnsureScratch(n, rigidModes.Length);

            // Copy (never alias) the caller's modes into reused scratch,
            // so orthonormalizing does not mutate rigidModes.
            var modes = _scratchModes;
            for (int i = 0; i < rigidModes.Length; i++)
                Array.Copy(rigidModes[i], modes[i], n);
            Orthonormalize(modes);

            var diag = _diag;
            k.Diagonal(diag);

            var r = _r;
            var kp = _kp;
            var z = _z;
            var p = _p;

            // KRYLOV CONTINUATION (see CgState): pick the iteration up
            // where it stopped instead of rebuilding the search direction.
            bool continued = false;
            if (state != null)
            {
                state.Ensure(n);
                continued = state.Primed && state.SuspendedTicks == 0 && state.LoadUnchanged(f);
            }

            float fNorm = (float)Math.Sqrt(Dot(f, f));
            float tolAbs = Tolerance * fNorm;

            float rzOld;
            if (continued)
            {
                // Exactly where it stopped: r, p and r.z all belong to one
                // CG run on one unchanged system.
                Array.Copy(state.R, r, n);
                Array.Copy(state.P, p, n);
                rzOld = state.Rz;
                state.TicksSinceRestart++;
            }
            else
            {
                k.Multiply(u, kp);
                for (int i = 0; i < n; i++) r[i] = f[i] - kp[i];
                Project(r, modes);

                ApplyPreconditioner(diag, r, z, n, coarse, modes);
                Array.Copy(z, p, n);

                rzOld = Dot(r, z);
                if (state != null)
                {
                    state.TicksSinceRestart = 0;
                    state.BestResidual = float.MaxValue;
                    state.TicksSinceImprovement = 0;
                    if (state.SuspendedTicks > 0) state.SuspendedTicks--;
                }
            }

            ContinuedFromLastTick = continued;
            if (state != null) state.ContinuedFromLastTick = continued;

            // STAGNATION GUARD: a near machine-zero right-hand side cannot
            // be driven under tolAbs; see the reference page for why.
            int stagnationPatience = Math.Max(100, n);
            // A CONTINUED tick inherits the best residual since the
            // restart, so divergence is measured against the whole run.
            float bestRNorm = continued ? state.BestResidual : float.MaxValue;
            int lastImprovedIter = 0;

            // PERFORMANCE: track the residual norm across iterations
            // instead of recomputing Dot(r,r) twice per iteration.
            float rNorm = (float)Math.Sqrt(Dot(r, r));

            bool breakdown = false;
            int iter = 0;
            for (; iter < MaxIterations; iter++)
            {
                if (rNorm <= tolAbs) break;
                // Divergence guard, checked per iteration (see
                // CgState.DivergenceFactor) so a diverging run stops early.
                if (continued && rNorm > bestRNorm * state.DivergenceFactor) { breakdown = true; break; }
                if (rNorm < bestRNorm * 0.999f) { bestRNorm = rNorm; lastImprovedIter = iter; }
                else if (iter - lastImprovedIter > stagnationPatience) { breakdown = true; break; }

                k.Multiply(p, kp);
                float pkp = Dot(p, kp);
                // Breakdown: `p` carries no more curvature and must not
                // become the next tick's continuation.
                if (Math.Abs(pkp) < 1e-20f) { breakdown = true; break; }

                float alpha = rzOld / pkp;
                for (int i = 0; i < n; i++) u[i] += alpha * p[i];
                for (int i = 0; i < n; i++) r[i] -= alpha * kp[i];
                Project(r, modes);

                rNorm = (float)Math.Sqrt(Dot(r, r));
                if (rNorm <= tolAbs) { iter++; break; }

                ApplyPreconditioner(diag, r, z, n, coarse, modes);

                float rzNew = Dot(r, z);
                // Guard against an exactly-annihilated residual (rzOld ==
                // 0f): 0/0 would otherwise be NaN and corrupt p and u.
                float beta = Math.Abs(rzOld) > 1e-30f ? rzNew / rzOld : 0f;
                for (int i = 0; i < n; i++) p[i] = z[i] + beta * p[i];
                rzOld = rzNew;
            }

            LastIterationCount = iter;
            LastResidualNorm = rNorm;
            Converged = LastResidualNorm <= tolAbs;

            if (state != null)
            {
                // BEST-U SNAPSHOT AND ROLLBACK: a continuation must never
                // leave `u` worse than the best this run had; see the reference page.
                bool catastrophic = !Converged && rNorm > state.BestResidual * state.ResidualGrowthSlack;
                bool stagnant = !Converged && state.TicksSinceImprovement >= state.StagnationTicks;
                if (continued && (breakdown || catastrophic || stagnant))
                {
                    Array.Copy(state.UBest, u, n);
                    LastResidualNorm = state.BestResidual;
                    Converged = LastResidualNorm <= tolAbs;
                    state.Invalidate();
                    state.SuspendedTicks = state.SuspensionTicks;
                }
                else if (breakdown)
                {
                    state.Invalidate();
                }
                else
                {
                    Array.Copy(r, state.R, n);
                    Array.Copy(p, state.P, n);
                    Array.Copy(f, state.PrevF, n);
                    state.Rz = rzOld;
                    state.Primed = true;
                    if (rNorm < state.BestResidual)
                    {
                        state.BestResidual = rNorm;
                        Array.Copy(u, state.UBest, n);
                        state.TicksSinceImprovement = 0;
                    }
                    else
                    {
                        state.TicksSinceImprovement++;
                    }

                    // A converged restart proves the system is solvable
                    // again, so lift any suspension.
                    if (Converged) state.SuspendedTicks = 0;
                }
            }
        }

        // Applies M^-1 to `r` into `z`: plain Jacobi plus
        // CoarsePreconditioner's deflated correction when `coarse` is set.
        static void ApplyPreconditioner(float[] diag, float[] r, float[] z, int n, CoarsePreconditioner coarse, float[][] modes)
        {
            for (int i = 0; i < n; i++)
                z[i] = diag[i] > 1e-12f ? r[i] / diag[i] : r[i];
            if (coarse != null) coarse.ApplyAdditive(r, z);
        }
    }
}
