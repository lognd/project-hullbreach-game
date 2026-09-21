using System.Collections.Generic;
using Unity.Mathematics;
using Hullbreach.World;

namespace Hullbreach.Ship.Behaviours
{
    /// <summary>
    /// Everything a block behaviour needs from "the rest of the world":
    /// spawning projectiles, dropping temporary gravity wells, and finding
    /// targets. Implemented by the game layer (WorldSink); ShipBody and its
    /// behaviours only ever see this interface, so the plain-C# simulation
    /// stays free of UnityEngine and testable with NullWorldSink.
    /// </summary>
    public interface IWorldSink
    {
        /// <summary>Spawns whatever object represents `shot` (visual,
        /// physics, or both; the sink decides).</summary>
        void SpawnProjectile(in ShotRequest shot);

        /// <summary>Adds a temporary gravity well/anti-well to the world's
        /// gravity field for `seconds`.</summary>
        void AddTemporaryGravity(GravityBody body, float seconds);

        /// <summary>Finds the nearest ship other than `self`, for a
        /// targeting behaviour (e.g. the seeking thruster). False (with
        /// default outs) when there is no other ship.</summary>
        bool TryNearestEnemy(float2 from, ShipBody self, out float2 position, out float2 velocity);

        /// <summary>Every ship currently known to the sink.</summary>
        IReadOnlyList<ShipBody> Ships { get; }
    }
}
