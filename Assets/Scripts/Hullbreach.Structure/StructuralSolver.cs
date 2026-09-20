using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Hullbreach.Structure
{
    /// <summary>
    /// Per-block stress result for one tick: the S36/S37 API. VonMises and
    /// the principal stresses are read from the QUASI-STATIC solve (ductile);
    /// the ratios summarize both criteria for whatever wants to color a block
    /// or decide it should break.
    /// </summary>
    public struct BlockStress
    {
        public float VonMises;
        public float Major;
        public float Minor;
        public float DuctileRatio;
        public float BrittleRatio;
    }

    /// <summary>
    /// Ties NodeLattice/Q8Element/StiffnessAssembly/LoadVector/CgSolver
    /// together for one simulation tick, and reduces the resulting
    /// displacement field to a per-block stress.
    ///
    /// TOPOLOGY TRACKING: this class does NOT call grid.ClearDirty() -- the
    /// ship branch owns that flag's lifecycle (other systems, e.g.
    /// Connectivity/Articulation, also read it). Instead it snapshots
    /// grid.TopologyDirty at the start of Tick and remembers whether it has
    /// already rebuilt for the current dirty streak, using its own
    /// `_lastRebuiltCount` compared against grid.Count as a cheap proxy: if
    /// the block count changed since the last rebuild, or the caller
    /// explicitly asks via MarkTopologyChanged, this rebuilds. This avoids
    /// ever mutating state owned by another module while still not
    /// re-assembling every tick.
    /// </summary>
    public sealed class StructuralSolver
    {
        readonly StiffnessAssembly _assembly = new StiffnessAssembly();
        readonly CgSolver _solver = new CgSolver();

        int _lastRebuiltCount = -1;
        bool _forceRebuild = true;

        float[] _displacement = Array.Empty<float>();

        /// <summary>Call when the caller knows topology changed but the block
        /// count happens to be unchanged (e.g. a block swapped for a
        /// different type at the same key) -- Count alone cannot detect that.</summary>
        public void MarkTopologyChanged() => _forceRebuild = true;

        /// <summary>Per-block stress results from the most recent Tick.</summary>
        public Dictionary<int, BlockStress> BlockStresses { get; } = new Dictionary<int, BlockStress>();

        /// <summary>
        /// Runs one structural solve: rebuilds K if topology changed, applies
        /// the given point forces plus inertia relief, solves for
        /// displacement, and fills <see cref="BlockStresses"/>.
        /// </summary>
        public void Tick(Hullbreach.Core.BlockGrid grid, IReadOnlyList<(float2 point, float2 force)> appliedForces, float dt)
        {
            bool needsRebuild = _forceRebuild || grid.TopologyDirty || grid.Count != _lastRebuiltCount;
            if (needsRebuild)
            {
                _assembly.Rebuild(grid);
                _lastRebuiltCount = grid.Count;
                _forceRebuild = false;

                if (_displacement.Length != _assembly.DofCount)
                    _displacement = new float[_assembly.DofCount];
            }

            var loads = new LoadVector(_assembly);
            var f = new float[_assembly.DofCount];
            loads.QuasiStatic = f;

            foreach (var (point, force) in appliedForces)
                loads.AddPointForce(f, point, force);

            loads.ApplyInertiaRelief(f, grid, out _, out _);

            var modes = new float[3][];
            modes[0] = new float[_assembly.DofCount];
            modes[1] = new float[_assembly.DofCount];
            modes[2] = new float[_assembly.DofCount];
            LoadVector.RigidBodyModes(_assembly.NodeRestPositions, modes);

            _solver.Solve(_assembly, f, _displacement, modes);

            ComputeBlockStress(grid);
        }

        /// <summary>Reduces the solved displacement field to a per-block
        /// stress, evaluated at the element center (xi = eta = 0).</summary>
        void ComputeBlockStress(Hullbreach.Core.BlockGrid grid)
        {
            BlockStresses.Clear();

            var b = new float[3, Q8Element.DofCount];
            Q8Element.StrainDisplacement(0f, 0f, Hullbreach.Core.BlockType.Width, b);

            var nodeIds = new int[NodeLattice.NodesPerElement];
            var ue = new float[Q8Element.DofCount];

            foreach (var kvp in grid.All)
            {
                Hullbreach.Core.BlockKey.Unpack(kvp.Key, out int x, out int y);
                NodeLattice.NodesOf(x, y, nodeIds);

                for (int i = 0; i < NodeLattice.NodesPerElement; i++)
                {
                    int dense = _assembly.NodeMap[nodeIds[i]];
                    ue[2 * i] = _displacement[2 * dense];
                    ue[2 * i + 1] = _displacement[2 * dense + 1];
                }

                // strain = B * u_e
                var strain = new float[3];
                for (int r = 0; r < 3; r++)
                {
                    float sum = 0f;
                    for (int c = 0; c < Q8Element.DofCount; c++)
                        sum += b[r, c] * ue[c];
                    strain[r] = sum;
                }

                var block = kvp.Value;
                var type = Hullbreach.Core.BlockTypes.Get(block.TypeId);
                float e = Hullbreach.Core.BlockTypes.EffectiveStiffness(block);

                var dHat = new float[3, 3];
                Q8Element.ConstitutiveUnit(Q8Element.NuFor(type.PoissonClass), dHat);

                float sxx = e * (dHat[0, 0] * strain[0] + dHat[0, 1] * strain[1] + dHat[0, 2] * strain[2]);
                float syy = e * (dHat[1, 0] * strain[0] + dHat[1, 1] * strain[1] + dHat[1, 2] * strain[2]);
                float txy = e * (dHat[2, 0] * strain[0] + dHat[2, 1] * strain[1] + dHat[2, 2] * strain[2]);

                float vm = StressCriteria.VonMises(sxx, syy, txy);
                StressCriteria.Principal(sxx, syy, txy, out float major, out float minor);

                float ductile = StressCriteria.DuctileRatio(vm, type.YieldStress, block.DamageFraction);
                float brittle = StressCriteria.BrittleRatio(major, minor, type.SpallStress, type.CompressiveStress);

                BlockStresses[kvp.Key] = new BlockStress
                {
                    VonMises = vm,
                    Major = major,
                    Minor = minor,
                    DuctileRatio = ductile,
                    BrittleRatio = brittle,
                };
            }
        }
    }
}
