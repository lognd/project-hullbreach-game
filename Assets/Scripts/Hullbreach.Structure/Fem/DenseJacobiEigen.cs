using System;

namespace Hullbreach.Structure
{
    // Cyclic Jacobi eigen-decomposition, shared by BucklingAnalysis and
    // CoarsePreconditioner per NO-DUPLICATION. Deterministic sweep order.
    internal static class DenseJacobiEigen
    {
        // Zeroes off-diagonal pairs by plane rotation until `a` (read via
        // a scratch copy) is diagonal to `tolerance` or sweeps run out.
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
