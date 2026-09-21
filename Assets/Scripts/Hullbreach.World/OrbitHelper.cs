using Unity.Mathematics;

namespace Hullbreach.World
{
    /// <summary>
    /// Helpers for placing something into a circular orbit around a
    /// GravityField body, used by DemoMode's "reset to orbit" key so a
    /// player can start (or return to) a stable pass without hand-tuning a
    /// velocity.
    /// </summary>
    public static class OrbitHelper
    {
        /// <summary>
        /// The world-space velocity that puts a massless object at `position`
        /// into a circular orbit around the body at `bodyIndex` inside
        /// `field`: v = sqrt(a(r) * r) tangential, counter-clockwise (rotate
        /// the outward radial direction +90 degrees), where a(r) is
        /// GravityField's softened acceleration law (so an orbit that dips
        /// inside SoftRadius still balances the actual pull applied there,
        /// not the un-softened Mu / r^2). Returns zero if the index is out
        /// of range or `position` coincides with the body (undefined orbit).
        /// </summary>
        public static float2 CircularOrbitVelocity(GravityField field, int bodyIndex, float2 position)
        {
            if (!field.TryGetPermanent(bodyIndex, out var body)) return float2.zero;
            float2 r = position - body.Position;
            float dist = math.length(r);
            if (dist <= 1e-6f) return float2.zero;

            float accel = GravityField.AccelerationMagnitude(body, dist);
            float speed = math.sqrt(accel * dist);
            float2 radial = r / dist;
            float2 tangential = new float2(-radial.y, radial.x); // +90 degrees: CCW
            return tangential * speed;
        }
    }
}
