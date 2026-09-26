using System.Collections.Generic;
using UnityEngine;
using Hullbreach.Core;
using Hullbreach.Structure;

namespace Hullbreach.Game
{
    // Ordered AFTER ShipController (-100) so ship.AppliedForcesThisStep for
    // this tick is already populated, and BEFORE ShipRenderer (default 0) so
    // the Stress overlay reads this tick's solve, not the previous one.
    // frob:doc docs/reference/hullbreach-game.md#shipstructure
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(ShipController))]
    public sealed class ShipStructure : MonoBehaviour
    {
        [SerializeField] ShipController controller;
        [SerializeField] ShipRenderer renderer_;

        // Forwarded to Solver in Awake so it can be tuned per-ship without
        // editing StructuralSolver's default. See
        // StructuralSolver.BucklingEveryNTicks for why this must stay
        // bounded (the eigen-solve is not free every FixedUpdate).
        [SerializeField] int bucklingEveryNTicks = 4;

        [SerializeField] int bucklingModeCount = 4;

        // Gameplay-force to material-unit conversion, forwarded to
        // StructuralSolver.LoadScale in Awake; see the reference page for
        // how this and materialStiffnessScale were calibrated.
        [SerializeField] float loadScale = DefaultLoadScale;

        // frob:doc docs/reference/hullbreach-game.md#shipstructure
        public const float DefaultLoadScale = 0.06f;

        // E-over-yield ratio missing from BlockType's normalized material
        // table, forwarded to StructuralSolver.MaterialStiffnessScale in
        // Awake; see the reference page for how this was calibrated.
        [SerializeField] float materialStiffnessScale = DefaultMaterialStiffnessScale;

        // frob:doc docs/reference/hullbreach-game.md#shipstructure
        public const float DefaultMaterialStiffnessScale = 40f;

        // Only the authoritative simulation may act on Solver.BuckledBlocks
        // by detaching blocks: the FE solve is not bit-identical across
        // machines, so a client independently detaching from BuckledBlocks
        // can desync from the server (see StructuralSolver.BuckledBlocks).
        // Non-authoritative instances (clients) still tint BucklingRatio via
        // ShipRenderer but skip the break here; they act only on explicit
        // block-died events broadcast by the server (see NetMessages.cs).
        // frob:doc docs/reference/hullbreach-game.md#shipstructure
        public bool Authoritative = true;

        // Exposed so ShipRenderer's Stress overlay (and diagnostics) can
        // read BlockStresses directly.
        // frob:doc docs/reference/hullbreach-game.md#shipstructure
        public StructuralSolver Solver { get; } = new StructuralSolver();

        // Seconds a block must stay in the solver's BuckledBlocks set
        // before it actually comes off; see the reference page for why
        // this hold exists.
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

        // Wired into ShipRenderer as the ExtraRatioSource for the Stress
        // overlay's max(...) and used directly by the Buckling overlay.
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

        // A block that stops buckling (the player eased off, or load
        // redistributed) loses its accumulated time entirely rather than
        // keeping partial credit: it survived, and the next overload starts
        // the clock again.
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

        // Same break path as the ductile/brittle stress failure above,
        // reused for buckled blocks: remove the given keys, then remove
        // whatever that stranded, rebuild derived views and mark the
        // renderer/collider dirty. Only called for BuckledBlocks when
        // Authoritative: see the Authoritative comment above.
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
