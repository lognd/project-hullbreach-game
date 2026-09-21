using System.Collections.Generic;
using UnityEngine;
using Hullbreach.Core;
using Hullbreach.Structure;

namespace Hullbreach.Game
{
    /// <summary>
    /// Runs the plain-C# StructuralSolver over a ship's grid every
    /// FixedUpdate, using the forces ShipBody recorded this Step, and applies
    /// damage/detach when a block's ratios cross DamageModel's thresholds.
    ///
    /// Ordered AFTER ShipController (-100) so ship.AppliedForcesThisStep for
    /// this tick is already populated, and BEFORE ShipRenderer (default 0) so
    /// the Stress overlay reads this tick's solve, not the previous one.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(ShipController))]
    public sealed class ShipStructure : MonoBehaviour
    {
        [SerializeField] ShipController controller;
        [SerializeField] ShipRenderer renderer_;

        /// <summary>Bounds how often the solver re-runs the linearized
        /// buckling subspace iteration; forwarded to Solver in Awake so it
        /// can be tuned per-ship without editing StructuralSolver's default.
        /// See StructuralSolver.BucklingEveryNTicks for why this must stay
        /// bounded (the eigen-solve is not free every FixedUpdate).</summary>
        [SerializeField] int bucklingEveryNTicks = 4;

        /// <summary>Number of buckling modes the subspace iteration tracks;
        /// forwarded to Solver in Awake. See StructuralSolver.BucklingModeCount.</summary>
        [SerializeField] int bucklingModeCount = 4;

        /// <summary>
        /// Gameplay-force to material-unit conversion, forwarded to
        /// StructuralSolver.LoadScale in Awake. See that property for why it
        /// exists; the default is measured, not guessed: at 1.0 the stock
        /// demo ship peaked at 1.08 of yield under its own full thrust and
        /// shed both thrusters within 0.2 s of the player pressing W, which
        /// is the "play mode instantly breaks" report. 0.06 puts that same
        /// peak near 0.34, inside the 0.3-to-0.5 band the demo is tuned to,
        /// and gravity alone near 0.02.
        /// </summary>
        [SerializeField] float loadScale = DefaultLoadScale;

        /// <summary>The calibrated default for <see cref="loadScale"/>, named
        /// so tests and docs can refer to the same number.</summary>
        public const float DefaultLoadScale = 0.06f;

        /// <summary>
        /// E-over-yield ratio missing from BlockType's normalized material
        /// table, forwarded to StructuralSolver.MaterialStiffnessScale in
        /// Awake. See that property: without it the demo ship read as
        /// buckling at a small fraction of its own thrust and ShipStructure
        /// dutifully detached the "buckled" blocks. 200 is a plausible
        /// E/yield for a stiff-but-not-steel structural material, and it is
        /// the knob that SEPARATES the two ends of the calibration: it moves
        /// buckling without touching stress at all, so the stock blob keeps a
        /// critical load factor in the tens while a slender 1-wide arm still
        /// folds. Measured at 1.0 the stock ship read a load factor of 0.13
        /// under its own thrust and ShipStructure detached the blocks that
        /// "buckled": a rubber ship folding up, not a metal one.
        /// </summary>
        [SerializeField] float materialStiffnessScale = DefaultMaterialStiffnessScale;

        /// <summary>The calibrated default for
        /// <see cref="materialStiffnessScale"/>.</summary>
        public const float DefaultMaterialStiffnessScale = 40f;

        /// <summary>
        /// Only the authoritative simulation may act on Solver.BuckledBlocks
        /// by detaching blocks: the FE solve is not bit-identical across
        /// machines, so a client independently detaching from BuckledBlocks
        /// can desync from the server (see StructuralSolver.BuckledBlocks).
        /// Non-authoritative instances (clients) still tint BucklingRatio via
        /// ShipRenderer but skip the break here; they act only on explicit
        /// block-died events broadcast by the server (see NetMessages.cs).
        /// </summary>
        public bool Authoritative = true;

        /// <summary>The underlying solver, exposed so ShipRenderer's Stress
        /// overlay (and diagnostics) can read BlockStresses directly.</summary>
        public StructuralSolver Solver { get; } = new StructuralSolver();

        /// <summary>
        /// Seconds a block must stay in the solver's BuckledBlocks set before
        /// it actually comes off.
        ///
        /// Buckling is published by an eigen-solve that only runs every
        /// BucklingEveryNTicks and only once its subspace iteration has
        /// converged, so the first tick that reports a sub-unity load factor
        /// is ALSO the first tick the player could have been told anything.
        /// Detaching on that tick means the HUD warning and the failure
        /// arrive in the same frame, which reads as "it just exploded".
        /// A real column does not collapse instantaneously either: it
        /// deflects, then goes. This hold is that deflection, and it is what
        /// makes the warning actionable rather than a post-mortem.
        /// </summary>
        [SerializeField] float bucklingHoldSeconds = 0.4f;

        readonly Dictionary<int, float> _buckledFor = new Dictionary<int, float>();
        readonly List<int> _buckledLongEnough = new List<int>();
        readonly Dictionary<int, bool> _failing = new Dictionary<int, bool>();
        readonly List<int> _toDamage = new List<int>();
        readonly List<int> _toDetach = new List<int>();

        void Awake()
        {
            if (controller == null) controller = GetComponent<ShipController>();
            if (renderer_ == null) renderer_ = GetComponent<ShipRenderer>();
            if (controller == null) Debug.LogError("ShipStructure requires a ShipController on the same GameObject.");
            if (renderer_ != null)
            {
                renderer_.Solver = Solver;
                renderer_.ExtraRatioSource = BucklingRatioFor;
            }
            Solver.LoadScale = loadScale;
            Solver.MaterialStiffnessScale = materialStiffnessScale;
            Solver.BucklingEveryNTicks = bucklingEveryNTicks;
            Solver.BucklingModeCount = bucklingModeCount;
        }

        /// <summary>Reads BlockStress.BucklingRatio for one block, 0 if the
        /// solver has no stress recorded for it yet; wired into ShipRenderer
        /// as the ExtraRatioSource for the Stress overlay's max(...) and used
        /// directly by the Buckling overlay.</summary>
        float BucklingRatioFor(int key)
            => Solver.BlockStresses.TryGetValue(key, out var stress) ? stress.BucklingRatio : 0f;

        void FixedUpdate()
        {
            if (controller == null || controller.Ship == null) return;
            var ship = controller.Ship;
            var grid = ship.Grid;
            if (grid.Count == 0) return;

            Solver.Tick(grid, ship.AppliedForcesThisStep, Time.fixedDeltaTime);

            _toDamage.Clear();
            foreach (var kvp in Solver.BlockStresses)
            {
                int key = kvp.Key;
                var stress = kvp.Value;
                bool wasFailing = _failing.TryGetValue(key, out var f) && f;
                bool shouldDetach = DamageModel.ShouldDetach(stress.DuctileRatio, stress.BrittleRatio, wasFailing);

                if (stress.DuctileRatio >= 1f || stress.BrittleRatio >= 1f)
                {
                    _toDamage.Add(key);
                    _failing[key] = true;
                }
                else
                {
                    _failing[key] = false;
                }

                if (shouldDetach) _toDetach.Add(key);
            }

            foreach (int key in _toDamage)
            {
                if (!grid.TryGet(key, out var block)) continue;
                Solver.BlockStresses.TryGetValue(key, out var stress);
                byte newDamage = DamageModel.Accumulate(block.Damage, stress.DuctileRatio, Time.fixedDeltaTime);
                grid.TrySet(key, block.WithDamage(newDamage));
            }

            if (_toDetach.Count > 0)
            {
                DetachAndCleanUp(grid);
            }

            _toDamage.Clear();
            _toDetach.Clear();

            if (Authoritative)
            {
                TickBucklingHold(Time.fixedDeltaTime);
                if (_buckledLongEnough.Count > 0) DetachAndCleanUp(grid, _buckledLongEnough);
            }
        }

        /// <summary>
        /// Advances each currently-buckling block's timer and collects the
        /// ones that have been buckling for longer than bucklingHoldSeconds
        /// into <see cref="_buckledLongEnough"/>. A block that stops
        /// buckling (the player eased off, or load redistributed) loses its
        /// accumulated time entirely rather than keeping partial credit: it
        /// survived, and the next overload starts the clock again.
        /// </summary>
        void TickBucklingHold(float dt)
        {
            _buckledLongEnough.Clear();

            var buckled = Solver.BuckledBlocks;
            for (int i = 0; i < buckled.Count; i++)
            {
                int key = buckled[i];
                float held = (_buckledFor.TryGetValue(key, out float t) ? t : 0f) + dt;
                _buckledFor[key] = held;
                if (held >= bucklingHoldSeconds) _buckledLongEnough.Add(key);
            }

            if (_buckledFor.Count == buckled.Count) return;

            // Drop timers for keys no longer buckling. Allocation-free in the
            // common case (nothing to drop) because of the count check above.
            _staleBuckling.Clear();
            foreach (var kvp in _buckledFor)
            {
                bool stillBuckling = false;
                for (int i = 0; i < buckled.Count; i++)
                {
                    if (buckled[i] == kvp.Key) { stillBuckling = true; break; }
                }
                if (!stillBuckling) _staleBuckling.Add(kvp.Key);
            }
            foreach (int key in _staleBuckling) _buckledFor.Remove(key);
        }

        readonly List<int> _staleBuckling = new List<int>();

        void DetachAndCleanUp(BlockGrid grid) => DetachAndCleanUp(grid, _toDetach);

        /// <summary>
        /// Same break path as the ductile/brittle stress failure above,
        /// reused for buckled blocks: remove the given keys, then remove
        /// whatever that stranded, rebuild derived views and mark the
        /// renderer/collider dirty. Only called for BuckledBlocks when
        /// Authoritative: see the Authoritative doc comment.
        /// </summary>
        void DetachAndCleanUp(BlockGrid grid, IReadOnlyList<int> keys)
        {
            foreach (int key in keys)
            {
                grid.TryRemove(key);
                _failing.Remove(key);
                _buckledFor.Remove(key);
            }

            var stranded = new List<int>();
            Connectivity.FindDetached(grid, stranded);
            foreach (int key in stranded)
            {
                grid.TryRemove(key);
                _failing.Remove(key);
            }

            Solver.MarkTopologyChanged();
            if (renderer_ != null) renderer_.MarkDirty();
            controller.Ship.RebuildDerivedViews();
        }
    }
}
