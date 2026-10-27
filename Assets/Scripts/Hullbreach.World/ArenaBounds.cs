using Unity.Mathematics;

namespace Hullbreach.World
{
    // Circular arena edge with a soft inward push; never touches structure (S41-1).
    // frob:doc docs/reference/hullbreach-world.md#arenabounds
    public readonly struct ArenaBounds
    {
        // frob:doc docs/reference/hullbreach-world.md#arenabounds
        public readonly float2 Center;
        // Distance from Center at which the push reaches full strength.
        // frob:doc docs/reference/hullbreach-world.md#arenabounds
        public readonly float Radius;
        // Width of the ramp inside Radius where the push fades in.
        // frob:doc docs/reference/hullbreach-world.md#arenabounds
        public readonly float Margin;
        // Peak inward acceleration, in world units per second squared.
        // frob:doc docs/reference/hullbreach-world.md#arenabounds
        public readonly float Strength;

        // frob:doc docs/reference/hullbreach-world.md#arenabounds
        public ArenaBounds(float2 center, float radius, float margin, float strength)
        {
            Center = center;
            Radius = radius;
            Margin = math.clamp(margin, 0f, radius);
            Strength = strength;
        }

        // 0 inside the soft zone's inner edge, 1 at or beyond Radius.
        // frob:doc docs/reference/hullbreach-world.md#arenabounds
        public float Depth(float2 position)
        {
            float over = math.length(position - Center) - (Radius - Margin);
            if (over <= 0f) return 0f;
            return Margin <= 0f ? 1f : math.saturate(over / Margin);
        }

        // True once the position is inside the push ramp or past the edge.
        // frob:doc docs/reference/hullbreach-world.md#arenabounds
        public bool InPushZone(float2 position) => Depth(position) > 0f;

        // Inward acceleration for a position; zero well inside, constant Strength outside.
        // frob:doc docs/reference/hullbreach-world.md#arenabounds
        public float2 PushAcceleration(float2 position)
        {
            float depth = Depth(position);
            if (depth <= 0f) return float2.zero;
            return math.normalizesafe(Center - position) * (Strength * depth);
        }
    }
}
