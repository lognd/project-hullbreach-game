using System.Collections.Generic;
using UnityEngine;
using Hullbreach.Core;

namespace Hullbreach.Game
{
    /// <summary>
    /// Maintains one BoxCollider2D per block on the ship's grid, sized to one
    /// cell and offset to that cell's center, so Projectile can hit-test
    /// against real per-block geometry instead of one big hull box. Rebuilt
    /// whenever <see cref="MarkDirty"/> is called (BuilderController after an
    /// edit, ShipStructure after a detach).
    /// </summary>
    [RequireComponent(typeof(ShipController))]
    public sealed class ShipCollider : MonoBehaviour
    {
        [SerializeField] ShipController controller;

        bool _dirty = true;
        int _builtCount = -1;

        readonly Dictionary<int, BoxCollider2D> _colliders = new Dictionary<int, BoxCollider2D>();

        /// <summary>Maps a live collider back to the grid key it represents,
        /// for Projectile's OnTriggerEnter2D to look up which block was hit.</summary>
        public readonly Dictionary<Collider2D, int> ColliderToKey = new Dictionary<Collider2D, int>();

        void Awake()
        {
            if (controller == null) controller = GetComponent<ShipController>();
            if (controller == null) Debug.LogError("ShipCollider requires a ShipController on the same GameObject.");
        }

        /// <summary>Call after any edit to the ship's grid so the next
        /// LateUpdate rebuilds colliders.</summary>
        public void MarkDirty() => _dirty = true;

        void LateUpdate()
        {
            if (controller == null || controller.Ship == null) return;
            var grid = controller.Ship.Grid;
            if (!_dirty && grid.Count == _builtCount) return;

            var stale = new List<int>();
            foreach (var key in _colliders.Keys)
            {
                if (!grid.Contains(key)) stale.Add(key);
            }
            foreach (var key in stale)
            {
                var col = _colliders[key];
                ColliderToKey.Remove(col);
                Destroy(col);
                _colliders.Remove(key);
            }

            foreach (var kvp in grid.All)
            {
                if (_colliders.ContainsKey(kvp.Key)) continue;

                BlockKey.Unpack(kvp.Key, out int x, out int y);
                var col = gameObject.AddComponent<BoxCollider2D>();
                col.size = new Vector2(BlockType.Width, BlockType.Height);
                col.offset = new Vector2(x + 0.5f, y + 0.5f);
                _colliders[kvp.Key] = col;
                ColliderToKey[col] = kvp.Key;
            }

            _dirty = false;
            _builtCount = grid.Count;
        }
    }
}
