using System.Collections.Generic;
using Unity.Mathematics;

namespace Hullbreach.World
{
    /// <summary>
    /// A field of gravitating bodies. Plain C# (no UnityEngine) so it can be
    /// shared by the edit-mode tests, the plain-C# ShipBody integration, and
    /// a future headless server exactly like ShipBody itself.
    ///
    /// Bodies are kept in one dense list (permanent bodies added via Add,
    /// temporary ones via AddTemporary) so AccelerationAt and TryContact are
    /// a single allocation-free pass with deterministic (insertion) order,
    /// important both for determinism across machines and so ContactsThisStep
    /// on ShipBody is reproducible.
    /// </summary>
    public sealed class GravityField
    {
        /// <summary>Ceiling on the SUMMED acceleration magnitude returned by
        /// AccelerationAt, applied after every body's contribution is added
        /// so overlapping strong wells cannot stack past a playable pull
        /// (gameplay choice: a ship should never get yanked harder than
        /// this regardless of how many wells overlap it). Units/s^2.</summary>
        public float MaxAcceleration = 40f;


        /// <summary>One temporary body plus the seconds remaining before it expires.</summary>
        struct TemporaryEntry
        {
            public GravityBody Body;
            public float SecondsRemaining;
        }

        readonly List<GravityBody> _permanent = new List<GravityBody>();
        readonly List<TemporaryEntry> _temporary = new List<TemporaryEntry>();

        /// <summary>Number of live bodies (permanent + temporary), for tests
        /// and diagnostics.</summary>
        public int Count => _permanent.Count + _temporary.Count;

        /// <summary>Adds a permanent body (a planet/moon placed in the
        /// scene). Returns its index among permanent bodies at the time of
        /// insertion; note this index is NOT stable across Remove calls.</summary>
        public void Add(GravityBody body) => _permanent.Add(body);

        /// <summary>Removes the permanent body at `index`. No-op if out of range.</summary>
        public void Remove(int index)
        {
            if (index < 0 || index >= _permanent.Count) return;
            _permanent.RemoveAt(index);
        }

        /// <summary>Reads the permanent body at `index` (as returned by
        /// TryContact's bodyIndex), for callers such as OrbitHelper that
        /// need that body's Position/Mu. False if out of range.</summary>
        public bool TryGetPermanent(int index, out GravityBody body)
        {
            if (index < 0 || index >= _permanent.Count) { body = default; return false; }
            body = _permanent[index];
            return true;
        }

        /// <summary>Removes every permanent and temporary body.</summary>
        public void Clear()
        {
            _permanent.Clear();
            _temporary.Clear();
        }

        /// <summary>
        /// Adds a body that expires after `seconds` of Tick calls. Used by
        /// the gravity-gun powerup to drop a short-lived well without the
        /// caller having to track and remove it itself.
        /// </summary>
        public void AddTemporary(GravityBody body, float seconds)
        {
            _temporary.Add(new TemporaryEntry { Body = body, SecondsRemaining = seconds });
        }

        /// <summary>
        /// Advances every temporary body's remaining lifetime by `dt` and
        /// drops any that have expired. Iterates backward so removal during
        /// the pass is safe and allocation-free.
        /// </summary>
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

        /// <summary>
        /// Net gravitational acceleration at `worldPoint`: sum over every
        /// body of its softened pull toward that body (see
        /// GravityBody.SoftRadius and AccelerationMagnitude), then clamped
        /// to MaxAcceleration in magnitude so stacked wells cannot exceed
        /// the same cap a single strong one would. Allocation-free:
        /// iterates the two lists directly.
        /// </summary>
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

        /// <summary>
        /// Softened inverse-square law shared by GravityField and
        /// OrbitHelper: Mu / r^2 at and beyond SoftRadius; inside it, scales
        /// LINEARLY from that same value at SoftRadius down to zero at the
        /// center (dist == 0), so the pull is continuous at SoftRadius and
        /// never diverges. `dist` must be &gt;= 0.
        /// </summary>
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

        /// <summary>
        /// Tests whether `worldPoint` is within `clearance` of any body's
        /// surface (i.e. distance to center &lt;= Radius + clearance).
        /// Returns the nearest such body (by penetration depth), its outward
        /// surface normal at that point, and how far inside the clearance
        /// shell the point is (Radius + clearance - distance; positive means
        /// penetrating). False (with default outs) when no body is in range.
        /// </summary>
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
