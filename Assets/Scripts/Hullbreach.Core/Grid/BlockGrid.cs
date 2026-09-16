using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Hullbreach.Core
{
    /// <summary>
    /// The authoritative sparse block map. Everything else in the simulation is
    /// a derived view rebuilt from this, so MUTABLE per-block state (damage,
    /// upgrades) must live here and nowhere else -- a derived view that holds
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

        public IEnumerable<KeyValuePair<int, Block>> All => _blocks;

        public bool TryAdd(int key, Block block)
        {

            // If there is already a block, don't add it.
            if (_blocks.ContainsKey(key)) return false;

            if (block.TypeId == BlockTypes.Core)
            {
                // If there is already a core in the grid, don't add it.
                if (CoreKey.HasValue) return false;
                CoreKey = key;
            }

            _blocks.Add(key, block);

            // Required to recalculate mass and moment of inertia of ship to maintain O(1) invariant.
            float2 center = BlockGrid.CenterOf(key);
            float mass = BlockTypes.Get(block.TypeId).Mass;
            Mass.Add(mass, center, MassProperties.RectangleInertia(mass, BlockType.Width, BlockType.Height));

            // Mark topology for recomp.
            TopologyDirty = true;

            return true;
        } 

        // TODO [A1]: Remove at `key`. Refuse to remove the core (S32: the core
        //            cannot be removed). Update accumulators and mark dirty.
        public bool TryRemove(int key)
            => throw new NotImplementedException();

        // TODO [A1]
        public bool TryGet(int key, out Block block)
            => throw new NotImplementedException();

        public bool Contains(int key) => _blocks.ContainsKey(key);

        /// <summary>
        /// Replace the block at `key`, for damage accumulation.
        /// </summary>
        // TODO [A1]: This must NOT mark topology dirty. Damage changes
        //            stiffness, not connectivity, and rebuilding the node map
        //            every damage tick would throw away the cached
        //            factorisations for no reason.
        public bool TrySet(int key, Block block)
            => throw new NotImplementedException();

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
