using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Hullbreach.Core
{
    /// <summary>
    /// The authoritative sparse block map. Everything else in the simulation is
    /// a derived view rebuilt from this, so MUTABLE per-block state (damage,
    /// upgrades) must live here and nowhere else: a derived view that holds
    /// state silently loses it on the next rebuild.
    ///
    /// NOTE: a managed Dictionary on purpose. Get it correct first; swapping to
    /// NativeHashMap for Burst is a later mechanical change, and the tests will
    /// tell you immediately if it broke something.
    /// </summary>
    public sealed class BlockGrid
    {
        readonly Dictionary<int, Block> _blocks = new Dictionary<int, Block>();

        /// <summary>
        /// Sorted snapshot of `_blocks.Keys`, rebuilt lazily whenever
        /// `_keysVersion` no longer matches `_structureVersion`. TryAdd and
        /// TryRemove bump `_structureVersion` (they change the key set);
        /// TrySet does not (it only rewrites a value in place), so damage
        /// accumulation never pays for a resort.
        /// </summary>
        int[] _sortedKeys = Array.Empty<int>();

        /// <summary>Count of keys currently reflected in `_sortedKeys`.</summary>
        int _sortedKeyCount;

        /// <summary>Bumped by every structural edit (add/remove).</summary>
        int _structureVersion;

        /// <summary>`_structureVersion` as of the last `_sortedKeys` rebuild.</summary>
        int _keysVersion = -1;

        /// <summary>
        /// Packed key of the core, or null when this grid is debris.
        /// A fragment that breaks off has NO core, so connectivity has no root
        /// and simply does not run for it. This is why the core is nullable
        /// rather than assumed to sit at (0,0).
        /// </summary>
        public int? CoreKey { get; private set; }

        public int Count => _blocks.Count;

        /// <summary>Running mass / center of mass / inertia. O(1) per edit.</summary>
        public MassProperties Mass;

        /// <summary>
        /// True when a derived view needs rebuilding. At a few hundred blocks a
        /// full rebuild is microseconds; make this per-chunk only once
        /// profiling says to, and keep the accessor so callers never change.
        /// </summary>
        public bool TopologyDirty { get; private set; }

        /// <summary>
        /// Every (key, block) pair, in the dictionary's own iteration order
        /// (unspecified; use SortedKeys/KeyAt for deterministic order). A
        /// struct enumerable so `foreach (var kv in grid.All)` boxes nothing:
        /// it hands out `Dictionary&lt;int,Block&gt;.Enumerator` directly.
        /// </summary>
        public BlockEnumerable All => new BlockEnumerable(_blocks);

        /// <summary>
        /// Thin struct wrapper around `Dictionary&lt;int,Block&gt;.Enumerator`
        /// so foreach over `BlockGrid.All` never boxes the enumerator (a
        /// plain `IEnumerable&lt;T&gt;` return type would box it on every
        /// foreach, once per Step per grid).
        /// </summary>
        public readonly struct BlockEnumerable : IEnumerable<KeyValuePair<int, Block>>
        {
            readonly Dictionary<int, Block> _blocks;

            /// <summary>Wrap the dictionary whose entries this enumerates.</summary>
            public BlockEnumerable(Dictionary<int, Block> blocks) => _blocks = blocks;

            /// <summary>Struct enumerator; the foreach pattern-match target
            /// (the compiler prefers this over the interface methods below,
            /// so a plain `foreach (var kv in grid.All)` never boxes).</summary>
            public Dictionary<int, Block>.Enumerator GetEnumerator() => _blocks.GetEnumerator();

            /// <summary>Interface fallback for LINQ (e.g. `.Select`) and any
            /// other IEnumerable-typed consumer; boxes the enumerator, same
            /// as before this change, but only for callers that need the
            /// interface rather than a bare foreach.</summary>
            IEnumerator<KeyValuePair<int, Block>> IEnumerable<KeyValuePair<int, Block>>.GetEnumerator() => _blocks.GetEnumerator();

            /// <summary>Non-generic IEnumerable fallback.</summary>
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _blocks.GetEnumerator();
        }

        /// <summary>
        /// Number of keys in the sorted key view (equal to Count). Rebuilds
        /// the sorted view first if a structural edit happened since the
        /// last rebuild.
        /// </summary>
        public int KeyCount
        {
            get
            {
                EnsureSortedKeys();
                return _sortedKeyCount;
            }
        }

        /// <summary>
        /// The i-th smallest key, 0 &lt;= i &lt; KeyCount. Rebuilds the sorted
        /// view first if a structural edit happened since the last rebuild.
        /// </summary>
        public int KeyAt(int i)
        {
            EnsureSortedKeys();
            return _sortedKeys[i];
        }

        /// <summary>
        /// All keys in ascending order as a span, for allocation-free,
        /// deterministic iteration over the key set (no values). Rebuilds
        /// the sorted view first if a structural edit happened since the
        /// last rebuild. The span is only valid until the next structural
        /// edit (TryAdd/TryRemove), same as any other cached view.
        /// </summary>
        public ReadOnlySpan<int> SortedKeys
        {
            get
            {
                EnsureSortedKeys();
                return new ReadOnlySpan<int>(_sortedKeys, 0, _sortedKeyCount);
            }
        }

        /// <summary>Rebuild `_sortedKeys` from `_blocks` if stale.</summary>
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

                // Actually remove the block; leaving it in the dictionary
                // would let it keep occupying the cell and double-count mass
                // on any later Add at the same key.
                _blocks.Remove(key);
                _structureVersion++;

                // Mark topology for recomp.
                TopologyDirty = true;

                return true;
            }

            return false;
        }

        public bool TryGet(int key, out Block block) => _blocks.TryGetValue(key, out block);

        public bool Contains(int key) => _blocks.ContainsKey(key);

        /// <summary>
        /// Replace the block at `key`, for damage accumulation.
        /// </summary>
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

                // Actually write the new block into the dictionary; without
                // this the caller's damage/modifier update is silently lost.
                _blocks[key] = block;

                // Topology is LEFT UNCHANGED.

                return true;
            }
            return false;
        }

        public void ClearDirty() => TopologyDirty = false;

        /// <summary>
        /// Center of block `key` in ship-local space. See the grid convention
        /// on BlockKey: block (x,y) spans [x,x+1] x [y,y+1].
        /// </summary>
        public static float2 CenterOf(int key)
        {
            BlockKey.Unpack(key, out var x, out var y);
            return new float2(x + 0.5f, y + 0.5f);
        }
    }
}
