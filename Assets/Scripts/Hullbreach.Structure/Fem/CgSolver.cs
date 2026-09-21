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
        /// Standard PCG with M = diag(K). Warm-starts from the `u` passed in.
        /// Because K is singular, the residual (and the initial load) are
        /// re-projected onto the complement of the rigid-body modes every
        /// iteration, so rounding cannot slowly excite them: Gram-Schmidt
        /// every iteration is affordable at this problem size.
        /// </summary>
        public void Solve(StiffnessAssembly k, float[] f, float[] u, float[][] rigidModes)
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

            k.Multiply(u, kp);
            for (int i = 0; i < n; i++) r[i] = f[i] - kp[i];
            Project(r, modes);

            for (int i = 0; i < n; i++)
                z[i] = diag[i] > 1e-12f ? r[i] / diag[i] : r[i];
            Array.Copy(z, p, n);

            float rzOld = Dot(r, z);
            float fNorm = (float)Math.Sqrt(Dot(f, f));
            float tolAbs = Tolerance * Math.Max(1f, fNorm);

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
            float bestRNorm = float.MaxValue;
            int lastImprovedIter = 0;

            int iter = 0;
            for (; iter < MaxIterations; iter++)
            {
                float rNorm = (float)Math.Sqrt(Dot(r, r));
                if (rNorm <= tolAbs) break;
                if (rNorm < bestRNorm * 0.999f) { bestRNorm = rNorm; lastImprovedIter = iter; }
                else if (iter - lastImprovedIter > stagnationPatience) break;

                k.Multiply(p, kp);
                float pkp = Dot(p, kp);
                if (Math.Abs(pkp) < 1e-20f) break;

                float alpha = rzOld / pkp;
                for (int i = 0; i < n; i++) u[i] += alpha * p[i];
                for (int i = 0; i < n; i++) r[i] -= alpha * kp[i];
                Project(r, modes);

                float rNormAfter = (float)Math.Sqrt(Dot(r, r));
                if (rNormAfter <= tolAbs) { iter++; break; }

                for (int i = 0; i < n; i++)
                    z[i] = diag[i] > 1e-12f ? r[i] / diag[i] : r[i];

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
            LastResidualNorm = (float)Math.Sqrt(Dot(r, r));
            Converged = LastResidualNorm <= tolAbs;
        }
    }
}
