using System;
using System.Collections.Generic;
using Hullbreach.Structure;

namespace Hullbreach.Structure.Tests
{
    /// <summary>
    /// TEST-ONLY dense reference for the linearized buckling eigenproblem
    /// K phi = -lambda*K_G phi. Independent of BucklingAnalysis's subspace
    /// iteration: builds full dense K and K_G by probing their sparse
    /// Multiply with unit vectors, projects both onto the orthogonal
    /// complement of the three rigid-body modes (so the reduced K is SPD),
    /// and solves the reduced generalized eigenproblem by Cholesky +
    /// cyclic Jacobi -- an O(n^3) method, fine at the few-hundred-dof sizes
    /// BucklingTests exercises. Exists so a bug in the geometric stiffness,
    /// the load case, or the subspace iteration can be told apart from one
    /// another: if this oracle and BucklingAnalysis agree, the subspace
    /// iteration is not the problem.
    ///
    /// DOUBLE PRECISION THROUGHOUT: the production classes work in float,
    /// but this oracle's own Gram-Schmidt complement basis (built off the
    /// n standard basis vectors, not a handful of well-separated seeds) is
    /// far more exposed to accumulated rounding at a few hundred dof than
    /// BucklingAnalysis's own better-conditioned subspace iteration --
    /// float arithmetic here measurably manufactured spurious near-zero
    /// generalized eigenvalues (an artifact of losing orthogonality against
    /// the rigid modes, not a real soft mode) that then masqueraded as a
    /// smaller "true" critical load than either BucklingAnalysis or physics
    /// agree on. Only the float[]/StiffnessAssembly boundary stays float,
    /// since that is what the production Multiply signatures require.
    /// </summary>
    public static class DenseEigenOracle
    {
        /// <summary>Smallest positive lambda solving K phi = -lambda*K_G phi,
        /// found by dense reduction to the rigid-mode complement. Returns
        /// +Infinity if no reduced eigenvalue of A = K^-1*(-K_G) is
        /// positive.</summary>
        public static float SmallestPositiveLambda(StiffnessAssembly k, GeometricStiffness kg, float[][] rigidModes)
        {
            int n = k.DofCount;

            var kd = new double[n, n];
            var gd = new double[n, n]; // -K_G, dense
            var e = new float[n];
            var col = new float[n];
            for (int j = 0; j < n; j++)
            {
                Array.Clear(e, 0, n);
                e[j] = 1f;

                k.Multiply(e, col);
                for (int i = 0; i < n; i++) kd[i, j] = col[i];

                kg.Multiply(e, col);
                for (int i = 0; i < n; i++) gd[i, j] = -col[i];
            }

            var v = new double[3][];
            for (int i = 0; i < 3; i++)
            {
                v[i] = new double[n];
                for (int i2 = 0; i2 < n; i2++) v[i][i2] = rigidModes[i][i2];
            }
            OrthonormalizeInPlace(v, n);

            var q = ComplementBasis(v, n);
            int m = q.GetLength(1);

            var kr = Reduce(kd, q, n, m);
            var gr = Reduce(gd, q, n, m);
            Symmetrize(kr, m);
            Symmetrize(gr, m);

            var l = Cholesky(kr, m);
            var mid = ReduceGeneralized(l, gr, m);
            Symmetrize(mid, m);

            var eigVecs = new double[m, m];
            var eigVals = new double[m];
            Jacobi(mid, m, eigVals, eigVecs);

            double best = double.PositiveInfinity;
            for (int i = 0; i < m; i++)
            {
                double mu = eigVals[i];
                if (mu > 1e-7)
                {
                    double lambda = 1.0 / mu;
                    if (lambda < best) best = lambda;
                }
            }
            return (float)best;
        }

        /// <summary>Orthonormal basis (n x (n - modes.Length)) for the
        /// complement of the given orthonormal `modes`, built by two-pass
        /// (reorthogonalized) Gram-Schmidt over the standard basis of R^n --
        /// a single pass loses enough orthogonality against the rigid modes
        /// at a few hundred dof to manufacture a spurious near-zero
        /// generalized eigenvalue later (see the class doc).</summary>
        static double[,] ComplementBasis(double[][] modes, int n)
        {
            var basis = new List<double[]>();
            for (int j = 0; j < n; j++)
            {
                var e = new double[n];
                e[j] = 1.0;

                for (int pass = 0; pass < 2; pass++)
                {
                    foreach (var mode in modes)
                    {
                        double dot = Dot(e, mode, n);
                        for (int i = 0; i < n; i++) e[i] -= dot * mode[i];
                    }
                    foreach (var b in basis)
                    {
                        double dot = Dot(e, b, n);
                        for (int i = 0; i < n; i++) e[i] -= dot * b[i];
                    }
                }

                double norm = Math.Sqrt(Dot(e, e, n));
                if (norm > 1e-6)
                {
                    for (int i = 0; i < n; i++) e[i] /= norm;
                    basis.Add(e);
                }
            }

            int m = basis.Count;
            var q = new double[n, m];
            for (int j = 0; j < m; j++)
            for (int i = 0; i < n; i++)
                q[i, j] = basis[j][i];
            return q;
        }

        /// <summary>Gram-Schmidt orthonormalization of `modes` in place --
        /// a private reimplementation rather than reusing CgSolver's
        /// internal helper, since this oracle must stay independent of the
        /// production code path it is checking.</summary>
        static void OrthonormalizeInPlace(double[][] modes, int n)
        {
            for (int i = 0; i < modes.Length; i++)
            {
                var mi = modes[i];
                for (int j = 0; j < i; j++)
                {
                    var mj = modes[j];
                    double dot = Dot(mi, mj, n);
                    for (int k = 0; k < n; k++) mi[k] -= dot * mj[k];
                }
                double norm = Math.Sqrt(Dot(mi, mi, n));
                if (norm > 1e-10)
                    for (int k = 0; k < n; k++) mi[k] /= norm;
            }
        }

        static double Dot(double[] a, double[] b, int n)
        {
            double s = 0.0;
            for (int i = 0; i < n; i++) s += a[i] * b[i];
            return s;
        }

        /// <summary>q^T * a * q, an m x m dense matrix from an n x n one.</summary>
        static double[,] Reduce(double[,] a, double[,] q, int n, int m)
        {
            var aq = new double[n, m];
            for (int i = 0; i < n; i++)
            for (int j = 0; j < m; j++)
            {
                double sum = 0.0;
                for (int k = 0; k < n; k++) sum += a[i, k] * q[k, j];
                aq[i, j] = sum;
            }

            var result = new double[m, m];
            for (int i = 0; i < m; i++)
            for (int j = 0; j < m; j++)
            {
                double sum = 0.0;
                for (int k = 0; k < n; k++) sum += q[k, i] * aq[k, j];
                result[i, j] = sum;
            }
            return result;
        }

        static void Symmetrize(double[,] a, int n)
        {
            for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                double avg = 0.5 * (a[i, j] + a[j, i]);
                a[i, j] = avg;
                a[j, i] = avg;
            }
        }

        static double[,] Cholesky(double[,] a, int n)
        {
            var l = new double[n, n];
            for (int i = 0; i < n; i++)
            for (int j = 0; j <= i; j++)
            {
                double sum = a[i, j];
                for (int k = 0; k < j; k++) sum -= l[i, k] * l[j, k];
                if (i == j)
                    l[i, j] = Math.Sqrt(Math.Max(sum, 1e-20));
                else
                    l[i, j] = sum / l[j, j];
            }
            return l;
        }

        /// <summary>L^-1 * gr * L^-T.</summary>
        static double[,] ReduceGeneralized(double[,] l, double[,] gr, int n)
        {
            var temp = new double[n, n];
            for (int col = 0; col < n; col++)
            for (int i = 0; i < n; i++)
            {
                double sum = gr[i, col];
                for (int k = 0; k < i; k++) sum -= l[i, k] * temp[k, col];
                temp[i, col] = sum / l[i, i];
            }

            var m = new double[n, n];
            for (int row = 0; row < n; row++)
            for (int i = 0; i < n; i++)
            {
                double sum = temp[row, i];
                for (int k = 0; k < i; k++) sum -= l[i, k] * m[row, k];
                m[row, i] = sum / l[i, i];
            }
            return m;
        }

        static void Jacobi(double[,] a, int n, double[] eigenvalues, double[,] eigenvectors)
        {
            var m = (double[,])a.Clone();
            for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                eigenvectors[i, j] = i == j ? 1.0 : 0.0;

            const int maxSweeps = 300;
            const double tolerance = 1e-16;

            for (int sweep = 0; sweep < maxSweeps; sweep++)
            {
                double off = 0.0;
                for (int p = 0; p < n; p++)
                for (int q = p + 1; q < n; q++)
                    off += m[p, q] * m[p, q];
                if (off < tolerance) break;

                for (int p = 0; p < n; p++)
                for (int q = p + 1; q < n; q++)
                {
                    if (Math.Abs(m[p, q]) < 1e-18) continue;

                    double theta = (m[q, q] - m[p, p]) / (2.0 * m[p, q]);
                    double t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1.0));
                    if (theta == 0.0) t = 1.0;
                    double c = 1.0 / Math.Sqrt(t * t + 1.0);
                    double s = t * c;

                    double mpp = m[p, p], mqq = m[q, q], mpq = m[p, q];
                    m[p, p] = c * c * mpp - 2.0 * s * c * mpq + s * s * mqq;
                    m[q, q] = s * s * mpp + 2.0 * s * c * mpq + c * c * mqq;
                    m[p, q] = 0.0;
                    m[q, p] = 0.0;

                    for (int i = 0; i < n; i++)
                    {
                        if (i == p || i == q) continue;
                        double mip = m[i, p], miq = m[i, q];
                        m[i, p] = c * mip - s * miq;
                        m[p, i] = m[i, p];
                        m[i, q] = s * mip + c * miq;
                        m[q, i] = m[i, q];
                    }

                    for (int i = 0; i < n; i++)
                    {
                        double vip = eigenvectors[i, p], viq = eigenvectors[i, q];
                        eigenvectors[i, p] = c * vip - s * viq;
                        eigenvectors[i, q] = s * vip + c * viq;
                    }
                }
            }

            for (int i = 0; i < n; i++) eigenvalues[i] = m[i, i];
        }
    }
}
