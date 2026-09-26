using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Hullbreach.Core
{
    // The authoritative sparse block map; MUTABLE per-block state must live
    // here and nowhere else. See docs/reference/hullbreach-core.md#blockgrid.
    // frob:doc docs/reference/hullbreach-core.md#blockgrid
    public sealed class BlockGrid
    {
        readonly Dictionary<int, Block> _blocks = new Dictionary<int, Block>();

        // Sorted snapshot of `_blocks.Keys`, rebuilt lazily; see
        // docs/reference/hullbreach-core.md#blockgrid.
        int[] _sortedKeys = Array.Empty<int>();

        int _sortedKeyCount;

        int _structureVersion;

        int _keysVersion = -1;

        // Null when this grid is debris (no core to stay attached to).
        // frob:doc docs/reference/hullbreach-core.md#blockgrid
        public int? CoreKey { get; private set; }

        // frob:doc docs/reference/hullbreach-core.md#blockgrid
        public int Count => _blocks.Count;

        // Running mass / center of mass / inertia. O(1) per edit.
        // frob:doc docs/reference/hullbreach-core.md#blockgrid
        public MassProperties Mass;

        // True when a derived view needs rebuilding.
        // frob:doc docs/reference/hullbreach-core.md#blockgrid
        public bool TopologyDirty { get; private set; }

        // Struct enumerable so `foreach (var kv in grid.All)` boxes nothing.
        // frob:doc docs/reference/hullbreach-core.md#blockgrid
        public BlockEnumerable All => new BlockEnumerable(_blocks);

        // Thin non-boxing wrapper around Dictionary's enumerator; see
        // docs/reference/hullbreach-core.md#blockgridblockenumerable.
        // frob:doc docs/reference/hullbreach-core.md#blockgridblockenumerable
        public readonly struct BlockEnumerable : IEnumerable<KeyValuePair<int, Block>>
        {
            readonly Dictionary<int, Block> _blocks;

            // frob:doc docs/reference/hullbreach-core.md#blockgridblockenumerable
            public BlockEnumerable(Dictionary<int, Block> blocks) => _blocks = blocks;

            // The foreach pattern-match target; never boxes.
            // frob:doc docs/reference/hullbreach-core.md#blockgridblockenumerable
            public Dictionary<int, Block>.Enumerator GetEnumerator() => _blocks.GetEnumerator();

            // Interface fallback for LINQ/IEnumerable-typed consumers; boxes.
            // frob:doc docs/reference/hullbreach-core.md#blockgridblockenumerable
            IEnumerator<KeyValuePair<int, Block>> IEnumerable<KeyValuePair<int, Block>>.GetEnumerator() => _blocks.GetEnumerator();

            // Non-generic IEnumerable fallback.
            // frob:doc docs/reference/hullbreach-core.md#blockgridblockenumerable
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _blocks.GetEnumerator();
        }

        // Equal to Count; rebuilds the sorted view first if stale.
        // frob:doc docs/reference/hullbreach-core.md#blockgrid
        public int KeyCount
        {
            get
            {
                EnsureSortedKeys();
                return _sortedKeyCount;
            }
        }

        // 0 <= i < KeyCount. Rebuilds the sorted view first if stale.
        // frob:doc docs/reference/hullbreach-core.md#blockgrid
        public int KeyAt(int i)
        {
            EnsureSortedKeys();
            return _sortedKeys[i];
        }

        // Allocation-free deterministic key iteration; valid only until the
        // next TryAdd/TryRemove. See docs/reference/hullbreach-core.md#blockgrid.
        // frob:doc docs/reference/hullbreach-core.md#blockgrid
        public ReadOnlySpan<int> SortedKeys
        {
            get
            {
                EnsureSortedKeys();
                return new ReadOnlySpan<int>(_sortedKeys, 0, _sortedKeyCount);
            }
        }

        void EnsureSortedKeys()
        {
            if (_keysVersion == _structureVersion) return;

            if (_sortedKeys.Length < _blocks.Count)
                _sortedKeys = new int[_blocks.Count];

            int i = 0;
            foreach (int key in _blocks.Keys) _sortedKeys[i++] = key;
            Array.Sort(_sortedKeys, 0, i);

            _sortedKeyCount = i;
            _keysVersion = _structureVersion;
        }

        // frob:doc docs/reference/hullbreach-core.md#blockgrid
        public bool TryAdd(int key, Block block)
        {
            // If there is already a block, don't add it.
            if (Contains(key)) return false;

            if (block.TypeId == BlockTypes.Core)
            {
                // If there is already a core in the grid, don't add it.
                if (CoreKey.HasValue) return false;
                CoreKey = key;
            }

            _blocks.Add(key, block);
            _structureVersion++;

            // Required to recalculate mass and moment of inertia of ship to maintain O(1) invariant.
            float2 center = CenterOf(key);
            float mass = BlockTypes.Get(block.TypeId).Mass;
            float rot_inertia = MassProperties.RectangleInertia(mass, BlockType.Width, BlockType.Height);
            Mass.Add(mass, center, rot_inertia);

            // Mark topology for recomp.
            TopologyDirty = true;

            return true;
        } 

        // frob:doc docs/reference/hullbreach-core.md#blockgrid
        public bool TryRemove(int key)
        {
            // You may not delete core, and the block must be in the map.
            if (key == CoreKey) return false;
            if (_blocks.TryGetValue(key, out Block toRemove))
            {
                float2 center = CenterOf(key);
                float mass = BlockTypes.Get(toRemove.TypeId).Mass;
                float loc_inertia = MassProperties.RectangleInertia(mass, BlockType.Width, BlockType.Height);
                // Update accumulators.
                Mass.Remove(mass, center, loc_inertia);

                // Actually remove; leaving it would double-count mass on any
                // later Add at the same key.
                _blocks.Remove(key);
                _structureVersion++;

                // Mark topology for recomp.
                TopologyDirty = true;

                return true;
            }

            return false;
        }

        // frob:doc docs/reference/hullbreach-core.md#blockgrid
        public bool TryGet(int key, out Block block) => _blocks.TryGetValue(key, out block);

        // frob:doc docs/reference/hullbreach-core.md#blockgrid
        public bool Contains(int key) => _blocks.ContainsKey(key);

        // frob:doc docs/reference/hullbreach-core.md#blockgrid
        public bool TrySet(int key, Block block)
        {
            if (_blocks.TryGetValue(key, out Block toModify))
            {
                float oldMass = BlockTypes.Get(toModify.TypeId).Mass;
                float newMass = BlockTypes.Get(block.TypeId).Mass;

                if (oldMass != newMass)
                {
                    float2 center = CenterOf(key);

                    float oldInertia = MassProperties.RectangleInertia(oldMass, BlockType.Width, BlockType.Height);
                    float newInertia = MassProperties.RectangleInertia(newMass, BlockType.Width, BlockType.Height);

                    Mass.Remove(oldMass, center, oldInertia);
                    Mass.Add(newMass, center, newInertia);
                }

                // Write the new block; otherwise the caller's damage/modifier
                // update is silently lost.
                _blocks[key] = block;

                // Topology is LEFT UNCHANGED.

                return true;
            }
            return false;
        }

        // frob:doc docs/reference/hullbreach-core.md#blockgrid
        public void ClearDirty() => TopologyDirty = false;

        // Ship-local center of block `key`; block (x,y) spans [x,x+1] x [y,y+1].
        // frob:doc docs/reference/hullbreach-core.md#blockgrid
        public static float2 CenterOf(int key)
        {
            BlockKey.Unpack(key, out var x, out var y);
            return new float2(x + 0.5f, y + 0.5f);
        }
    }
}
