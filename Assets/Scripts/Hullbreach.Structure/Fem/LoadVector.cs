using System;
using Unity.Mathematics;

namespace Hullbreach.Structure
{
    // Builds the right-hand side, including INERTIA RELIEF (d'Alembert);
    // see docs/reference/hullbreach-structure.md#loadvector for why.
    // frob:doc docs/reference/hullbreach-structure.md#loadvector
    public sealed class LoadVector
    {
        // Passed in rather than looked up globally so a LoadVector is
        // always explicit about which topology snapshot it belongs to.
        readonly StiffnessAssembly _assembly;

        // frob:doc docs/reference/hullbreach-structure.md#loadvector
        public LoadVector(StiffnessAssembly assembly)
        {
            _assembly = assembly;
        }

        // The quasi-static case: thrust, gravity wells, contact. Checked
        // against von Mises (ductile).
        // frob:doc docs/reference/hullbreach-structure.md#loadvector
        public float[] QuasiStatic;

        // The impulsive case: projectile impacts this tick only, checked
        // against max tensile principal stress (brittle).
        // frob:doc docs/reference/hullbreach-structure.md#loadvector
        public float[] Impulsive;

        // Scratch for AddPointForce, so the per-tick hot path never allocates.
        readonly float[] _shape = new float[Q8Element.NodeCount];
        readonly int[] _pointNodeIds = new int[NodeLattice.NodesPerElement];

        // Scatters a force at a ship-local point into the nodal load vector
        // by shape-function weight over the containing element. Weights of
        // nodes absent from the structure are renormalized over the present
        // ones so the force magnitude is conserved. Returns false (target
        // untouched) for a non-finite or off-structure point.
        // frob:doc docs/reference/hullbreach-structure.md#loadvector
        public bool AddPointForce(float[] target, float2 shipLocalPoint, float2 force)
        {
            // Bounded so the int cast below is defined.
            const float Limit = 1e6f;
            if (!math.all(math.isfinite(shipLocalPoint)) || !math.all(math.isfinite(force))) return false;
            if (math.any(math.abs(shipLocalPoint) > Limit)) return false;

            int bx = (int)math.floor(shipLocalPoint.x);
            int by = (int)math.floor(shipLocalPoint.y);

            // Local coordinate within the block, mapped from [0,1] to [-1,1].
            float fx = shipLocalPoint.x - bx;
            float fy = shipLocalPoint.y - by;
            float xi = 2f * fx - 1f;
            float eta = 2f * fy - 1f;

            var n = _shape;
            Q8Element.ShapeFunctions(xi, eta, n);

            var nodeIds = _pointNodeIds;
            NodeLattice.NodesOf(bx, by, nodeIds);

            // Shape functions sum to 1, so the present subset's sum is the
            // fraction that would otherwise be lost.
            float present = 0f;
            for (int i = 0; i < NodeLattice.NodesPerElement; i++)
                if (_assembly.NodeMap.ContainsKey(nodeIds[i])) present += n[i];
            if (present < MinPresentWeight) return false;

            float scale = 1f / present;
            for (int i = 0; i < NodeLattice.NodesPerElement; i++)
            {
                if (!_assembly.NodeMap.TryGetValue(nodeIds[i], out int dense)) continue;
                target[2 * dense] += n[i] * scale * force.x;
                target[2 * dense + 1] += n[i] * scale * force.y;
            }
            return true;
        }

        // Below this the present nodes carry too little of the point's weight
        // to renormalize without amplifying noise.
        const float MinPresentWeight = 1e-3f;

        // Returns the rigid-body acceleration solved for; body forces are
        // distributed per block across its 4 corner (lumped-mass) nodes.
        // frob:doc docs/reference/hullbreach-structure.md#loadvector
        public void ApplyInertiaRelief(float[] target, Hullbreach.Core.BlockGrid grid,
                                       out float2 linearAccel, out float angularAccel)
        {
            float2 com = grid.Mass.CenterOfMass;
            float totalMass = grid.Mass.Total;

            // Point-mass inertia, NOT grid.Mass.InertiaAboutCenterOfMass:
            // that also folds in spin inertia with no counterpart here.
            float inertia = 0f;
            foreach (var kvp in grid.All)
            {
                float2 center = Hullbreach.Core.BlockGrid.CenterOf(kvp.Key);
                float mass = Hullbreach.Core.BlockTypes.Get(kvp.Value.TypeId).Mass;
                inertia += mass * math.lengthsq(center - com);
            }

            // Net force/torque about the center of mass, from the load
            // already scattered into `target`.
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

        // The three rigid-body modes as DOF vectors (translate x, translate
        // y, rotate); `modes` needs three preallocated length-2n arrays.
        // frob:doc docs/reference/hullbreach-structure.md#loadvector
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
