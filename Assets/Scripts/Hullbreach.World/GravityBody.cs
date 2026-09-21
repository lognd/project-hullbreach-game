using Unity.Mathematics;

namespace Hullbreach.World
{
    /// <summary>
    /// One gravitating point mass (a planet, moon, or temporary gravity-gun
    /// well): position, gravitational parameter (G*M, so the field never has
    /// to know G or M separately), a physical radius used both to soften the
    /// acceleration near the center and to test surface contact, and the
    /// restitution a ship should bounce with when it hits the surface.
    /// </summary>
    public struct GravityBody
    {
        /// <summary>Default multiplier of Radius used to derive SoftRadius
        /// when a caller does not specify one explicitly.</summary>
        public const float DefaultSoftRadiusFactor = 1.5f;

        /// <summary>Floor applied to SoftRadius so even a tiny body (e.g. a
        /// gravity-gun well with sub-unit Radius) still softens over a
        /// perceptible distance instead of behaving like a near-singularity.</summary>
        public const float MinSoftRadius = 2f;

        /// <summary>World-space position of the body's center.</summary>
        public float2 Position;

        /// <summary>Gravitational parameter G*M. Acceleration at distance r
        /// (outside SoftRadius) is Mu / r^2 toward Position.</summary>
        public float Mu;

        /// <summary>Physical radius, used by the contact test as the
        /// surface distance.</summary>
        public float Radius;

        /// <summary>Distance below which acceleration is softened: at and
        /// beyond SoftRadius the law is the ordinary Mu / r^2; inside it,
        /// acceleration scales linearly from that value down to zero at the
        /// center (gameplay choice over physical realism, so close
        /// encounters with a planet or a gravity-gun well never blow up).
        /// Defaults to max(Radius * DefaultSoftRadiusFactor, MinSoftRadius).</summary>
        public float SoftRadius;

        /// <summary>Coefficient of restitution (0..1) used by ShipBody's
        /// contact response when a ship lands on this body's surface.</summary>
        public float SurfaceRestitution;

        /// <summary>Builds a gravity body with an explicit SoftRadius; a
        /// convenience over positional struct initializer syntax at call sites.</summary>
        public GravityBody(float2 position, float mu, float radius, float surfaceRestitution, float softRadius)
        {
            Position = position;
            Mu = mu;
            Radius = radius;
            SurfaceRestitution = surfaceRestitution;
            SoftRadius = softRadius;
        }

        /// <summary>Builds a gravity body with SoftRadius derived from
        /// Radius via the default factor/floor (see SoftRadius), for the
        /// common case where a caller has no reason to override it.</summary>
        public GravityBody(float2 position, float mu, float radius, float surfaceRestitution)
            : this(position, mu, radius, surfaceRestitution,
                   math.max(radius * DefaultSoftRadiusFactor, MinSoftRadius))
        {
        }
    }
}
