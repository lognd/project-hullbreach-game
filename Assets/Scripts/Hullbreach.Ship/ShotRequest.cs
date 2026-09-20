using Unity.Mathematics;

namespace Hullbreach.Ship
{
    /// <summary>
    /// One cannon shot handed off by ShipBody.Step for the caller to spawn.
    /// ShipBody only records intent (and applies its own recoil); it never
    /// creates the projectile object, so this stays a plain value the caller
    /// drains from ShipBody.PendingShots.
    /// </summary>
    public readonly struct ShotRequest
    {
        /// <summary>Packed grid key of the firing cannon.</summary>
        public readonly int Key;

        /// <summary>Muzzle position in world space.</summary>
        public readonly float2 WorldOrigin;

        /// <summary>Unit fire direction in world space.</summary>
        public readonly float2 WorldDirection;

        /// <summary>Projectile parameters to spawn with.</summary>
        public readonly ProjectileSpec Spec;

        public ShotRequest(int key, float2 worldOrigin, float2 worldDirection, ProjectileSpec spec)
        {
            Key = key;
            WorldOrigin = worldOrigin;
            WorldDirection = worldDirection;
            Spec = spec;
        }
    }
}
