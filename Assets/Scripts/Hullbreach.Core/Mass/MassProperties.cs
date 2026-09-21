using System;
using Unity.Mathematics;

namespace Hullbreach.Core
{
    /// <summary>
    /// Running mass, center of mass and rotational inertia, maintained in O(1)
    /// as blocks come and go. No traversal, ever.
    ///
    /// Deliberately NOT delegated to the physics engine. Rigidbody2D's
    /// useAutoMass recomputes from collider geometry on every change, which is
    /// slower, gives no control over the value the netcode must agree on, and,
    /// worth knowing, com.unity.physics is a 3D package that cannot help
    /// a 2D game at all. Unity's 2D physics is Box2D behind Rigidbody2D.
    ///
    /// The accumulators are kept about the ORIGIN and shifted to the center of
    /// mass on read, via the parallel axis theorem. That is precisely what
    /// makes removal O(1): you cannot incrementally maintain a quantity
    /// measured about a center that itself moves when you edit.
    /// </summary>
    public struct MassProperties
    {
        /// <summary>Total mass. Sum of m_i.</summary>
        public float Total;

        /// <summary>First moment about the origin. Sum of m_i * p_i.</summary>
        public float2 FirstMoment;

        /// <summary>Second moment about the origin.
        /// Sum of (I_local_i + m_i * |p_i|^2).</summary>
        public float SecondMomentAboutOrigin;

        /// <summary>Center of mass, FirstMoment / Total. Zero (not NaN) for an
        /// empty grid, since dividing by zero mass is meaningless.</summary>
        public float2 CenterOfMass
            => Total == 0f ? float2.zero : FirstMoment / Total;

        /// <summary>Rotational inertia about the center of mass.
        /// Parallel axis theorem: I_com = I_origin - M * |com|^2.</summary>
        public float InertiaAboutCenterOfMass
        {
            get
            {
                if (Total == 0f) return 0f;
                var com = CenterOfMass;
                return SecondMomentAboutOrigin - Total * math.lengthsq(com);
            }
        }

        /// <summary>
        /// Inertia of a solid rectangle about its own center:
        /// I = m * (w^2 + h^2) / 12. For a unit square of mass m that is m/6.
        /// </summary>
        public static float RectangleInertia(float mass, float width, float height)
            => mass * (width * width + height * height) / 12f;

        /// <summary>Accumulate one block. `center` is ship-local; `localInertia`
        /// is that block's inertia about its OWN center. Parallel axis theorem
        /// folds the block's own inertia plus its offset into the origin-frame
        /// second moment, all in O(1).</summary>
        public void Add(float mass, float2 center, float localInertia)
        {
            Total += mass;
            FirstMoment += mass * center;
            SecondMomentAboutOrigin += localInertia + mass * math.lengthsq(center);
        }

        /// <summary>Exact inverse of Add. Removal being O(1) is the entire
        /// reason the accumulators are kept about the origin.</summary>
        public void Remove(float mass, float2 center, float localInertia)
        {
            Total -= mass;
            FirstMoment -= mass * center;
            SecondMomentAboutOrigin -= localInertia + mass * math.lengthsq(center);
        }
    }
}
