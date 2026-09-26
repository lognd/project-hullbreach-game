using System;
using Unity.Mathematics;

namespace Hullbreach.Core
{
    // Running mass/COM/inertia, maintained in O(1); see
    // docs/reference/hullbreach-core.md#massproperties for why.
    // frob:doc docs/reference/hullbreach-core.md#massproperties
    public struct MassProperties
    {
        // frob:doc docs/reference/hullbreach-core.md#massproperties
        public float Total;

        // frob:doc docs/reference/hullbreach-core.md#massproperties
        public float2 FirstMoment;

        // frob:doc docs/reference/hullbreach-core.md#massproperties
        public float SecondMomentAboutOrigin;

        // Zero (not NaN) for an empty grid.
        // frob:doc docs/reference/hullbreach-core.md#massproperties
        public float2 CenterOfMass
            => Total == 0f ? float2.zero : FirstMoment / Total;

        // Parallel axis theorem: I_com = I_origin - M * |com|^2.
        // frob:doc docs/reference/hullbreach-core.md#massproperties
        public float InertiaAboutCenterOfMass
        {
            get
            {
                if (Total == 0f) return 0f;
                var com = CenterOfMass;
                return SecondMomentAboutOrigin - Total * math.lengthsq(com);
            }
        }

        // I = m * (w^2 + h^2) / 12.
        // frob:doc docs/reference/hullbreach-core.md#massproperties
        public static float RectangleInertia(float mass, float width, float height)
            => mass * (width * width + height * height) / 12f;

        // `center` is ship-local; `localInertia` is about the block's own center.
        // frob:doc docs/reference/hullbreach-core.md#massproperties
        public void Add(float mass, float2 center, float localInertia)
        {
            Total += mass;
            FirstMoment += mass * center;
            SecondMomentAboutOrigin += localInertia + mass * math.lengthsq(center);
        }

        // Exact inverse of Add.
        // frob:doc docs/reference/hullbreach-core.md#massproperties
        public void Remove(float mass, float2 center, float localInertia)
        {
            Total -= mass;
            FirstMoment -= mass * center;
            SecondMomentAboutOrigin -= localInertia + mass * math.lengthsq(center);
        }
    }
}
