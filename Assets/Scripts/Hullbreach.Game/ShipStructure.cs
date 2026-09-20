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
        /// Only the authoritative simulation may act on Solver.BuckledBlocks
        /// by detaching blocks -- the FE solve is not bit-identical across
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

            if (Authoritative && Solver.BuckledBlocks.Count > 0)
            {
                DetachAndCleanUp(grid, Solver.BuckledBlocks);
            }
        }

        void DetachAndCleanUp(BlockGrid grid) => DetachAndCleanUp(grid, _toDetach);

        /// <summary>
        /// Same break path as the ductile/brittle stress failure above,
        /// reused for buckled blocks: remove the given keys, then remove
        /// whatever that stranded, rebuild derived views and mark the
        /// renderer/collider dirty. Only called for BuckledBlocks when
        /// Authoritative -- see the Authoritative doc comment.
        /// </summary>
        void DetachAndCleanUp(BlockGrid grid, IReadOnlyList<int> keys)
        {
            foreach (int key in keys)
            {
                grid.TryRemove(key);
                _failing.Remove(key);
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
