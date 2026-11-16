using Unity.Mathematics;

namespace Hullbreach.World
{
    // How bad an approaching planet impact would be, for the HUD.
    // frob:doc docs/reference/hullbreach-world.md#planetproximity
    public enum ImpactSeverity : byte
    {
        // No planet closing in, or too far away to matter.
        None = 0,
        // Would touch down without damage.
        Safe = 1,
        // Light damage.
        Caution = 2,
        // Heavy damage.
        Danger = 3,
        // A core hit would be a kill.
        Catastrophic = 4,
    }

    // Read-only picture of the nearest planet for one ship; filled by ShipBody.QueryPlanetProximity.
    // frob:doc docs/reference/hullbreach-world.md#planetproximity
    public struct PlanetProximity
    {
        // False when there is no planet.
        // frob:doc docs/reference/hullbreach-world.md#planetproximity
        public bool HasBody;
        // Index in GravityField permanent list.
        // frob:doc docs/reference/hullbreach-world.md#planetproximity
        public int BodyIndex;
        // Unit vector from the planet centre to the lowest block of the ship.
        // frob:doc docs/reference/hullbreach-world.md#planetproximity
        public float2 SurfaceNormal;
        // Gap between the lowest block and the surface (0 when touching).
        // frob:doc docs/reference/hullbreach-world.md#planetproximity
        public float Altitude;
        // Speed toward the surface; negative while rising.
        // frob:doc docs/reference/hullbreach-world.md#planetproximity
        public float ClosingSpeed;
        // Speed along the surface.
        // frob:doc docs/reference/hullbreach-world.md#planetproximity
        public float TangentialSpeed;
        // Seconds until touchdown, including gravity; +Infinity when not closing.
        // frob:doc docs/reference/hullbreach-world.md#planetproximity
        public float TimeToImpact;
        // Closing speed expected at touchdown.
        // frob:doc docs/reference/hullbreach-world.md#planetproximity
        public float PredictedImpactSpeed;
        // 0..1 air density at the ship.
        // frob:doc docs/reference/hullbreach-world.md#planetproximity
        public float AtmosphereDensity;
        // Distance full forward thrust needs to cancel the closing speed, +Infinity when the thrusters cannot beat gravity.
        // frob:doc docs/reference/hullbreach-world.md#planetproximity
        public float StopDistance;
        // Thruster acceleration minus gravity along the surface normal, in units/s^2 (always filled when HasBody);
        // zero or negative means the thrusters cannot lift the ship off this planet.
        // frob:doc docs/reference/hullbreach-world.md#planetproximity
        public float NetClimbAccel;
        // True when the ship should burn away from the surface now: closing in and the stop distance is nearly the whole gap.
        // frob:doc docs/reference/hullbreach-world.md#planetproximity
        public bool MustBrake;
        // True when the atmosphere is dragging or burning at the current speed.
        // frob:doc docs/reference/hullbreach-world.md#planetproximity
        public bool Heating;
        // How bad the predicted touchdown is; None when it is not imminent.
        // frob:doc docs/reference/hullbreach-world.md#planetproximity
        public ImpactSeverity Severity;
        // False when a warning should be shown: a damaging touchdown is imminent or the ship is heating.
        // frob:doc docs/reference/hullbreach-world.md#planetproximity
        public bool IsSafe;

        // Classifies a predicted closing speed.
        // frob:doc docs/reference/hullbreach-world.md#planetproximity
        public static ImpactSeverity Classify(in PlanetCrashConfig config, float impactSpeed)
        {
            if (impactSpeed <= config.SafeLandingSpeed) return ImpactSeverity.Safe;
            if (impactSpeed >= config.CatastrophicSpeed) return ImpactSeverity.Catastrophic;
            float caution = config.SafeLandingSpeed + (config.CatastrophicSpeed - config.SafeLandingSpeed) * 0.25f;
            return impactSpeed <= caution ? ImpactSeverity.Caution : ImpactSeverity.Danger;
        }

        // Time to cover `gap` while closing at `closing` under constant inward acceleration `gravity`.
        // frob:doc docs/reference/hullbreach-world.md#planetproximity
        public static float TimeToCover(float gap, float closing, float gravity, out float impactSpeed)
        {
            gap = math.max(gap, 0f);
            float disc = closing * closing + 2f * gravity * gap;
            impactSpeed = math.sqrt(math.max(disc, 0f));
            if (gap <= 0f) return 0f;
            if (math.abs(gravity) < 1e-5f) return closing > 1e-5f ? gap / closing : float.PositiveInfinity;
            float t = (-closing + impactSpeed) / gravity;
            return t >= 0f ? t : float.PositiveInfinity;
        }
    }
}
