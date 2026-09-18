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
            if (Contains(key)) return false;

            if (block.TypeId == BlockTypes.Core)
            {
                // If there is already a core in the grid, don't add it.
                if (CoreKey.HasValue) return false;
                CoreKey = key;
            }

            _blocks.Add(key, block);

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
