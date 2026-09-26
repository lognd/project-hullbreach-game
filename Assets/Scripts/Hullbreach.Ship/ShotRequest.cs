using Unity.Mathematics;

namespace Hullbreach.Ship
{
    // One cannon shot handed off by ShipBody.Step for the caller to spawn.
    // ShipBody only records intent (and applies its own recoil); it never
    // creates the projectile object, so this stays a plain value the caller
    // drains from ShipBody.PendingShots.
    // frob:doc docs/reference/hullbreach-ship.md#shotrequest
    public readonly struct ShotRequest
    {
        // Packed grid key of the firing cannon.
        // frob:doc docs/reference/hullbreach-ship.md#shotrequest
        public readonly int Key;

        // Muzzle position in world space.
        // frob:doc docs/reference/hullbreach-ship.md#shotrequest
        public readonly float2 WorldOrigin;

        // Unit fire direction in world space.
        // frob:doc docs/reference/hullbreach-ship.md#shotrequest
        public readonly float2 WorldDirection;

        // Projectile parameters to spawn with.
        // frob:doc docs/reference/hullbreach-ship.md#shotrequest
        public readonly ProjectileSpec Spec;

        // frob:doc docs/reference/hullbreach-ship.md#shotrequest
        public ShotRequest(int key, float2 worldOrigin, float2 worldDirection, ProjectileSpec spec)
        {
            Key = key;
            WorldOrigin = worldOrigin;
            WorldDirection = worldDirection;
            Spec = spec;
        }
    }
}
