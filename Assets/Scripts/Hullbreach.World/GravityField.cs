using System.Collections.Generic;
using Unity.Mathematics;

namespace Hullbreach.World
{
    // Plain C# (no UnityEngine) so it is shared by edit-mode tests, the
    // plain-C# ShipBody integration, and a future headless server.
    // frob:doc docs/reference/hullbreach-world.md#gravityfield
    public sealed class GravityField
    {
        // frob:doc docs/reference/hullbreach-world.md#gravityfield
        public float MaxAcceleration = 40f;

        // One temporary body plus the seconds remaining before it expires.
        struct TemporaryEntry
        {
            public GravityBody Body;
            public float SecondsRemaining;
        }

        readonly List<GravityBody> _permanent = new List<GravityBody>();
        readonly List<TemporaryEntry> _temporary = new List<TemporaryEntry>();

        // frob:doc docs/reference/hullbreach-world.md#gravityfield
        public int Count => _permanent.Count + _temporary.Count;

        // frob:doc docs/reference/hullbreach-world.md#gravityfield
        public void Add(GravityBody body) => _permanent.Add(body);

        // frob:doc docs/reference/hullbreach-world.md#gravityfield
        public void Remove(int index)
        {
            if (index < 0 || index >= _permanent.Count) return;
            _permanent.RemoveAt(index);
        }

        // frob:doc docs/reference/hullbreach-world.md#gravityfield
        public bool TryGetPermanent(int index, out GravityBody body)
        {
            if (index < 0 || index >= _permanent.Count) { body = default; return false; }
            body = _permanent[index];
            return true;
        }

        // frob:doc docs/reference/hullbreach-world.md#gravityfield
        public void Clear()
        {
            _permanent.Clear();
            _temporary.Clear();
        }

        // frob:doc docs/reference/hullbreach-world.md#gravityfield
        public void AddTemporary(GravityBody body, float seconds)
        {
            _temporary.Add(new TemporaryEntry { Body = body, SecondsRemaining = seconds });
        }

        // frob:doc docs/reference/hullbreach-world.md#gravityfield
        public void Tick(float dt)
        {
            for (int i = _temporary.Count - 1; i >= 0; i--)
            {
                var entry = _temporary[i];
                entry.SecondsRemaining -= dt;
                if (entry.SecondsRemaining <= 0f)
                {
                    _temporary.RemoveAt(i);
                }
                else
                {
                    _temporary[i] = entry;
                }
            }
        }

        // frob:doc docs/reference/hullbreach-world.md#gravityfield
        public float2 AccelerationAt(float2 worldPoint)
        {
            float2 total = float2.zero;
            for (int i = 0; i < _permanent.Count; i++)
            {
                total += AccelerationFrom(_permanent[i], worldPoint);
            }
            for (int i = 0; i < _temporary.Count; i++)
            {
                total += AccelerationFrom(_temporary[i].Body, worldPoint);
            }

            float totalMag = math.length(total);
            if (totalMag > MaxAcceleration && totalMag > 1e-6f)
            {
                total = total / totalMag * MaxAcceleration;
            }
            return total;
        }

        static float2 AccelerationFrom(in GravityBody body, float2 worldPoint)
        {
            float2 r = worldPoint - body.Position;
            float dist = math.length(r);
            if (dist <= 1e-6f)
            {
                // Undefined direction at the exact center; treat as no pull
                // rather than NaN.
                return float2.zero;
            }

            float2 direction = r / dist;
            float magnitude = AccelerationMagnitude(body, dist);
            return -direction * magnitude;
        }

        // frob:doc docs/reference/hullbreach-world.md#gravityfield
        public static float AccelerationMagnitude(in GravityBody body, float dist)
        {
            float softRadius = body.SoftRadius;
            if (dist >= softRadius)
            {
                return body.Mu / (dist * dist);
            }

            float edgeMagnitude = body.Mu / (softRadius * softRadius);
            return edgeMagnitude * (dist / softRadius);
        }

        // frob:doc docs/reference/hullbreach-world.md#gravityfield
        public bool TryContact(float2 worldPoint, float clearance, out int bodyIndex, out float2 normal, out float penetration)
        {
            bodyIndex = -1;
            normal = float2.zero;
            penetration = 0f;
            float bestPenetration = float.NegativeInfinity;

            for (int i = 0; i < _permanent.Count; i++)
            {
                if (TryOne(_permanent[i], worldPoint, clearance, out var n, out var pen) && pen > bestPenetration)
                {
                    bestPenetration = pen;
                    bodyIndex = i;
                    normal = n;
                    penetration = pen;
                }
            }

            return bodyIndex >= 0;
        }

        static bool TryOne(in GravityBody body, float2 worldPoint, float clearance, out float2 normal, out float penetration)
        {
            float2 r = worldPoint - body.Position;
            float dist = math.length(r);
            float shell = body.Radius + clearance;

            if (dist > shell)
            {
                normal = float2.zero;
                penetration = 0f;
                return false;
            }

            normal = dist > 1e-6f ? r / dist : new float2(0f, 1f);
            penetration = shell - dist;
            return true;
        }
    }
}
