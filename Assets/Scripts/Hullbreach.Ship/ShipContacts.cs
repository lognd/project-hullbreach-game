using Unity.Mathematics;
using Hullbreach.Core;

namespace Hullbreach.Ship
{
    // Ship-to-ship contact, resolved in plain C# by the same authority
    // that integrates the ships; see docs/reference/hullbreach-ship.md#shipcontacts.
    // frob:doc docs/reference/hullbreach-ship.md#shipcontacts
    public static class ShipContacts
    {
        // frob:doc docs/reference/hullbreach-ship.md#shipcontacts
        public const float BlockRadius = 0.5f;

        // frob:doc docs/reference/hullbreach-ship.md#shipcontacts
        public const float Restitution = 0.2f;

        // Resolves at most one contact (the deepest overlapping block
        // pair) between `a` and `b` for this step.
        // frob:doc docs/reference/hullbreach-ship.md#shipcontacts
        public static bool Resolve(ShipBody a, ShipBody b, float dt)
        {
            if (a == null || b == null || ReferenceEquals(a, b)) return false;

            float massA = a.Grid.Mass.Total;
            float massB = b.Grid.Mass.Total;
            if (massA <= 0f || massB <= 0f) return false;

            // Broad phase: the two ships cannot touch if their bounding
            // circles do not. Cheap, and it is the common case.
            float reachA = BoundingRadius(a);
            float reachB = BoundingRadius(b);
            if (math.distancesq(a.Position, b.Position) > (reachA + reachB) * (reachA + reachB)) return false;

            if (!FindDeepestPair(a, b, out int keyA, out int keyB,
                                 out float2 normal, out float penetration, out float2 contactPoint))
            {
                return false;
            }

            // Separate along the normal, split by inverse mass: the
            // lighter ship moves further.
            float inverseSum = (1f / massA) + (1f / massB);
            float shareA = (1f / massA) / inverseSum;
            a.Position -= normal * penetration * shareA;
            b.Position += normal * penetration * (1f - shareA);

            // Relative velocity of the two contacting blocks, not of the two
            // ship origins: a spinning ship's rim hits harder than its hub.
            float2 velocityA = PointVelocity(a, contactPoint);
            float2 velocityB = PointVelocity(b, contactPoint);
            float2 relative = velocityB - velocityA;
            float closing = math.dot(relative, normal);

            if (closing < 0f)
            {
                // Standard 2D contact formula, including both ships'
                // rotational terms so an off-center hit spins them too.
                float2 rA = contactPoint - a.LocalToWorld(a.Grid.Mass.CenterOfMass);
                float2 rB = contactPoint - b.LocalToWorld(b.Grid.Mass.CenterOfMass);
                float crossA = rA.x * normal.y - rA.y * normal.x;
                float crossB = rB.x * normal.y - rB.y * normal.x;
                float inertiaA = a.Grid.Mass.InertiaAboutCenterOfMass;
                float inertiaB = b.Grid.Mass.InertiaAboutCenterOfMass;

                float denominator = inverseSum
                    + (inertiaA > 0f ? crossA * crossA / inertiaA : 0f)
                    + (inertiaB > 0f ? crossB * crossB / inertiaB : 0f);
                if (denominator <= 0f) return true;

                float magnitude = -(1f + Restitution) * closing / denominator;
                float2 impulse = normal * magnitude;

                // Equal and opposite: momentum is conserved by construction.
                a.ApplyImpulseAtWorldPoint(contactPoint, -impulse);
                b.ApplyImpulseAtWorldPoint(contactPoint, impulse);
            }

            float impactSpeed = math.abs(closing);
            if (impactSpeed > a.ContactDamageSpeed)
            {
                ApplyContactDamage(a, keyA, impactSpeed);
                ApplyContactDamage(b, keyB, impactSpeed);
            }

            return true;
        }

        // Distance from the ship origin to the far corner of its furthest
        // block, for the broad-phase circle test.
        static float BoundingRadius(ShipBody ship)
        {
            var keys = ship.Grid.SortedKeys;
            float worst = 0f;
            for (int i = 0; i < keys.Length; i++)
            {
                float2 center = BlockGrid.CenterOf(keys[i]);
                worst = math.max(worst, math.lengthsq(center));
            }
            return math.sqrt(worst) + BlockRadius;
        }

        // Deepest overlapping block-disc pair, normal pointing from `a`
        // toward `b`, plus penetration depth and contact point.
        static bool FindDeepestPair(ShipBody a, ShipBody b,
                                    out int keyA, out int keyB,
                                    out float2 normal, out float penetration, out float2 contactPoint)
        {
            keyA = 0;
            keyB = 0;
            normal = new float2(1f, 0f);
            penetration = 0f;
            contactPoint = float2.zero;

            const float touchDistance = 2f * BlockRadius;
            float touchDistanceSq = touchDistance * touchDistance;
            bool found = false;

            var keysA = a.Grid.SortedKeys;
            var keysB = b.Grid.SortedKeys;
            for (int i = 0; i < keysA.Length; i++)
            {
                float2 centerA = a.LocalToWorld(BlockGrid.CenterOf(keysA[i]));
                for (int j = 0; j < keysB.Length; j++)
                {
                    float2 centerB = b.LocalToWorld(BlockGrid.CenterOf(keysB[j]));
                    float2 delta = centerB - centerA;
                    float distanceSq = math.lengthsq(delta);
                    if (distanceSq >= touchDistanceSq) continue;

                    float distance = math.sqrt(distanceSq);
                    float depth = touchDistance - distance;
                    if (depth <= penetration) continue;

                    // Exactly coincident centers give no normal to push
                    // along; fall back to +x rather than dividing by zero.
                    float2 direction = distance > 1e-6f ? delta / distance : new float2(1f, 0f);

                    found = true;
                    penetration = depth;
                    normal = direction;
                    keyA = keysA[i];
                    keyB = keysB[j];
                    contactPoint = (centerA + centerB) * 0.5f;
                }
            }

            return found;
        }

        // World velocity of a world-space point rigidly attached to `ship`:
        // v + w x r about the world center of mass.
        static float2 PointVelocity(ShipBody ship, float2 worldPoint)
        {
            float2 r = worldPoint - ship.LocalToWorld(ship.Grid.Mass.CenterOfMass);
            return ship.Velocity + ship.AngularVelocity * new float2(-r.y, r.x);
        }

        // Applies the same speed-proportional damage a planet impact does,
        // so one collision model covers both.
        static void ApplyContactDamage(ShipBody ship, int key, float impactSpeed)
        {
            if (!ship.Grid.TryGet(key, out var block)) return;
            float amount = math.clamp((impactSpeed - ship.ContactDamageSpeed) * ship.ContactDamagePerSpeed, 0f, 255f);
            int updated = math.min(255, block.Damage + (int)amount);
            ship.Grid.TrySet(key, block.WithDamage((byte)updated));
        }
    }
}
