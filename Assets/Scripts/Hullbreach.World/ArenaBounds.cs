using Unity.Mathematics;

namespace Hullbreach.World
{
    // A circular arena edge that pushes a point back with a spring, never a wall.
    // frob:doc docs/reference/hullbreach-world.md#arenabounds
    public sealed class ArenaBounds
    {
        // frob:doc docs/reference/hullbreach-world.md#arenabounds
        public const float DefaultStiffness = 4f;

        // Equal to DefaultStiffness gives critical damping on the way out.
        // frob:doc docs/reference/hullbreach-world.md#arenabounds
        public const float DefaultDamping = 4f;

        // frob:doc docs/reference/hullbreach-world.md#arenabounds
        public const float DefaultMaxAcceleration = 30f;

        // frob:doc docs/reference/hullbreach-world.md#arenabounds
        public readonly float2 Center;

        // frob:doc docs/reference/hullbreach-world.md#arenabounds
        public readonly float Radius;

        // frob:doc docs/reference/hullbreach-world.md#arenabounds
        public readonly float Stiffness;

        // frob:doc docs/reference/hullbreach-world.md#arenabounds
        public readonly float Damping;

        // frob:doc docs/reference/hullbreach-world.md#arenabounds
        public readonly float MaxAcceleration;

        // frob:doc docs/reference/hullbreach-world.md#arenabounds
        public ArenaBounds(float2 center, float radius,
                           float stiffness = DefaultStiffness,
                           float damping = DefaultDamping,
                           float maxAcceleration = DefaultMaxAcceleration)
        {
            Center = center;
            Radius = radius;
            Stiffness = stiffness;
            Damping = damping;
            MaxAcceleration = maxAcceleration;
        }

        // frob:doc docs/reference/hullbreach-world.md#arenabounds
        public bool Contains(float2 point) => math.lengthsq(point - Center) <= Radius * Radius;

        // Damping only opposes OUTWARD speed: on the way back the spring
        // runs undamped, so the point crosses the edge moving inward
        // instead of creeping up to it asymptotically.
        // frob:doc docs/reference/hullbreach-world.md#arenabounds
        public float2 PushBackAcceleration(float2 point, float2 velocity)
        {
            float2 r = point - Center;
            float dist = math.length(r);
            if (dist <= Radius) return float2.zero;

            float2 outward = r / dist;
            float2 accel = -outward * (Stiffness * (dist - Radius));

            float outwardSpeed = math.dot(velocity, outward);
            if (outwardSpeed > 0f) accel -= outward * (Damping * outwardSpeed);

            float mag = math.length(accel);
            if (mag > MaxAcceleration) accel = accel / mag * MaxAcceleration;
            return accel;
        }
    }
}
