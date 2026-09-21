using System;
using Unity.Mathematics;

namespace Hullbreach.Structure
{
    /// <summary>
    /// Builds the right-hand side, including INERTIA RELIEF.
    ///
    /// THE PROBLEM: a ship in space has no supports, so K is singular with a
    /// 3-dimensional null space (translate x, translate y, rotate). K u = f has
    /// a solution only when f is orthogonal to that null space, which
    /// physically means net force zero and net torque zero. An accelerating
    /// ship does not satisfy that.
    ///
    /// THE WRONG FIX: pin the core. One line, but it is false physics: the
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
        /// <summary>
        /// The assembly this load vector is built against. Needed to map a
        /// ship-local point to a containing element's nodes/dofs, and to know
        /// the dense node layout for inertia relief. Passed in rather than
        /// looked up globally so a LoadVector is always explicit about which
        /// assembly (and therefore which topology snapshot) it belongs to.
        /// </summary>
        readonly StiffnessAssembly _assembly;

        public LoadVector(StiffnessAssembly assembly)
        {
            _assembly = assembly;
        }

        /// <summary>The quasi-static case: thrust, gravity wells, contact.
        /// Checked against von Mises (ductile).</summary>
        public float[] QuasiStatic;

        /// <summary>The impulsive case: projectile impacts this tick only.
        /// Checked against max tensile principal stress (brittle).
        /// Separate because linear FE superposes exactly, so the two load
        /// cases can share one K and one assembly.</summary>
        public float[] Impulsive;

        /// <summary>
        /// Scatters a force applied at a world (ship-local) point into the
        /// nodal load vector, distributing it over the containing element's
        /// nodes by shape-function weight.
        /// </summary>
        public void AddPointForce(float[] target, float2 shipLocalPoint, float2 force)
        {
            int bx = (int)math.floor(shipLocalPoint.x);
            int by = (int)math.floor(shipLocalPoint.y);

            // Local coordinate within the block, mapped from [0,1] to [-1,1].
            float fx = shipLocalPoint.x - bx;
            float fy = shipLocalPoint.y - by;
            float xi = 2f * fx - 1f;
            float eta = 2f * fy - 1f;

            var n = new float[Q8Element.NodeCount];
            Q8Element.ShapeFunctions(xi, eta, n);

            var nodeIds = new int[NodeLattice.NodesPerElement];
            NodeLattice.NodesOf(bx, by, nodeIds);

            for (int i = 0; i < NodeLattice.NodesPerElement; i++)
            {
                if (!_assembly.NodeMap.TryGetValue(nodeIds[i], out int dense)) continue;
                target[2 * dense] += n[i] * force.x;
                target[2 * dense + 1] += n[i] * force.y;
            }
        }

        /// <summary>
        /// Applies inertia relief to `target`, in place. Returns the
        /// rigid-body acceleration it solved for, which the caller also wants
        /// for integrating the actual ship motion.
        ///
        /// Net force/torque are read off of `target` itself (the loads already
        /// scattered into it), and inertial body forces are then distributed
        /// per BLOCK across that block's 4 corner nodes (a lumped-mass
        /// simplification; midside nodes carry no mass in this scheme, which
        /// is standard practice and keeps the distribution trivial).
        /// </summary>
        public void ApplyInertiaRelief(float[] target, Hullbreach.Core.BlockGrid grid,
                                       out float2 linearAccel, out float angularAccel)
        {
            float2 com = grid.Mass.CenterOfMass;
            float totalMass = grid.Mass.Total;

            // Point-mass inertia (mass concentrated at each block's center),
            // NOT grid.Mass.InertiaAboutCenterOfMass: that value also folds
            // in each block's own spin inertia (RectangleInertia), which has
            // no counterpart in this lumped-corner-mass distribution below.
            // Using the wrong I here would make alpha inconsistent with how
            // torque is actually cancelled, leaving a residual net torque.
            float inertia = 0f;
            foreach (var kvp in grid.All)
            {
                float2 center = Hullbreach.Core.BlockGrid.CenterOf(kvp.Key);
                float mass = Hullbreach.Core.BlockTypes.Get(kvp.Value.TypeId).Mass;
                inertia += mass * math.lengthsq(center - com);
            }

            // Net force and net torque (about the center of mass) implied by
            // the currently scattered load.
            float2 netForce = float2.zero;
            float netTorque = 0f;

            var positions = _assembly.NodeRestPositions;
            for (int i = 0; i < positions.Length; i++)
            {
                float fx = target[2 * i];
                float fy = target[2 * i + 1];
                netForce.x += fx;
                netForce.y += fy;

                float2 r = positions[i] - com;
                netTorque += r.x * fy - r.y * fx;
            }

            linearAccel = totalMass > 0f ? netForce / totalMass : float2.zero;
            angularAccel = inertia > 0f ? netTorque / inertia : 0f;

            var nodeIds = new int[NodeLattice.NodesPerElement];
            foreach (var kvp in grid.All)
            {
                Hullbreach.Core.BlockKey.Unpack(kvp.Key, out int x, out int y);
                float2 center = Hullbreach.Core.BlockGrid.CenterOf(kvp.Key);
                float mass = Hullbreach.Core.BlockTypes.Get(kvp.Value.TypeId).Mass;

                float2 r = center - com;
                float2 alphaCrossR = new float2(-angularAccel * r.y, angularAccel * r.x);
                float2 bodyForce = -mass * (linearAccel + alphaCrossR);

                NodeLattice.NodesOf(x, y, nodeIds);
                // Only the four corners carry lumped mass.
                for (int i = 0; i < 4; i++)
                {
                    if (!_assembly.NodeMap.TryGetValue(nodeIds[i], out int dense)) continue;
                    target[2 * dense] += bodyForce.x / 4f;
                    target[2 * dense + 1] += bodyForce.y / 4f;
                }
            }
        }

        /// <summary>
        /// The three rigid-body modes as DOF vectors, for the projection above
        /// and for the solver's orthogonalization:
        ///   translate x : (1, 0) at every node
        ///   translate y : (0, 1) at every node
        ///   rotate      : (-y, x) at the node at (x, y)
        /// `modes` must already contain three preallocated arrays of length
        /// 2 * nodeRest.Length.
        /// </summary>
        public static void RigidBodyModes(float2[] nodeRest, float[][] modes)
        {
            var tx = modes[0];
            var ty = modes[1];
            var rot = modes[2];

            for (int i = 0; i < nodeRest.Length; i++)
            {
                float x = nodeRest[i].x;
                float y = nodeRest[i].y;

                tx[2 * i] = 1f;
                ty[2 * i + 1] = 1f;
                rot[2 * i] = -y;
                rot[2 * i + 1] = x;
            }
        }
    }
}
