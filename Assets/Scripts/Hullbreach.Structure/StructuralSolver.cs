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

        /// <summary>Raw element-center stress tensor components (quasi-static
        /// case), plane-stress convention sigma = [[Sxx,Txy],[Txy,Syy]].
        /// GeometricStiffness needs the tensor itself, not just the
        /// von Mises/principal reductions.</summary>
        public float Sxx;
        public float Syy;
        public float Txy;

        /// <summary>
        /// Buckling risk tint: 1 / CriticalLoadFactor scaled by this block's
        /// strain-energy participation in the CRITICAL (lowest load factor)
        /// buckling mode; 0 when the block does not participate in that mode
        /// or no sub-critical mode exists. CLIENT-SAFE: unlike
        /// <see cref="StructuralSolver.BuckledBlocks"/>, this is a continuous
        /// float derived the same way on every machine's own solve and is
        /// meant for a color tint only: it must never be used to decide
        /// that a block breaks (see BuckledBlocks doc for why).
        /// </summary>
        public float BucklingRatio;
    }

    /// <summary>
    /// Ties NodeLattice/Q8Element/StiffnessAssembly/LoadVector/CgSolver
    /// together for one simulation tick, and reduces the resulting
    /// displacement field to a per-block stress.
    ///
    /// TOPOLOGY TRACKING: this class does NOT call grid.ClearDirty(); the
    /// ship branch owns that flag's lifecycle (other systems, e.g.
    /// Connectivity/Articulation, also read it). Instead it snapshots
    /// grid.TopologyDirty at the start of Tick and remembers whether it has
    /// already rebuilt for the current dirty streak, using its own
    /// `_lastRebuiltCount` compared against grid.Count as a cheap proxy: if
    /// the block count changed since the last rebuild, or the caller
    /// explicitly asks via MarkTopologyChanged, this rebuilds. This avoids
    /// ever mutating state owned by another module while still not
    /// re-assembling every tick.
    ///
    /// PER-TICK CONVERGENCE BUDGET: <see cref="MaxCgIterationsPerTick"/>
    /// bounds how much CG work one Tick call may spend on the quasi-static
    /// solve. A ship large enough that CG cannot reach tolerance within the
    /// budget (see CgSolver's doc on iterations scaling with ship width)
    /// keeps its PARTIAL displacement as next tick's warm start rather than
    /// blocking the frame or discarding progress, so <see cref="Converged"/>
    /// can be false for several ticks in a row while the solve slowly
    /// catches up as the warm start improves. Stress and damage decisions
    /// (<see cref="BlockStresses"/>) always use whatever displacement is
    /// available, so on an unconverged tick they LAG the true quasi-static
    /// answer by however far the residual still is from tolerance; this is
    /// deliberate (better a slightly stale stress field than a stalled
    /// frame) and is why buckling (which depends on that same stress field
    /// for its geometric stiffness) only runs when <see cref="Converged"/>.
    /// </summary>
    public sealed class StructuralSolver
    {
        readonly StiffnessAssembly _assembly = new StiffnessAssembly();
        readonly CgSolver _solver = new CgSolver();

        int _lastRebuiltCount = -1;
        bool _forceRebuild = true;

        float[] _displacement = Array.Empty<float>();

        /// <summary>Per-tick CG iteration budget: the quasi-static solve is
        /// warm-started from whatever displacement the previous tick left,
        /// so a ship too large to fully converge within one frame keeps
        /// making progress across ticks instead of either blocking the
        /// frame or silently returning nonsense (the pre-budget behavior
        /// was an unconditional 4000-iteration cap inside CgSolver, cheap
        /// for a plain mat-vec but not bounded to a frame budget). 400 is a
        /// starting point, not a measured number: see SolverBenchmarks for
        /// per-tick ms at a few ship sizes to retune it.</summary>
        public int MaxCgIterationsPerTick = 400;

        /// <summary>True when the most recent Tick's quasi-static solve met
        /// CgSolver's tolerance within <see cref="MaxCgIterationsPerTick"/>.
        /// False means <see cref="BlockStresses"/> reflects a partially
        /// converged displacement field (see the class remarks on lag) and
        /// buckling was skipped this tick (see RunBuckling).</summary>
        public bool Converged { get; private set; }

        /// <summary>CgSolver.LastResidualNorm from the most recent Tick's
        /// quasi-static solve, for callers that want to log/plot the
        /// convergence trend rather than just a bool.</summary>
        public float ResidualNorm { get; private set; }

        /// <summary>CgSolver.LastIterationCount from the most recent Tick:
        /// how much of MaxCgIterationsPerTick this tick actually spent.</summary>
        public int IterationsThisTick { get; private set; }

        /// <summary>Call when the caller knows topology changed but the block
        /// count happens to be unchanged (e.g. a block swapped for a
        /// different type at the same key): Count alone cannot detect that.</summary>
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

            _solver.MaxIterations = MaxCgIterationsPerTick;
            _solver.Solve(_assembly, f, _displacement, modes);
            Converged = _solver.Converged;
            ResidualNorm = _solver.LastResidualNorm;
            IterationsThisTick = _solver.LastIterationCount;

            // Stress and damage decisions use whatever displacement is
            // available, converged or not: see the class remarks on lag.
            ComputeBlockStress(grid);

            // Buckling's geometric stiffness is built FROM this tick's
            // element stresses (see GeometricStiffness.Rebuild), so an
            // under-converged solve would feed it a stress field that has
            // not settled yet and can bias the Rayleigh quotients the same
            // way an under-converged CG cap used to (see CgSolver's
            // MaxIterations doc): skip the sweep entirely rather than
            // spend it on stale input, and let the previously published
            // modes stand until a later tick converges.
            if (Converged) RunBuckling(grid);
            _tickIndex++;
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
                    Sxx = sxx,
                    Syy = syy,
                    Txy = txy,
                };
            }
        }

        // ---------------------------------------------------------------
        // Linearized buckling.
        // ---------------------------------------------------------------

        readonly GeometricStiffness _kg = new GeometricStiffness();
        readonly BucklingAnalysis _buckling = new BucklingAnalysis();

        int _tickIndex;
        int _bucklingDof = -1;
        readonly List<BucklingMode> _bucklingModes = new List<BucklingMode>();
        readonly List<int> _buckledBlocks = new List<int>();

        /// <summary>Master switch; on by default. Off entirely skips the
        /// geometric-stiffness assembly and subspace iteration.</summary>
        public bool BucklingEnabled = true;

        /// <summary>Buckling is attempted at most once every this many ticks
        /// (a tick-count throttle, not a wall-clock one; see
        /// BucklingAnalysis's determinism doc). Between eligible ticks the
        /// previously published modes stand.</summary>
        public int BucklingEveryNTicks = 4;

        /// <summary>Modes requested from the subspace iteration.</summary>
        public int BucklingModeCount = 4;

        /// <summary>Forwards to BucklingAnalysis.MaxSweepsPerTick: exposed
        /// here so a caller (or a test wanting faster convergence than the
        /// production per-tick budget) can tune the per-tick cost cap without
        /// reaching into StructuralSolver's private analysis instance.</summary>
        public int BucklingMaxSweepsPerTick
        {
            get => _buckling.MaxSweepsPerTick;
            set => _buckling.MaxSweepsPerTick = value;
        }

        /// <summary>
        /// Cumulative strain-energy fraction (descending by block
        /// participation) that defines "the blocks that fold" in a
        /// sub-critical mode: e.g. 0.5 means the fewest highest-energy
        /// blocks whose participation sums to half the mode's energy.
        /// </summary>
        public float BucklingParticipationThreshold = 0.5f;

        /// <summary>Most recent converged buckling modes, ascending by load
        /// factor. Empty when buckling is disabled, not yet converged since
        /// the last topology change, or the ship has no compression anywhere.</summary>
        public IReadOnlyList<BucklingMode> BucklingModes => _bucklingModes;

        /// <summary>Smallest load factor among <see cref="BucklingModes"/>,
        /// or +infinity when there is none (nothing sub-critical, or no
        /// converged analysis yet).</summary>
        public float CriticalLoadFactor { get; private set; } = float.PositiveInfinity;

        /// <summary>
        /// Union, across every mode with LoadFactor &lt;= 1, of the blocks
        /// making up <see cref="BucklingParticipationThreshold"/> of that
        /// mode's strain energy: i.e. every block that some independent
        /// sub-critical fold wants to break, combined, because a ship can
        /// fold in two places at once and both must break.
        ///
        /// SERVER-AUTHORITATIVE, NOT CLIENT-SAFE: the float FE solve is not
        /// bit-identical across machines. Only the authoritative simulation
        /// may read this list to decide a block dies and then BROADCAST that
        /// as an event; a client independently reading this and detaching a
        /// block itself can disagree with the server and desync. Clients
        /// must use BlockStress.BucklingRatio (a tint, not a decision) instead.
        /// </summary>
        public IReadOnlyList<int> BuckledBlocks => _buckledBlocks;

        /// <summary>Minor-principal-stress floor below which compression
        /// counts as real for the early-out below. Pure tension still leaves
        /// a whisper of local transverse compression at a point-load's
        /// application node from Poisson coupling: that is a load-
        /// application artifact, not a structural instability, so it must
        /// not by itself keep the subspace machinery running every tick.</summary>
        const float CompressionFloor = -1e-3f;

        /// <summary>
        /// Runs (at most every BucklingEveryNTicks ticks) the geometric
        /// stiffness assembly and one step of the subspace iteration, and
        /// publishes modes/BuckledBlocks/BucklingRatio only once it has
        /// converged. Cheap early-out: if no element anywhere is meaningfully
        /// in compression, K_G is positive semidefinite (see
        /// GeometricStiffness' sign convention), so there is no positive
        /// lambda to find; skipped without ever touching the subspace
        /// machinery.
        /// </summary>
        void RunBuckling(Hullbreach.Core.BlockGrid grid)
        {
            if (!BucklingEnabled) return;
            if (_tickIndex % Math.Max(1, BucklingEveryNTicks) != 0) return;

            bool anyCompression = false;
            foreach (var kvp in BlockStresses)
            {
                if (kvp.Value.Minor < CompressionFloor) { anyCompression = true; break; }
            }

            if (!anyCompression)
            {
                _bucklingModes.Clear();
                _buckledBlocks.Clear();
                CriticalLoadFactor = float.PositiveInfinity;
                ClearBucklingRatios();
                return;
            }

            // Reset only on an actual DOF-COUNT change, not merely because K
            // was numerically rebuilt this tick: StructuralSolver rebuilds K
            // whenever grid.TopologyDirty is set, and that flag's lifecycle
            // belongs to the ship branch (see the class doc); it can stay
            // true for many ticks in a row with no real topology change
            // (e.g. in a test harness that never clears it), and discarding
            // a perfectly good warm-started subspace every such tick would
            // make convergence impossible. The subspace and K_G's sparsity
            // are only actually invalidated when the number of DOFs changes.
            if (_bucklingDof != _assembly.DofCount)
            {
                _kg.AttachSparsity(_assembly);

                var rigid = new float[3][];
                rigid[0] = new float[_assembly.DofCount];
                rigid[1] = new float[_assembly.DofCount];
                rigid[2] = new float[_assembly.DofCount];
                LoadVector.RigidBodyModes(_assembly.NodeRestPositions, rigid);

                _buckling.Reset(_assembly.DofCount, BucklingModeCount, rigid);
                _bucklingDof = _assembly.DofCount;
            }

            _kg.Rebuild(grid, _assembly, BlockStresses);

            var modes3 = new float[3][];
            modes3[0] = new float[_assembly.DofCount];
            modes3[1] = new float[_assembly.DofCount];
            modes3[2] = new float[_assembly.DofCount];
            LoadVector.RigidBodyModes(_assembly.NodeRestPositions, modes3);

            bool converged = _buckling.Step(_assembly, _kg, modes3, _tickIndex);
            if (!converged) return;

            var modes = _buckling.ExtractModes(grid, _assembly, BucklingModeCount);
            _bucklingModes.Clear();
            _bucklingModes.AddRange(modes);
            CriticalLoadFactor = _bucklingModes.Count > 0 ? _bucklingModes[0].LoadFactor : float.PositiveInfinity;

            RecomputeBuckledBlocks();
            RecomputeBucklingRatios();
        }

        /// <summary>Combines every sub-critical (LoadFactor &lt;= 1) mode's
        /// top-participation blocks (cumulative to BucklingParticipationThreshold)
        /// into one sorted, de-duplicated list: see BuckledBlocks' doc for
        /// why this is deterministic and server-only.</summary>
        void RecomputeBuckledBlocks()
        {
            var set = new HashSet<int>();
            foreach (var mode in _bucklingModes)
            {
                if (mode.LoadFactor > 1f) continue;

                var ordered = new List<KeyValuePair<int, float>>(mode.BlockParticipation);
                ordered.Sort((a, b) =>
                {
                    int cmp = b.Value.CompareTo(a.Value);
                    return cmp != 0 ? cmp : a.Key.CompareTo(b.Key);
                });

                float cumulative = 0f;
                foreach (var kvp in ordered)
                {
                    if (cumulative >= BucklingParticipationThreshold) break;
                    set.Add(kvp.Key);
                    cumulative += kvp.Value;
                }
            }

            _buckledBlocks.Clear();
            _buckledBlocks.AddRange(set);
            _buckledBlocks.Sort();
        }

        /// <summary>Writes BlockStress.BucklingRatio from the CRITICAL mode's
        /// participation, scaled by 1/CriticalLoadFactor; 0 elsewhere.</summary>
        void RecomputeBucklingRatios()
        {
            if (_bucklingModes.Count == 0 || float.IsInfinity(CriticalLoadFactor))
            {
                ClearBucklingRatios();
                return;
            }

            var critical = _bucklingModes[0].BlockParticipation;
            float inv = 1f / CriticalLoadFactor;

            var keys = new List<int>(BlockStresses.Keys);
            foreach (var key in keys)
            {
                var stress = BlockStresses[key];
                stress.BucklingRatio = critical.TryGetValue(key, out float frac) ? inv * frac : 0f;
                BlockStresses[key] = stress;
            }
        }

        void ClearBucklingRatios()
        {
            var keys = new List<int>(BlockStresses.Keys);
            foreach (var key in keys)
            {
                var stress = BlockStresses[key];
                stress.BucklingRatio = 0f;
                BlockStresses[key] = stress;
            }
        }
    }
}
