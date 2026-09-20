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
    /// grows like the ship's width in elements. The intuition is exact -- each
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
        public int MaxIterations = 200;
        public float Tolerance = 1e-5f;

        /// <summary>Iterations the last Solve actually took. Watch this grow
        /// with ship size -- it is the scaling wall, made visible.</summary>
        public int LastIterationCount { get; private set; }

        /// <summary>
        /// Orthonormalizes `modes` in place via Gram-Schmidt, so they can be
        /// used to repeatedly project a vector onto their complement.
        /// </summary>
        static void Orthonormalize(float[][] modes)
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

        static float Dot(float[] a, float[] b)
        {
            float s = 0f;
            for (int i = 0; i < a.Length; i++) s += a[i] * b[i];
            return s;
        }

        /// <summary>Removes the component of `v` along each of the (assumed
        /// orthonormal) `modes`, in place.</summary>
        static void Project(float[] v, float[][] modes)
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
        /// iteration, so rounding cannot slowly excite them -- Gram-Schmidt
        /// every iteration is affordable at this problem size.
        /// </summary>
        public void Solve(StiffnessAssembly k, float[] f, float[] u, float[][] rigidModes)
        {
            int n = k.DofCount;

            // Work on copies of the modes so we can orthonormalize without
            // mutating the caller's arrays.
            var modes = new float[rigidModes.Length][];
            for (int i = 0; i < rigidModes.Length; i++)
                modes[i] = (float[])rigidModes[i].Clone();
            Orthonormalize(modes);

            var diag = new float[n];
            k.Diagonal(diag);

            var r = new float[n];
            var kp = new float[n];
            var z = new float[n];
            var p = new float[n];

            k.Multiply(u, kp);
            for (int i = 0; i < n; i++) r[i] = f[i] - kp[i];
            Project(r, modes);

            for (int i = 0; i < n; i++)
                z[i] = diag[i] > 1e-12f ? r[i] / diag[i] : r[i];
            Array.Copy(z, p, n);

            float rzOld = Dot(r, z);
            float fNorm = (float)Math.Sqrt(Dot(f, f));
            float tolAbs = Tolerance * Math.Max(1f, fNorm);

            int iter = 0;
            for (; iter < MaxIterations; iter++)
            {
                float rNorm = (float)Math.Sqrt(Dot(r, r));
                if (rNorm <= tolAbs) break;

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
                float beta = rzNew / rzOld;
                for (int i = 0; i < n; i++) p[i] = z[i] + beta * p[i];
                rzOld = rzNew;
            }

            LastIterationCount = iter;
        }
    }
}
