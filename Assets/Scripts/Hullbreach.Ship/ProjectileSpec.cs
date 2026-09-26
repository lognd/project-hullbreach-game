namespace Hullbreach.Ship
{
    // What special payload a projectile carries beyond plain damage. None is
    // the stock cannon round; GravityWell marks a shot whose impact should
    // drop a temporary gravity well (positive Mu) or anti-well (negative Mu)
    // via IWorldSink.AddTemporaryGravity, per its WellSpec.
    // frob:doc docs/reference/hullbreach-ship.md#projectilekind
    public enum ProjectileKind : byte
    {
        // frob:doc docs/reference/hullbreach-ship.md#projectilekind
        None = 0,

        // frob:doc docs/reference/hullbreach-ship.md#projectilekind
        GravityWell = 1,
    }

    // Parameters for the temporary gravity well a GravityWell-kind
    // projectile drops on impact. Meaningless (and ignored) for Kind == None.
    // frob:doc docs/reference/hullbreach-ship.md#wellspec
    public readonly struct WellSpec
    {
        // Gravitational parameter G*M of the dropped well. Negative values
        // repel instead of attract (the anti-gravity gun).
        // frob:doc docs/reference/hullbreach-ship.md#wellspec
        public readonly float Mu;

        // Physical radius of the dropped well, same meaning as GravityBody.Radius.
        // frob:doc docs/reference/hullbreach-ship.md#wellspec
        public readonly float Radius;

        // Seconds before the dropped well expires.
        // frob:doc docs/reference/hullbreach-ship.md#wellspec
        public readonly float Seconds;

        // frob:doc docs/reference/hullbreach-ship.md#wellspec
        public WellSpec(float mu, float radius, float seconds)
        {
            Mu = mu;
            Radius = radius;
            Seconds = seconds;
        }

        // The default, inert well spec (zero pull, zero lifetime) used by
        // every projectile that is not a gravity well.
        // frob:doc docs/reference/hullbreach-ship.md#wellspec
        public static readonly WellSpec None = new WellSpec(0f, 0f, 0f);
    }

    // Immutable description of the projectile a cannon fires. Plain data so
    // the demo-scene branch can spawn whatever visual/physics object it
    // wants from a ShotRequest without ShipBody knowing about prefabs.
    // frob:doc docs/reference/hullbreach-ship.md#projectilespec
    public readonly struct ProjectileSpec
    {
        // Muzzle velocity, world units/second.
        // frob:doc docs/reference/hullbreach-ship.md#projectilespec
        public readonly float Speed;

        // Recoil impulse magnitude applied to the firing ship, opposite the
        // shot direction.
        // frob:doc docs/reference/hullbreach-ship.md#projectilespec
        public readonly float Impulse;

        // Damage dealt on hit, saturating a target block's Damage byte.
        // frob:doc docs/reference/hullbreach-ship.md#projectilespec
        public readonly byte Damage;

        // Seconds the projectile survives before despawning.
        // frob:doc docs/reference/hullbreach-ship.md#projectilespec
        public readonly float LifetimeSeconds;

        // Collision radius for hit testing.
        // frob:doc docs/reference/hullbreach-ship.md#projectilespec
        public readonly float Radius;

        // Special payload this projectile carries. Defaults to None for
        // every stock cannon round.
        // frob:doc docs/reference/hullbreach-ship.md#projectilespec
        public readonly ProjectileKind Kind;

        // Gravity well parameters; meaningful only when Kind == GravityWell.
        // frob:doc docs/reference/hullbreach-ship.md#projectilespec
        public readonly WellSpec Well;

        // frob:doc docs/reference/hullbreach-ship.md#projectilespec
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

        // Baseline cannon round; a reasonable default until the balance
        // pass picks real numbers.
        // frob:doc docs/reference/hullbreach-ship.md#projectilespec
        public static ProjectileSpec Default
            => new ProjectileSpec(speed: 20f, impulse: 2f, damage: 25,
                                   lifetimeSeconds: 3f, radius: 0.1f);

        // Used by the gravity-gun/anti-gravity-gun variants to reuse the
        // ship's own ballistic numbers (speed, damage, lifetime, radius)
        // while swapping only the impact payload.
        // frob:doc docs/reference/hullbreach-ship.md#projectilespec
        public ProjectileSpec WithGravityWell(WellSpec well)
            => new ProjectileSpec(Speed, Impulse, Damage, LifetimeSeconds, Radius,
                                   ProjectileKind.GravityWell, well);
    }
}
