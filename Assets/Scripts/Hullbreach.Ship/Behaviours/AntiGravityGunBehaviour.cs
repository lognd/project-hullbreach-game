namespace Hullbreach.Ship.Behaviours
{
    /// <summary>
    /// Cannon variant 2: fires a shot flagged GravityWell with a negative-Mu
    /// WellSpec, so on impact the game drops a short-lived REPULSING well at
    /// the hit point.
    /// </summary>
    public sealed class AntiGravityGunBehaviour : CannonBehaviourBase
    {
        /// <summary>Gravitational parameter of the dropped well (negative:
        /// repels rather than attracts).</summary>
        public float WellMu = -40f;

        /// <summary>Physical radius of the dropped well.</summary>
        public float WellRadius = 1.5f;

        /// <summary>Seconds before the dropped well expires.</summary>
        public float WellSeconds = 6f;

        /// <summary>The ship's own Projectile spec, tagged as a repelling
        /// gravity well.</summary>
        protected override ProjectileSpec BuildSpec(in BlockContext ctx)
            => ctx.Ship.Projectile.WithGravityWell(new WellSpec(WellMu, WellRadius, WellSeconds));
    }
}
