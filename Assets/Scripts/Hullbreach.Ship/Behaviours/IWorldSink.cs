using System.Collections.Generic;
using Unity.Mathematics;
using Hullbreach.World;

namespace Hullbreach.Ship.Behaviours
{
    // Everything a block behaviour needs from "the rest of the world":
    // spawning projectiles, dropping temporary gravity wells, and finding
    // targets. Implemented by the game layer (WorldSink); ShipBody and its
    // behaviours only ever see this interface, so the plain-C# simulation
    // stays free of UnityEngine and testable with NullWorldSink.
    // frob:doc docs/reference/hullbreach-ship.md#iworldsink
    public interface IWorldSink
    {
        // frob:doc docs/reference/hullbreach-ship.md#iworldsink
        void SpawnProjectile(in ShotRequest shot);

        // frob:doc docs/reference/hullbreach-ship.md#iworldsink
        void AddTemporaryGravity(GravityBody body, float seconds);

        // False (with default outs) when there is no other ship.
        // frob:doc docs/reference/hullbreach-ship.md#iworldsink
        bool TryNearestEnemy(float2 from, ShipBody self, out float2 position, out float2 velocity);

        // frob:doc docs/reference/hullbreach-ship.md#iworldsink
        IReadOnlyList<ShipBody> Ships { get; }
    }
}
