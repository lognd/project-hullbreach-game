using Unity.Mathematics;

namespace Hullbreach.World
{
    // frob:doc docs/reference/hullbreach-world.md#orbithelper
    public static class OrbitHelper
    {
        // frob:doc docs/reference/hullbreach-world.md#orbithelper
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
