using Unity.Mathematics;

namespace Hullbreach.World
{
    // frob:doc docs/reference/hullbreach-world.md#gravitybody
    public struct GravityBody
    {
        // frob:doc docs/reference/hullbreach-world.md#gravitybody
        public const float DefaultSoftRadiusFactor = 1.5f;

        // frob:doc docs/reference/hullbreach-world.md#gravitybody
        public const float MinSoftRadius = 2f;

        // frob:doc docs/reference/hullbreach-world.md#gravitybody
        public float2 Position;

        // frob:doc docs/reference/hullbreach-world.md#gravitybody
        public float Mu;

        // frob:doc docs/reference/hullbreach-world.md#gravitybody
        public float Radius;

        // frob:doc docs/reference/hullbreach-world.md#gravitybody
        public float SoftRadius;

        // frob:doc docs/reference/hullbreach-world.md#gravitybody
        public float SurfaceRestitution;

        // frob:doc docs/reference/hullbreach-world.md#gravitybody
        public GravityBody(float2 position, float mu, float radius, float surfaceRestitution, float softRadius)
        {
            Position = position;
            Mu = mu;
            Radius = radius;
            SurfaceRestitution = surfaceRestitution;
            SoftRadius = softRadius;
        }

        // frob:doc docs/reference/hullbreach-world.md#gravitybody
        public GravityBody(float2 position, float mu, float radius, float surfaceRestitution)
            : this(position, mu, radius, surfaceRestitution,
                   math.max(radius * DefaultSoftRadiusFactor, MinSoftRadius))
        {
        }
    }
}
