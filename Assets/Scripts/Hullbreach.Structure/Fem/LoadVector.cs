using System;
using Unity.Mathematics;

namespace Hullbreach.Structure
{
    /// <summary>
    /// Builds the right-hand side, including INERTIA RELIEF.
    ///
    /// THE PROBLEM: a ship in space has no supports, so K is singular with a
    /// 3-dimensional null space (translate x, translate y, rotate). K u = f has
    /// a solution only when f is orthogonal to that null space -- which
    /// physically means net force zero and net torque zero. An accelerating
    /// ship does not satisfy that.
    ///
    /// THE WRONG FIX: pin the core. One line, but it is false physics -- the
    /// pinned node supplies reaction forces, so stress piles up at the core and
    /// a distant thruster reads as a lever against it.
    ///
    /// THE RIGHT FIX (this file): d'Alembert. Work in the accelerating frame
    /// and add the inertial body force to every block:
    ///
    ///     a = F_net / M,   alpha = tau_net / I
    ///     f_eff_i = f_applied_i - m_i * (a + alpha x r_i)
    ///     with  alpha x r = alpha * (-r.y, r.x)  in 2D
    ///
    /// Summing forces gives F_net - M a = 0 and summing moments gives
    /// tau_net - I alpha = 0, so f_eff is self-equilibrated BY CONSTRUCTION and
    /// the system becomes solvable with nothing pinned.
    ///
    /// The solution u is still only determined up to a rigid-body mode, but B
    /// annihilates rigid modes, so the STRESS does not care. Only orthogonalize
    /// u against the modes if you want to draw the deformed shape.
    /// </summary>
    public sealed class LoadVector
    {
        /// <summary>The quasi-static case: thrust, gravity wells, contact.
        /// Checked against von Mises (ductile).</summary>
        public float[] QuasiStatic;

        /// <summary>The impulsive case: projectile impacts this tick only.
        /// Checked against max tensile principal stress (brittle).
        /// Separate because linear FE superposes exactly, so the two load
        /// cases can share one K and one assembly.</summary>
        public float[] Impulsive;

        // TODO [C4]: Scatter a force applied at a world point into the nodal
        //            load vector, distributing it over the containing element's
        //            nodes by shape-function weight.
        public void AddPointForce(float[] target, float2 shipLocalPoint, float2 force)
            => throw new NotImplementedException();

        // TODO [C4]: Apply inertia relief to `target`, in place. Returns the
        //            rigid-body acceleration it solved for, which the caller
        //            also wants for integrating the actual ship motion.
        //
        //            Verify with the test: after this call, the projection of
        //            `target` onto each of the three rigid-body modes must be
        //            ~0. That single assertion catches almost every sign error.
        public void ApplyInertiaRelief(float[] target, Hullbreach.Core.BlockGrid grid,
                                       out float2 linearAccel, out float angularAccel)
            => throw new NotImplementedException();

        // TODO [C4]: The three rigid-body modes as DOF vectors, for the
        //            projection above and for the solver's orthogonalization:
        //              translate x : (1, 0) at every node
        //              translate y : (0, 1) at every node
        //              rotate      : (-y, x) at the node at (x, y)
        public static void RigidBodyModes(float2[] nodeRest, float[][] modes)
            => throw new NotImplementedException();
    }
}
