namespace Hullbreach.Ship
{
    /// <summary>
    /// Immutable description of the projectile a cannon fires. Plain data so
    /// the demo-scene branch can spawn whatever visual/physics object it
    /// wants from a ShotRequest without ShipBody knowing about prefabs.
    /// </summary>
    public readonly struct ProjectileSpec
    {
        /// <summary>Muzzle velocity, world units/second.</summary>
        public readonly float Speed;

        /// <summary>Recoil impulse magnitude applied to the firing ship,
        /// opposite the shot direction.</summary>
        public readonly float Impulse;

        /// <summary>Damage dealt on hit, saturating a target block's Damage byte.</summary>
        public readonly byte Damage;

        /// <summary>Seconds the projectile survives before despawning.</summary>
        public readonly float LifetimeSeconds;

        /// <summary>Collision radius for hit testing.</summary>
        public readonly float Radius;

        public ProjectileSpec(float speed, float impulse, byte damage,
                               float lifetimeSeconds, float radius)
        {
            Speed = speed;
            Impulse = impulse;
            Damage = damage;
            LifetimeSeconds = lifetimeSeconds;
            Radius = radius;
        }

        /// <summary>Baseline cannon round; a reasonable default until the
        /// balance pass picks real numbers.</summary>
        public static ProjectileSpec Default
            => new ProjectileSpec(speed: 20f, impulse: 2f, damage: 25,
                                   lifetimeSeconds: 3f, radius: 0.1f);
    }
}
