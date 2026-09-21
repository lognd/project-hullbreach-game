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
            if (renderer_ != null) renderer_.Solver = Solver;
        }

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
        }

        void DetachAndCleanUp(BlockGrid grid)
        {
            foreach (int key in _toDetach)
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
