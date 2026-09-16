using System;

namespace Hullbreach.Structure
{
    /// <summary>
    /// Scatters each element's 16x16 into the sparse global K.
    ///
    /// The accumulation at shared nodes IS the structural connection: two
    /// blocks are joined precisely because they write into the same rows.
    ///
    /// K comes out symmetric positive SEMI-definite. It is singular, by three,
    /// because a free-floating ship has three rigid-body modes -- see
    /// LoadVector for how that is handled rather than papered over.
    /// </summary>
    public sealed class StiffnessAssembly
    {
        // TODO [C3]: Build sparse K for the whole grid. Choose a sparse format
        //            (CSR is fine) and keep it; the solver only ever needs
        //            K * v, so nothing fancier is required.
        //
        //            Rebuild only when topology is dirty. Damage changes E,
        //            which scales existing entries -- for a damage-only change
        //            you can rescale in place instead of reassembling.
        public void Rebuild(Hullbreach.Core.BlockGrid grid)
            => throw new NotImplementedException();

        // TODO [C3]: y = K * x. The only operation CG needs.
        public void Multiply(float[] x, float[] y)
            => throw new NotImplementedException();

        /// <summary>Number of degrees of freedom (2 per node).</summary>
        public int DofCount => throw new NotImplementedException();
    }
}
