using Unity.Mathematics;

namespace Hullbreach.World
{
    // Every tunable of planet crashes, the thin atmosphere band and the impact warning, in one value.
    // Speeds are world units per second relative to the planet surface; damage is block damage
    // points (a block is destroyed at 255, and a core at 255 loses the match).
    // frob:doc docs/reference/hullbreach-world.md#planetcrashconfig
    public struct PlanetCrashConfig
    {
        // Closing speed at or below which a touchdown does nothing at all.
        // frob:doc docs/reference/hullbreach-world.md#planetcrashconfig
        public float SafeLandingSpeed;
        // Closing speed at which a hit on the core side becomes a possible kill.
        // frob:doc docs/reference/hullbreach-world.md#planetcrashconfig
        public float CatastrophicSpeed;
        // Damage per unit of closing speed above SafeLandingSpeed, before it is shared between the touching blocks.
        // frob:doc docs/reference/hullbreach-world.md#planetcrashconfig
        public float DamagePerSpeed;
        // Fraction of a normal block damage that a touching core takes.
        // frob:doc docs/reference/hullbreach-world.md#planetcrashconfig
        public float CoreDamageScale;
        // The most a core can lose to one non-catastrophic impact; below 255 so one graze can never end a match.
        // frob:doc docs/reference/hullbreach-world.md#planetcrashconfig
        public float CoreMaxDamagePerImpact;
        // Closing speeds under this bounce with no restitution, so a resting ship settles instead of buzzing.
        // frob:doc docs/reference/hullbreach-world.md#planetcrashconfig
        public float RestSpeed;
        // Upper bound on a body SurfaceRestitution, so no setting can add energy.
        // frob:doc docs/reference/hullbreach-world.md#planetcrashconfig
        public float MaxRestitution;
        // Longest distance a ship may move between two contact tests; faster ships are substepped.
        // frob:doc docs/reference/hullbreach-world.md#planetcrashconfig
        public float SweepStep;
        // Cap on substeps per tick.
        // frob:doc docs/reference/hullbreach-world.md#planetcrashconfig
        public int MaxSubsteps;

        // Atmosphere depth as a fraction of the planet radius.
        // frob:doc docs/reference/hullbreach-world.md#planetcrashconfig
        public float AtmosphereDepthFactor;
        // Floor on the atmosphere depth, so small moons still get a band.
        // frob:doc docs/reference/hullbreach-world.md#planetcrashconfig
        public float AtmosphereMinDepth;
        // Speed under which the atmosphere is harmless; sits above any stable orbit speed.
        // frob:doc docs/reference/hullbreach-world.md#planetcrashconfig
        public float AtmosphereSafeSpeed;
        // Drag deceleration per unit of speed above AtmosphereSafeSpeed, at surface density.
        // frob:doc docs/reference/hullbreach-world.md#planetcrashconfig
        public float AtmosphereDrag;
        // Speed above which the leading blocks start to heat.
        // frob:doc docs/reference/hullbreach-world.md#planetcrashconfig
        public float HeatSpeed;
        // Damage per second to each leading block at surface density and twice HeatSpeed.
        // frob:doc docs/reference/hullbreach-world.md#planetcrashconfig
        public float HeatDamagePerSecond;
        // Cap on heating damage per second per block.
        // frob:doc docs/reference/hullbreach-world.md#planetcrashconfig
        public float HeatMaxDamagePerSecond;
        // The warning only reports an impact this close in time.
        // frob:doc docs/reference/hullbreach-world.md#planetcrashconfig
        public float WarningSeconds;

        // frob:doc docs/reference/hullbreach-world.md#planetcrashconfig
        public static PlanetCrashConfig Default() => new PlanetCrashConfig
        {
            SafeLandingSpeed = 4f,
            CatastrophicSpeed = 32f,
            DamagePerSpeed = 9f,
            CoreDamageScale = 0.5f,
            CoreMaxDamagePerImpact = 110f,
            RestSpeed = 0.6f,
            MaxRestitution = 0.6f,
            SweepStep = 0.4f,
            MaxSubsteps = 16,
            AtmosphereDepthFactor = 0.5f,
            AtmosphereMinDepth = 4f,
            AtmosphereSafeSpeed = 8f,
            AtmosphereDrag = 0.8f,
            HeatSpeed = 14f,
            HeatDamagePerSecond = 10f,
            HeatMaxDamagePerSecond = 40f,
            WarningSeconds = 6f,
        };

        // Null when valid, else what is wrong (used by the JSON loader).
        // frob:doc docs/reference/hullbreach-world.md#planetcrashconfig
        public string Validate()
        {
            if (!(SafeLandingSpeed >= 0f)) return "safeLandingSpeed must be >= 0";
            if (!(CatastrophicSpeed > SafeLandingSpeed)) return "catastrophicSpeed must be > safeLandingSpeed";
            if (!(DamagePerSpeed >= 0f)) return "damagePerSpeed must be >= 0";
            if (!(CoreDamageScale >= 0f)) return "coreDamageScale must be >= 0";
            if (!(CoreMaxDamagePerImpact >= 0f && CoreMaxDamagePerImpact < 255f)) return "coreMaxDamagePerImpact must be in [0, 255)";
            if (!(RestSpeed >= 0f)) return "restSpeed must be >= 0";
            if (!(MaxRestitution >= 0f && MaxRestitution <= 1f)) return "maxRestitution must be in [0, 1]";
            if (!(SweepStep > 0.05f)) return "sweepStep must be > 0.05";
            if (MaxSubsteps < 1 || MaxSubsteps > 64) return "maxSubsteps must be in [1, 64]";
            if (!(AtmosphereDepthFactor >= 0f)) return "atmosphereDepthFactor must be >= 0";
            if (!(AtmosphereMinDepth >= 0f)) return "atmosphereMinDepth must be >= 0";
            if (!(AtmosphereSafeSpeed >= 0f)) return "atmosphereSafeSpeed must be >= 0";
            if (!(AtmosphereDrag >= 0f)) return "atmosphereDrag must be >= 0";
            if (!(HeatSpeed >= AtmosphereSafeSpeed)) return "heatSpeed must be >= atmosphereSafeSpeed";
            if (!(HeatDamagePerSecond >= 0f)) return "heatDamagePerSecond must be >= 0";
            if (!(HeatMaxDamagePerSecond >= 0f)) return "heatMaxDamagePerSecond must be >= 0";
            if (!(WarningSeconds >= 0f)) return "warningSeconds must be >= 0";
            return null;
        }

        // Depth of the atmosphere band above the surface of a body.
        // frob:doc docs/reference/hullbreach-world.md#planetcrashconfig
        public float AtmosphereDepth(in GravityBody body) => math.max(AtmosphereMinDepth, body.Radius * AtmosphereDepthFactor);

        // 0 at or above the band, 1 at the surface, quadratic in between.
        // frob:doc docs/reference/hullbreach-world.md#planetcrashconfig
        public float AtmosphereDensity(in GravityBody body, float altitude)
        {
            float depth = AtmosphereDepth(body);
            if (depth <= 0f || altitude >= depth) return 0f;
            float t = 1f - math.max(altitude, 0f) / depth;
            return t * t;
        }

        // Damage one block takes from an impact. `closing` is its own closing speed, `touching` the number of
        // blocks sharing the hit. A core is scaled down and capped unless the hit is catastrophic.
        // frob:doc docs/reference/hullbreach-world.md#planetcrashconfig
        public float ImpactDamage(float closing, int touching, bool isCore)
        {
            float excess = closing - SafeLandingSpeed;
            if (excess <= 0f) return 0f;
            float share = 1f / math.sqrt(math.max(1, touching));
            float damage = excess * DamagePerSpeed * share;
            if (isCore)
            {
                if (closing >= CatastrophicSpeed) return math.min(255f, damage);
                damage = math.min(damage * CoreDamageScale, CoreMaxDamagePerImpact);
            }
            return math.min(255f, damage);
        }
    }
}
