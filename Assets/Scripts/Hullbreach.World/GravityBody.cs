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
        /// <summary>World-space position of the body's center.</summary>
        public float2 Position;

        /// <summary>Gravitational parameter G*M. Acceleration at distance r
        /// (outside Radius) is Mu / r^2 toward Position.</summary>
        public float Mu;

        /// <summary>Physical radius. Inside this distance the acceleration
        /// is clamped as if evaluated at the surface, and it is the contact
        /// test's surface distance.</summary>
        public float Radius;

        /// <summary>Coefficient of restitution (0..1) used by ShipBody's
        /// contact response when a ship lands on this body's surface.</summary>
        public float SurfaceRestitution;

        /// <summary>Builds a gravity body with all fields specified; a
        /// convenience over positional struct initializer syntax at call sites.</summary>
        public GravityBody(float2 position, float mu, float radius, float surfaceRestitution)
        {
            Position = position;
            Mu = mu;
            Radius = radius;
            SurfaceRestitution = surfaceRestitution;
        }
    }
}
