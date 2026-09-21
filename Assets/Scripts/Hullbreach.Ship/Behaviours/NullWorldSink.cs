using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Hullbreach.World;

namespace Hullbreach.Ship.Behaviours
{
    /// <summary>
    /// The do-nothing IWorldSink: every ShipBody defaults to this so tests
    /// (and any ship never wired to a real game scene) can Step without a
    /// null check at every call site. SpawnProjectile/AddTemporaryGravity are
    /// no-ops; TryNearestEnemy always fails; Ships is always empty.
    /// </summary>
    public sealed class NullWorldSink : IWorldSink
    {
        /// <summary>The single shared instance; this sink holds no state, so
        /// there is never a reason to allocate more than one.</summary>
        public static readonly NullWorldSink Instance = new NullWorldSink();

        NullWorldSink() { }

        /// <summary>No-op: nothing is listening.</summary>
        public void SpawnProjectile(in ShotRequest shot) { }

        /// <summary>No-op: there is no gravity field to add to.</summary>
        public void AddTemporaryGravity(GravityBody body, float seconds) { }

        /// <summary>Always false: a null sink knows about no other ships.</summary>
        public bool TryNearestEnemy(float2 from, ShipBody self, out float2 position, out float2 velocity)
        {
            position = float2.zero;
            velocity = float2.zero;
            return false;
        }

        /// <summary>Always empty.</summary>
        public IReadOnlyList<ShipBody> Ships => Array.Empty<ShipBody>();
    }
}
