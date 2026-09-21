namespace Hullbreach.Ship
{
    /// <summary>
    /// What special payload a projectile carries beyond plain damage. None is
    /// the stock cannon round; GravityWell marks a shot whose impact should
    /// drop a temporary gravity well (positive Mu) or anti-well (negative Mu)
    /// via IWorldSink.AddTemporaryGravity, per its WellSpec.
    /// </summary>
    public enum ProjectileKind : byte
    {
        /// <summary>Plain cannon round: deals damage, nothing else.</summary>
        None = 0,

        /// <summary>Drops a temporary gravity well/anti-well on impact.</summary>
        GravityWell = 1,
    }

    /// <summary>
    /// Parameters for the temporary gravity well a GravityWell-kind
    /// projectile drops on impact. Meaningless (and ignored) for Kind == None.
    /// </summary>
    public readonly struct WellSpec
    {
        /// <summary>Gravitational parameter G*M of the dropped well. Negative
        /// values repel instead of attract (the anti-gravity gun).</summary>
        public readonly float Mu;

        /// <summary>Physical radius of the dropped well, same meaning as
        /// GravityBody.Radius.</summary>
        public readonly float Radius;

        /// <summary>Seconds before the dropped well expires.</summary>
        public readonly float Seconds;

        public WellSpec(float mu, float radius, float seconds)
        {
            Mu = mu;
            Radius = radius;
            Seconds = seconds;
        }

        /// <summary>The default, inert well spec (zero pull, zero lifetime)
        /// used by every projectile that is not a gravity well.</summary>
        public static readonly WellSpec None = new WellSpec(0f, 0f, 0f);
    }

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

        /// <summary>Special payload this projectile carries. Defaults to
        /// None for every stock cannon round.</summary>
        public readonly ProjectileKind Kind;

        /// <summary>Gravity well parameters; meaningful only when
        /// Kind == GravityWell.</summary>
        public readonly WellSpec Well;

        public ProjectileSpec(float speed, float impulse, byte damage,
                               float lifetimeSeconds, float radius,
                               ProjectileKind kind = ProjectileKind.None,
                               WellSpec well = default)
        {
            Speed = speed;
            Impulse = impulse;
            Damage = damage;
            LifetimeSeconds = lifetimeSeconds;
            Radius = radius;
            Kind = kind;
            Well = well;
        }

        /// <summary>Baseline cannon round; a reasonable default until the
        /// balance pass picks real numbers.</summary>
        public static ProjectileSpec Default
            => new ProjectileSpec(speed: 20f, impulse: 2f, damage: 25,
                                   lifetimeSeconds: 3f, radius: 0.1f);

        /// <summary>Returns a copy of this spec with Kind = GravityWell and
        /// the given well parameters attached, everything else unchanged --
        /// used by the gravity-gun/anti-gravity-gun variants to reuse the
        /// ship's own ballistic numbers (speed, damage, lifetime, radius)
        /// while swapping only the impact payload.</summary>
        public ProjectileSpec WithGravityWell(WellSpec well)
            => new ProjectileSpec(Speed, Impulse, Damage, LifetimeSeconds, Radius,
                                   ProjectileKind.GravityWell, well);
    }
}
