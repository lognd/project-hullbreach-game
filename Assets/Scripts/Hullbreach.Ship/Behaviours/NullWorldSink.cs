using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Hullbreach.World;

namespace Hullbreach.Ship.Behaviours
{
    // The do-nothing IWorldSink: every ShipBody defaults to this so tests
    // (and any ship never wired to a real game scene) can Step without a
    // null check at every call site. SpawnProjectile/AddTemporaryGravity are
    // no-ops; TryNearestEnemy always fails; Ships is always empty.
    // frob:doc docs/reference/hullbreach-ship.md#nullworldsink
    public sealed class NullWorldSink : IWorldSink
    {
        // This sink holds no state, so there is never a reason to allocate
        // more than one.
        // frob:doc docs/reference/hullbreach-ship.md#nullworldsink
        public static readonly NullWorldSink Instance = new NullWorldSink();

        NullWorldSink() { }

        // frob:doc docs/reference/hullbreach-ship.md#nullworldsink
        public void SpawnProjectile(in ShotRequest shot) { }

        // frob:doc docs/reference/hullbreach-ship.md#nullworldsink
        public void AddTemporaryGravity(GravityBody body, float seconds) { }

        // frob:doc docs/reference/hullbreach-ship.md#nullworldsink
        public bool TryNearestEnemy(float2 from, ShipBody self, out float2 position, out float2 velocity)
        {
            position = float2.zero;
            velocity = float2.zero;
            return false;
        }

        // frob:doc docs/reference/hullbreach-ship.md#nullworldsink
        public IReadOnlyList<ShipBody> Ships => Array.Empty<ShipBody>();
    }
}
