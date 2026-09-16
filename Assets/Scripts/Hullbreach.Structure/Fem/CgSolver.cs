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

        // TODO [C5]: Standard PCG with M = diag(K). Warm-start from the `u`
        //            passed in rather than zeroing it.
        //
        //            Because K is singular, re-project the residual onto the
        //            complement of the rigid-body modes periodically, or
        //            rounding will slowly excite them.
        public void Solve(StiffnessAssembly k, float[] f, float[] u, float[][] rigidModes)
            => throw new NotImplementedException();
    }
}
