using Unity.Mathematics;

namespace Hullbreach.World
{
    // frob:doc docs/reference/hullbreach-world.md#orbithelper
    public static class OrbitHelper
    {
        // Fails (false, velocity zero) for a bad body index, a position on
        // the body's center, or Mu <= 0 (a repeller has no circular orbit).
        // frob:doc docs/reference/hullbreach-world.md#orbithelper
        public static bool TryCircularOrbitVelocity(GravityField field, int bodyIndex, float2 position, out float2 velocity)
        {
            velocity = float2.zero;
            if (!field.TryGetPermanent(bodyIndex, out var body)) return false;
            if (!(body.Mu > 0f)) return false;

            float2 r = position - body.Position;
            float dist = math.length(r);
            if (!(dist > 1e-6f)) return false;

            float accel = GravityField.AccelerationMagnitude(body, dist);
            float speed = math.sqrt(accel * dist);
            float2 radial = r / dist;
            float2 tangential = new float2(-radial.y, radial.x); // +90 degrees: CCW
            velocity = tangential * speed;
            return true;
        }
    }
}
