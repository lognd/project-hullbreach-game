using System;

namespace Hullbreach.Structure
{
    /// <summary>
    /// Jacobi-preconditioned conjugate gradient, warm-started.
    ///
    /// CG only needs K * v, never K itself, which is why StiffnessAssembly
    /// exposes just Multiply.
    ///
    /// CONVERGENCE, and why this eventually stops scaling: iterations grow like
    /// sqrt(condition number), and for 2D elasticity kappa ~ h^-2, so the count
    /// grows like the ship's width in elements. The intuition is exact: each
    /// multiply by K propagates information exactly one element further, so
    /// telling the bow that the stern fired takes ~L iterations. Warm-starting
    /// hides this while loads change smoothly, but combat changes topology and
    /// destroys the warm start, which is when you would need it most.
    ///
    /// The fix, when you get there, is chunking: solve a coarse homogenised
    /// problem globally (few, large elements, so information crosses fast) and
    /// refine per chunk with cached factorisations. Do NOT build that yet.
    /// Keep this interface taking a region and its boundary conditions, and the
    /// flat version becomes the one-chunk case for free.
    /// </summary>
    public sealed class CgSolver
    {
        /// <summary>Iteration cap. WAS 200, which is far below what even a
        /// modest ship needs: a 1-wide, 32-block column (326 dof) measured
        /// at ~650 iterations to hit Tolerance, so the old cap silently
        /// returned an under-converged (force-imbalanced) displacement field
        /// for anything longer than a stub. That fed wrong element stresses
        /// into GeometricStiffness and biased BucklingAnalysis's Rayleigh
        /// quotients enough to break the Euler P_cr ~ 1/L^2 trend (it read
        /// closer to 1/L, and non-monotonically at that; see
        /// BucklingTests). 4000 is cheap per call (a plain sparse mat-vec)
        /// and CG still exits the moment Tolerance is met, so this only
        /// matters for the cases that actually needed more room.</summary>
        public int MaxIterations = 4000;

        /// <summary>Stopping criterion, RELATIVE to |f|: Solve exits once
        /// |r| &lt;= Tolerance * |f|. It used to be
        /// Tolerance * max(1, |f|), which is the same thing only for ships
        /// loaded past |f| = 1 and an ABSOLUTE bound of 1e-5 below that.
        /// Every buckling test drives a self-equilibrated end load with
        /// |f| on the order of 0.04, so that floor let CG stop at ~2.5e-4
        /// relative residual while still reporting Converged, and the
        /// displacement error left at that point is preconditioner-
        /// dependent: with CoarsePreconditioner attached it landed
        /// differently than under plain Jacobi, fed a different element
        /// stress field into GeometricStiffness, and moved a 12-block
        /// column's critical load factor to 0.008 against the dense
        /// oracle's 0.149. A relative criterion means Tolerance means the
        /// same thing at every load scale, which is what the callers that
        /// reason about it (StructuralSolver.Converged gating buckling,
        /// BucklingAnalysis's own inverse iteration) already assume.
        /// |f| = 0 is still handled: the initial residual is then 0 too,
        /// so the first check passes immediately.</summary>
        public float Tolerance = 1e-5f;

        /// <summary>Iterations the last Solve actually took. Watch this grow
        /// with ship size: it is the scaling wall, made visible.</summary>
        public int LastIterationCount { get; private set; }

        /// <summary>Euclidean norm of the (rigid-mode-projected) residual
        /// after the last Solve returned, whether or not it met Tolerance:
        /// StructuralSolver uses this to decide whether a tick's partial
        /// solve is trustworthy enough to run buckling against (see
        /// StructuralSolver.MaxCgIterationsPerTick).</summary>
        public float LastResidualNorm { get; private set; }

        /// <summary>True when the last Solve's LastResidualNorm actually met
        /// Tolerance (relative to |f|, see Solve's tolAbs) before hitting
        /// MaxIterations; false means the returned `u` is a partial,
        /// still-improving warm start, not a converged displacement
        /// field.</summary>
        public bool Converged { get; private set; }

        /// <summary>True when the last Solve continued the caller-owned
        /// CgState's Krylov subspace instead of restarting from `u`; false
        /// when no state was passed, the load changed, or the state had
        /// been invalidated (see CgState).</summary>
        public bool ContinuedFromLastTick { get; private set; }

        // Scratch buffers reused across Solve calls: this is warm-started
        // and called every tick (see StructuralSolver.Tick), so allocating
        // n-sized arrays here every call was real per-tick GC pressure with
        // nothing to show for it (n only changes when topology does; see
        // SolverBenchmarks' zero-allocation assertion). Resized only when n
        // or the mode count changes.
        int _scratchN = -1;
        int _scratchModeCount = -1;
        float[] _diag = Array.Empty<float>();
        float[] _r = Array.Empty<float>();
        float[] _kp = Array.Empty<float>();
        float[] _z = Array.Empty<float>();
        float[] _p = Array.Empty<float>();
        float[][] _scratchModes = Array.Empty<float[]>();

        /// <summary>(Re)sizes every scratch buffer for `n` dofs and
        /// `modeCount` rigid modes, only when either actually changed.</summary>
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

        /// <summary>
        /// Orthonormalizes `modes` in place via Gram-Schmidt, so they can be
        /// used to repeatedly project a vector onto their complement.
        /// </summary>
        /// <summary>Internal (not private) so BucklingAnalysis can reuse the same
        /// Gram-Schmidt routine for its subspace block, per NO-DUPLICATION.</summary>
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

        /// <summary>Euclidean dot product, shared with BucklingAnalysis's
        /// Rayleigh-Ritz projection.</summary>
        internal static float Dot(float[] a, float[] b)
        {
            float s = 0f;
            for (int i = 0; i < a.Length; i++) s += a[i] * b[i];
            return s;
        }

        /// <summary>Removes the component of `v` along each of the (assumed
        /// orthonormal) `modes`, in place.</summary>
        /// <summary>Shared with BucklingAnalysis: every subspace vector and
        /// right-hand side must stay off the rigid modes for the same reason
        /// CG's residual does.</summary>
        internal static void Project(float[] v, float[][] modes)
        {
            foreach (var m in modes)
            {
                float dot = Dot(v, m);
                for (int i = 0; i < v.Length; i++)
                    v[i] -= dot * m[i];
            }
        }

        /// <summary>
        /// Standard PCG with M = diag(K), optionally augmented by a
        /// deflated coarse correction (see CoarsePreconditioner) when
        /// `coarse` is non-null: M^-1 = D^-1 + P Kc^+ P^T. Warm-starts from
        /// the `u` passed in. Because K is singular, the residual (and the
        /// initial load) are re-projected onto the complement of the
        /// rigid-body modes every iteration, so rounding cannot slowly
        /// excite them: Gram-Schmidt every iteration is affordable at this
        /// problem size.
        ///
        /// KRYLOV CONTINUATION: pass a caller-owned <see cref="CgState"/>
        /// to continue the previous call's iteration (same r, p and rz)
        /// instead of restarting the subspace from `u`. Read CgState's doc
        /// before doing so: a continuation is only valid while K, the
        /// preconditioner and f all stay put, and only the last of those
        /// three is something this method can check for itself.
        /// </summary>
        public void Solve(StiffnessAssembly k, float[] f, float[] u, float[][] rigidModes, CoarsePreconditioner coarse = null, CgState state = null)
        {
            int n = k.DofCount;
            EnsureScratch(n, rigidModes.Length);

            // Copy (never alias) the caller's modes into scratch so we can
            // orthonormalize without mutating rigidModes, reusing the same
            // buffer every call instead of cloning fresh arrays.
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

            // KRYLOV CONTINUATION (see CgState): when the caller kept the
            // previous tick's r/p/rz and the load is the same system, pick
            // the iteration up where it stopped instead of rebuilding the
            // search direction from the residual, which would discard the
            // accumulated subspace and spend this tick's whole budget
            // re-earning it.
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
                // Pick the iteration up exactly where it stopped: the
                // residual, the search direction and r.z all belong to one
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

            // STAGNATION GUARD: tracks the best (smallest) residual norm seen
            // and how long ago it improved. A right-hand side that is
            // genuinely near machine-zero (e.g. BucklingAnalysis solving
            // K*y = -K_G*v for a v the current, nearly-uncompressed K_G maps
            // to machine-zero, see below) cannot be driven under tolAbs by
            // more iterations; without this, raising MaxIterations to cover
            // the large, genuinely slow-converging problems this solver also
            // sees (see MaxIterations' doc) meant every such degenerate call
            // burned the ENTIRE cap chasing round-off noise that was never
            // going to shrink; measured turning a small blob's buckling
            // test from a sub-second run into tens of seconds. StagnationPatience
            // iterations without at least a 0.1% improvement means "this is
            // the best available", not "not done yet".
            int stagnationPatience = Math.Max(100, n);
            // A CONTINUED tick inherits the best residual seen since the
            // restart, so the divergence guard below measures growth against
            // the whole continued run rather than against this tick's first
            // iteration.
            float bestRNorm = continued ? state.BestResidual : float.MaxValue;
            int lastImprovedIter = 0;

            // PERFORMANCE: the residual norm used to be recomputed twice per
            // iteration (once at the top of the loop for the convergence/
            // stagnation check, once again right after updating r for the
            // early-exit below), a full O(n) Dot(r,r) doubled for no reason:
            // both checks want the SAME quantity, "the residual norm right
            // now". Tracking it in one variable across iterations (seeded
            // once before the loop) halves that particular cost; measured on
            // a 5642-dof case, this and the other per-iteration cost audit
            // in CoarsePreconditioner's commit brought full-solve iteration
            // cost down from ~327us to within noise of matvec-only cost.
            float rNorm = (float)Math.Sqrt(Dot(r, r));

            bool breakdown = false;
            int iter = 0;
            for (; iter < MaxIterations; iter++)
            {
                if (rNorm <= tolAbs) break;
                // Divergence guard, checked per iteration and not per tick
                // (see CgState.DivergenceFactor): a continued run that has
                // started to diverge would otherwise burn its entire
                // remaining budget making the displacement worse before the
                // end-of-tick check noticed.
                if (continued && rNorm > bestRNorm * state.DivergenceFactor) { breakdown = true; break; }
                if (rNorm < bestRNorm * 0.999f) { bestRNorm = rNorm; lastImprovedIter = iter; }
                else if (iter - lastImprovedIter > stagnationPatience) { breakdown = true; break; }

                k.Multiply(p, kp);
                float pkp = Dot(p, kp);
                // Breakdown: the search direction carries no more curvature.
                // Whatever is in `p` is numerically exhausted, so it must not
                // become the next tick's continuation (see CgState's doc on
                // the one failure mode Solve can detect from inside).
                if (Math.Abs(pkp) < 1e-20f) { breakdown = true; break; }

                float alpha = rzOld / pkp;
                for (int i = 0; i < n; i++) u[i] += alpha * p[i];
                for (int i = 0; i < n; i++) r[i] -= alpha * kp[i];
                Project(r, modes);

                rNorm = (float)Math.Sqrt(Dot(r, r));
                if (rNorm <= tolAbs) { iter++; break; }

                ApplyPreconditioner(diag, r, z, n, coarse, modes);

                float rzNew = Dot(r, z);
                // Guard against an exactly-annihilated residual (rzOld == 0
                // without rNorm having already tripped the convergence check
                // above, possible when the right-hand side itself is
                // exactly zero, e.g. BucklingAnalysis solving K*y = -K_G*v
                // for a v the current, nearly-uncompressed K_G maps to
                // machine-zero). 0/0 would otherwise be NaN and corrupt
                // every later use of p and u.
                float beta = Math.Abs(rzOld) > 1e-30f ? rzNew / rzOld : 0f;
                for (int i = 0; i < n; i++) p[i] = z[i] + beta * p[i];
                rzOld = rzNew;
            }

            LastIterationCount = iter;
            LastResidualNorm = rNorm;
            Converged = LastResidualNorm <= tolAbs;

            if (state != null)
            {
                // BEST-U SNAPSHOT AND ROLLBACK. `u` is what the stress
                // field and the next tick's warm start are read from, so a
                // continuation that wanders must never leave it worse than
                // the best this run ever had. Every tick that improves on
                // the best residual since the restart snapshots `u`
                // alongside it (one n-copy, against a budget of hundreds of
                // matvecs); a continuation that then goes bad restores that
                // snapshot instead of publishing its own worse answer.
                //
                // "Goes bad" is judged by STAGNATION ACROSS TICKS, not by
                // this tick's residual alone: PCG minimizes the energy
                // norm, not |r|, so a healthy continued run under a small
                // per-tick budget bounces its residual upward for several
                // ticks at a time (the CgContinuationTests ship does
                // exactly that and still converges to the same total
                // iteration count as one unbudgeted solve). What the ships
                // that must NOT continue look like instead is a best
                // residual that stops improving at all while the current
                // one creeps upward tick after tick, which is the
                // 500-/2000-block plate+arm case: unguarded, that creep
                // reached ~1e5 from ~1e2 over ten ticks.
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

                    // A restart that converged is proof the system is
                    // solvable inside the budget again, so lift any
                    // suspension and let the next tick continue.
                    if (Converged) state.SuspendedTicks = 0;
                }
            }
        }

        /// <summary>Applies M^-1 to `r` into `z`: plain Jacobi (D^-1), plus
        /// CoarsePreconditioner's additive deflated correction when `coarse`
        /// is attached. Shared between the initial residual and every
        /// iteration's preconditioning step so the two never drift apart.
        ///
        /// NO EXTRA PROJECTION OF `z` HERE: the rigid-mode leak this used
        /// to mop up (the coarse correction is only approximately zero on
        /// K's null space when Kc^+ is built from an eigenvalue floor) is
        /// now removed at its source, inside
        /// CoarsePreconditioner.ApplyAdditive, which projects both its
        /// input and its output. Projecting `z` here as well would only
        /// re-project the JACOBI term, which the plain-Jacobi path
        /// deliberately does not do (D^-1 cannot introduce a rigid
        /// component orthonormal `modes` did not already put in `r`), and
        /// would make the two paths' M^-1 differ for no benefit.
        /// `modes` is still taken so the coarse path and the plain path
        /// share one signature.</summary>
        static void ApplyPreconditioner(float[] diag, float[] r, float[] z, int n, CoarsePreconditioner coarse, float[][] modes)
        {
            for (int i = 0; i < n; i++)
                z[i] = diag[i] > 1e-12f ? r[i] / diag[i] : r[i];
            if (coarse != null) coarse.ApplyAdditive(r, z);
        }
    }
}
