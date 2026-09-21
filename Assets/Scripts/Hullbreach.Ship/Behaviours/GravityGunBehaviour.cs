namespace Hullbreach.Ship.Behaviours
{
    /// <summary>
    /// Cannon variant 1: fires a shot flagged GravityWell with a positive-Mu
    /// WellSpec, so on impact the game drops a short-lived ATTRACTING well at
    /// the hit point (via IWorldSink.AddTemporaryGravity).
    /// </summary>
    public sealed class GravityGunBehaviour : CannonBehaviourBase
    {
        /// <summary>Gravitational parameter of the dropped well.</summary>
        public float WellMu = 40f;

        /// <summary>Physical radius of the dropped well.</summary>
        public float WellRadius = 1.5f;

        /// <summary>Seconds before the dropped well expires.</summary>
        public float WellSeconds = 6f;

        /// <summary>The ship's own Projectile spec, tagged as an attracting
        /// gravity well.</summary>
        protected override ProjectileSpec BuildSpec(in BlockContext ctx)
            => ctx.Ship.Projectile.WithGravityWell(new WellSpec(WellMu, WellRadius, WellSeconds));
    }
}
