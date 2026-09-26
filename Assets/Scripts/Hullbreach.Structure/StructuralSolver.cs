using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Hullbreach.Structure
{
    // Per-block stress result for one tick: the S36/S37 API. VonMises and
    // the principal stresses are from the quasi-static (ductile) solve.
    // frob:doc docs/reference/hullbreach-structure.md#blockstress
    public struct BlockStress
    {
        // frob:doc docs/reference/hullbreach-structure.md#blockstress
        public float VonMises;
        // frob:doc docs/reference/hullbreach-structure.md#blockstress
        public float Major;
        // frob:doc docs/reference/hullbreach-structure.md#blockstress
        public float Minor;
        // frob:doc docs/reference/hullbreach-structure.md#blockstress
        public float DuctileRatio;
        // frob:doc docs/reference/hullbreach-structure.md#blockstress
        public float BrittleRatio;

        // Raw element-center stress tensor (quasi-static case); needed by
        // GeometricStiffness, not just the reduced ratios above.
        // frob:doc docs/reference/hullbreach-structure.md#blockstress
        public float Sxx;
        // frob:doc docs/reference/hullbreach-structure.md#blockstress
        public float Syy;
        // frob:doc docs/reference/hullbreach-structure.md#blockstress
        public float Txy;

        // CLIENT-SAFE continuous tint (unlike StructuralSolver.BuckledBlocks);
        // see the reference page for why it must never decide a break.
        // frob:doc docs/reference/hullbreach-structure.md#blockstress
        public float BucklingRatio;
    }

    // Ties NodeLattice/Q8Element/StiffnessAssembly/LoadVector/CgSolver
    // together for one tick; see docs/reference/hullbreach-structure.md#structuralsolver.
    // frob:doc docs/reference/hullbreach-structure.md#structuralsolver
    public sealed class StructuralSolver
    {
        readonly StiffnessAssembly _assembly = new StiffnessAssembly();
        readonly CgSolver _solver = new CgSolver();
        readonly CoarsePreconditioner _coarse = new CoarsePreconditioner();

        // Per-ship Krylov state, carried across ticks so the per-tick
        // budget COMPOUNDS; invalidated below on every K/preconditioner rebuild.
        readonly CgState _cgState = new CgState();
        bool _lastUsedCoarse = true;

        int _lastRebuiltCount = -1;
        bool _forceRebuild = true;

        float[] _displacement = Array.Empty<float>();

        // Reused across ticks so a converged, steady-state ship never
        // allocates in its per-tick hot path.
        readonly LoadVector _loadVector;
        float[] _loadBuffer = Array.Empty<float>();
        float[][] _rigidModes = new float[3][] { Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>() };

        // 400 is a starting point, not a measured number; see
        // SolverBenchmarks for per-tick ms at a few ship sizes to retune it.
        // frob:doc docs/reference/hullbreach-structure.md#structuralsolver
        public int MaxCgIterationsPerTick = 400;

        // False means BlockStresses reflects a partially converged
        // displacement field, and buckling was skipped this tick.
        // frob:doc docs/reference/hullbreach-structure.md#structuralsolver
        public bool Converged { get; private set; }

        // frob:doc docs/reference/hullbreach-structure.md#structuralsolver
        public float ResidualNorm { get; private set; }

        // How much of MaxCgIterationsPerTick this tick actually spent.
        // frob:doc docs/reference/hullbreach-structure.md#structuralsolver
        public int IterationsThisTick { get; private set; }

        // False on the tick after any topology/stiffness rebuild or real
        // load change, when a restart is the only correct answer.
        // frob:doc docs/reference/hullbreach-structure.md#structuralsolver
        public bool ContinuedFromLastTick { get; private set; }

        // A ship under a steady load should see this climb until converged.
        // frob:doc docs/reference/hullbreach-structure.md#structuralsolver
        public int TicksSinceRestart { get; private set; }

        // Exposed read-only for callers (e.g. SolverBenchmarks) reporting
        // problem size alongside iteration counts.
        // frob:doc docs/reference/hullbreach-structure.md#structuralsolver
        public int DofCount => _assembly.DofCount;

        // Wires the cached LoadVector to this instance's StiffnessAssembly
        // once, since Rebuild mutates it in place across future rebuilds.
        // frob:doc docs/reference/hullbreach-structure.md#structuralsolver
        public StructuralSolver()
        {
            _loadVector = new LoadVector(_assembly);
            // NOT loosened to 1e-3: this also gates RunBuckling's
            // Converged check; see the reference page for the regressed tests.
        }

        // Count alone cannot detect a same-key type swap.
        // frob:doc docs/reference/hullbreach-structure.md#structuralsolver
        public void MarkTopologyChanged() => _forceRebuild = true;

        // On by default: augments Jacobi with CoarsePreconditioner's
        // deflated coarse correction. Switchable for SolverBenchmarks comparison.
        // frob:doc docs/reference/hullbreach-structure.md#structuralsolver
        public bool UseCoarseCorrection = true;

        // frob:doc docs/reference/hullbreach-structure.md#structuralsolver
        public Dictionary<int, BlockStress> BlockStresses { get; } = new Dictionary<int, BlockStress>();

        // Converts GAMEPLAY force units into the solver's normalized
        // material units before the solve; see the reference page.
        // frob:doc docs/reference/hullbreach-structure.md#structuralsolver
        public float LoadScale = 1f;

        // Ratio of real Young's modulus to yield stress BlockType omits,
        // applied to buckling load factors only; see the reference page.
        // frob:doc docs/reference/hullbreach-structure.md#structuralsolver
        public float MaterialStiffnessScale = 1f;

        // Runs one structural solve: rebuilds K if topology changed,
        // applies point forces plus inertia relief, solves, fills BlockStresses.
        // frob:doc docs/reference/hullbreach-structure.md#structuralsolver
        public void Tick(Hullbreach.Core.BlockGrid grid, IReadOnlyList<(float2 point, float2 force)> appliedForces, float dt)
        {
            bool needsRebuild = _forceRebuild || grid.TopologyDirty || grid.Count != _lastRebuiltCount;
            if (needsRebuild)
            {
                _assembly.Rebuild(grid);
                _lastRebuiltCount = grid.Count;
                _forceRebuild = false;

                if (_displacement.Length != _assembly.DofCount)
                {
                    _displacement = new float[_assembly.DofCount];
                    _loadBuffer = new float[_assembly.DofCount];
                    _rigidModes[0] = new float[_assembly.DofCount];
                    _rigidModes[1] = new float[_assembly.DofCount];
                    _rigidModes[2] = new float[_assembly.DofCount];
                }

                // Rigid modes depend only on NodeRestPositions, which only
                // changes here, keeping the hot path allocation-free.
                LoadVector.RigidBodyModes(_assembly.NodeRestPositions, _rigidModes);

                // Kc depends only on K's current values/sparsity, both of
                // which only change on this same rebuild.
                if (UseCoarseCorrection) _coarse.Rebuild(_assembly);

                // K and (if attached) M^-1 both just changed, so the
                // stored r/p/rz describe a system that no longer exists. Restart.
                _cgState.Invalidate();
            }

            // Same reasoning for the preconditioner being switched on or
            // off mid-run: M^-1 changed without K changing, no rebuild flag covers it.
            if (UseCoarseCorrection != _lastUsedCoarse)
            {
                _cgState.Invalidate();
                _lastUsedCoarse = UseCoarseCorrection;
            }

            var f = _loadBuffer;
            Array.Clear(f, 0, f.Length);
            _loadVector.QuasiStatic = f;

            foreach (var (point, force) in appliedForces)
                _loadVector.AddPointForce(f, point, force);

            _loadVector.ApplyInertiaRelief(f, grid, out _, out _);

            // Applied AFTER inertia relief so the self-equilibrated load
            // set is scaled as a whole, not left unbalanced.
            if (LoadScale != 1f)
            {
                for (int i = 0; i < f.Length; i++) f[i] *= LoadScale;
            }

            _solver.MaxIterations = MaxCgIterationsPerTick;
            _solver.Solve(_assembly, f, _displacement, _rigidModes, UseCoarseCorrection ? _coarse : null, _cgState);
            Converged = _solver.Converged;
            ResidualNorm = _solver.LastResidualNorm;
            IterationsThisTick = _solver.LastIterationCount;
            ContinuedFromLastTick = _cgState.ContinuedFromLastTick;
            TicksSinceRestart = _cgState.TicksSinceRestart;

            // Stress and damage decisions use whatever displacement is
            // available, converged or not (see the reference page on lag).
            ComputeBlockStress(grid);

            // Buckling's K_G is built FROM this tick's element stresses;
            // skip on an under-converged solve, let old modes stand.
            if (Converged) RunBuckling(grid);
            _tickIndex++;
        }

        // Reduces displacement to a per-block stress at the element
        // center; scratch below is reused across every block/tick.
        readonly float[,] _strainB = new float[3, Q8Element.DofCount];
        readonly int[] _stressNodeIds = new int[NodeLattice.NodesPerElement];
        readonly float[] _stressUe = new float[Q8Element.DofCount];
        readonly float[] _strainScratch = new float[3];
        readonly float[,] _dHatScratch = new float[3, 3];

        void ComputeBlockStress(Hullbreach.Core.BlockGrid grid)
        {
            BlockStresses.Clear();

            var b = _strainB;
            Q8Element.StrainDisplacement(0f, 0f, Hullbreach.Core.BlockType.Width, b);

            var nodeIds = _stressNodeIds;
            var ue = _stressUe;

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
                var strain = _strainScratch;
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

                var dHat = _dHatScratch;
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

        // --- Linearized buckling ---

        readonly GeometricStiffness _kg = new GeometricStiffness();
        readonly BucklingAnalysis _buckling = new BucklingAnalysis { MaxCgIterationsPerTick = 200 };

        int _tickIndex;
        int _bucklingDof = -1;
        readonly List<BucklingMode> _bucklingModes = new List<BucklingMode>();
        readonly List<int> _buckledBlocks = new List<int>();

        // Master switch; off entirely skips the geometric-stiffness
        // assembly and subspace iteration.
        // frob:doc docs/reference/hullbreach-structure.md#structuralsolver
        public bool BucklingEnabled = true;

        // A tick-count throttle, not a wall-clock one; previously
        // published modes stand between eligible ticks.
        // frob:doc docs/reference/hullbreach-structure.md#structuralsolver
        public int BucklingEveryNTicks = 4;

        // frob:doc docs/reference/hullbreach-structure.md#structuralsolver
        public int BucklingModeCount = 4;

        // Forwards to BucklingAnalysis.MaxCgIterationsPerTick; see the
        // reference page for the measured per-tick cost this caps.
        // frob:doc docs/reference/hullbreach-structure.md#structuralsolver
        public int BucklingMaxCgIterationsPerTick
        {
            get => _buckling.MaxCgIterationsPerTick;
            set => _buckling.MaxCgIterationsPerTick = value;
        }

        // frob:doc docs/reference/hullbreach-structure.md#structuralsolver
        public int BucklingMaxSweepsPerTick
        {
            get => _buckling.MaxSweepsPerTick;
            set => _buckling.MaxSweepsPerTick = value;
        }

        // Cumulative strain-energy fraction (descending) that defines "the
        // blocks that fold" in a sub-critical mode.
        // frob:doc docs/reference/hullbreach-structure.md#structuralsolver
        public float BucklingParticipationThreshold = 0.5f;

        // Empty when buckling is disabled, not yet converged, or nothing is
        // in compression.
        // frob:doc docs/reference/hullbreach-structure.md#structuralsolver
        public IReadOnlyList<BucklingMode> BucklingModes => _bucklingModes;

        // frob:doc docs/reference/hullbreach-structure.md#structuralsolver
        public float CriticalLoadFactor { get; private set; } = float.PositiveInfinity;

        // SERVER-AUTHORITATIVE, NOT CLIENT-SAFE: see the reference page for
        // why only the authority may read this to decide a block dies.
        // frob:doc docs/reference/hullbreach-structure.md#structuralsolver
        public IReadOnlyList<int> BuckledBlocks => _buckledBlocks;

        // Relative to the ship's own max von Mises so the test means the
        // same thing at every load scale; see the reference page.
        const float CompressionFloorFraction = 1e-3f;

        // Absolute backstop for an unloaded hull, so rounding noise near
        // zero does not count as compression.
        const float CompressionFloorAbsolute = 1e-6f;

        // Runs (at most every BucklingEveryNTicks ticks) one subspace step,
        // publishing only once converged; early-out if nothing compresses.
        void RunBuckling(Hullbreach.Core.BlockGrid grid)
        {
            if (!BucklingEnabled) return;
            if (_tickIndex % Math.Max(1, BucklingEveryNTicks) != 0) return;

            // One pass for the scale, one for the test: cheap Dictionary walk.
            float maxVonMises = 0f;
            foreach (var kvp in BlockStresses)
                if (kvp.Value.VonMises > maxVonMises) maxVonMises = kvp.Value.VonMises;

            float compressionFloor = -Math.Max(CompressionFloorFraction * maxVonMises, CompressionFloorAbsolute);

            bool anyCompression = false;
            foreach (var kvp in BlockStresses)
            {
                if (kvp.Value.Minor < compressionFloor) { anyCompression = true; break; }
            }

            if (!anyCompression)
            {
                _bucklingModes.Clear();
                _buckledBlocks.Clear();
                CriticalLoadFactor = float.PositiveInfinity;
                ClearBucklingRatios();
                return;
            }

            // Reset only on an actual DOF-COUNT change, not merely a K
            // rebuild (TopologyDirty can stay true with no real change).
            if (_bucklingDof != _assembly.DofCount)
            {
                _kg.AttachSparsity(_assembly);
                // _rigidModes was already recomputed this Tick for the
                // same DofCount; reuse instead of allocating a second set.
                _buckling.Reset(_assembly.DofCount, BucklingModeCount, _rigidModes);
                _bucklingDof = _assembly.DofCount;
            }

            _kg.Rebuild(grid, _assembly, BlockStresses);

            _buckling.Coarse = UseCoarseCorrection ? _coarse : null;
            bool converged = _buckling.Step(_assembly, _kg, _rigidModes, _tickIndex);
            if (!converged) return;

            var modes = _buckling.ExtractModes(grid, _assembly, _kg, BucklingModeCount);
            _bucklingModes.Clear();
            // Applied here, before anything reads a load factor, so
            // BuckledBlocks and BucklingRatio see the same corrected numbers.
            for (int i = 0; i < modes.Count; i++)
            {
                var mode = modes[i];
                mode.LoadFactor *= MaterialStiffnessScale;
                _bucklingModes.Add(mode);
            }
            CriticalLoadFactor = _bucklingModes.Count > 0 ? _bucklingModes[0].LoadFactor : float.PositiveInfinity;

            RecomputeBuckledBlocks();
            RecomputeBucklingRatios();
        }

        // Combines every sub-critical (LoadFactor <= 1) mode's
        // top-participation blocks into one sorted, de-duplicated list.
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

        // Writes BlockStress.BucklingRatio from the CRITICAL mode's
        // participation, scaled by 1/CriticalLoadFactor; 0 elsewhere.
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
