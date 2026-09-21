using System;

namespace Hullbreach.Structure
{
    /// <summary>
    /// Cyclic Jacobi eigen-decomposition of a small dense symmetric matrix,
    /// shared by BucklingAnalysis's subspace Rayleigh-Ritz solve and
    /// CoarsePreconditioner's coarse-operator pseudo-inverse (NO-DUPLICATION:
    /// both previously would have carried their own copy of the same ~40
    /// lines). Deterministic (fixed sweep order, no early-exit dependent on
    /// anything but the matrix itself).
    /// </summary>
    internal static class DenseJacobiEigen
    {
        /// <summary>
        /// Repeatedly zeroes the largest-magnitude off-diagonal pair with a
        /// plane rotation until `a` (read-only; a scratch copy is rotated
        /// internally) is diagonal to `tolerance`, or `maxSweeps` cyclic
        /// sweeps have run. `eigenvalues` and `eigenvectors` must already be
        /// sized `n` / `n x n`; `eigenvectors` columns are the eigenvectors,
        /// matching `eigenvalues` by index (not sorted).
        /// </summary>
        public static void Solve(float[,] a, int n, float[] eigenvalues, float[,] eigenvectors, int maxSweeps = 100, float tolerance = 1e-9f)
        {
            var m = (float[,])a.Clone();
            for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                eigenvectors[i, j] = i == j ? 1f : 0f;

            for (int sweep = 0; sweep < maxSweeps; sweep++)
            {
                float off = 0f;
                for (int p = 0; p < n; p++)
                for (int q = p + 1; q < n; q++)
                    off += m[p, q] * m[p, q];
                if (off < tolerance) break;

                for (int p = 0; p < n; p++)
                for (int q = p + 1; q < n; q++)
                {
                    if (Math.Abs(m[p, q]) < 1e-12f) continue;

                    float theta = (m[q, q] - m[p, p]) / (2f * m[p, q]);
                    float t = Math.Sign(theta) / (Math.Abs(theta) + (float)Math.Sqrt(theta * theta + 1f));
                    if (theta == 0f) t = 1f;
                    float c = 1f / (float)Math.Sqrt(t * t + 1f);
                    float s = t * c;

                    float mpp = m[p, p], mqq = m[q, q], mpq = m[p, q];
                    m[p, p] = c * c * mpp - 2f * s * c * mpq + s * s * mqq;
                    m[q, q] = s * s * mpp + 2f * s * c * mpq + c * c * mqq;
                    m[p, q] = 0f;
                    m[q, p] = 0f;

                    for (int i = 0; i < n; i++)
                    {
                        if (i == p || i == q) continue;
                        float mip = m[i, p], miq = m[i, q];
                        m[i, p] = c * mip - s * miq;
                        m[p, i] = m[i, p];
                        m[i, q] = s * mip + c * miq;
                        m[q, i] = m[i, q];
                    }

                    for (int i = 0; i < n; i++)
                    {
                        float vip = eigenvectors[i, p], viq = eigenvectors[i, q];
                        eigenvectors[i, p] = c * vip - s * viq;
                        eigenvectors[i, q] = s * vip + c * viq;
                    }
                }
            }

            for (int i = 0; i < n; i++) eigenvalues[i] = m[i, i];
        }
    }
}
