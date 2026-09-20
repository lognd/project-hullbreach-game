using System;

namespace Hullbreach.Structure
{
    /// <summary>
    /// Damage accumulation as cheap pseudo-plasticity.
    ///
    /// "Ductile" means YIELD, not instant fracture. If blocks snap the moment
    /// von Mises crosses yield, it reads brittle no matter what the criterion
    /// is called. Real plasticity (return mapping, history variables, a
    /// nonlinear solve) is far too much machinery for this.
    ///
    /// The substitute: on overshoot, accumulate damage and SOFTEN the block --
    /// reduce its E and its yield stress. A softened block sheds load to its
    /// neighbors, which is what yielding actually does. You get visible
    /// bending before breaking, load redistribution for free, and the solve
    /// stays linear. It also reuses the E multiplier the upgrades already need.
    /// </summary>
    public static class DamageModel
    {
        /// <summary>Break above this, but do not un-break until
        /// <see cref="RecoverRatio"/> -- without hysteresis, blocks chatter in
        /// and out of existence at the threshold.</summary>
        public const float FailRatio = 1.0f;
        public const float RecoverRatio = 0.9f;

        /// <summary>Rate at which overshoot above yield accumulates damage,
        /// in damage-fraction per (ratio-overshoot * second).</summary>
        const float AccumulationRate = 1.0f;

        /// <summary>
        /// Accumulates damage proportional to the overshoot above yield,
        /// clamped to 0..1 (represented as a byte 0..255). Below yield
        /// (ratio &lt;= 1) damage does not change -- this models plastic
        /// accumulation, not elastic loading/unloading fatigue.
        /// </summary>
        public static byte Accumulate(byte currentDamage, float ductileRatio, float dt)
        {
            if (ductileRatio <= FailRatio) return currentDamage;

            float damage = currentDamage / 255f;
            damage += (ductileRatio - FailRatio) * AccumulationRate * dt;
            damage = Math.Min(1f, Math.Max(0f, damage));
            return (byte)Math.Round(damage * 255f);
        }

        /// <summary>Stiffness multiplier from damage. Floored at 0.05 so a
        /// block never reaches exactly zero stiffness, which would make K
        /// singular in a way inertia relief does not account for.</summary>
        public static float SofteningFactor(float damage)
        {
            const float floor = 0.05f;
            return Math.Max(floor, 1f - damage);
        }

        /// <summary>
        /// Should this block detach this tick? Ductile failure uses a
        /// hysteresis band: once failing, it must drop below RecoverRatio to
        /// stop, so blocks do not chatter in and out of existence right at
        /// the threshold. Brittle failure is instantaneous (no hysteresis --
        /// a crack does not partially open).
        /// </summary>
        public static bool ShouldDetach(float ductileRatio, float brittleRatio, bool alreadyFailing)
        {
            if (brittleRatio >= 1f) return true;

            if (alreadyFailing)
                return ductileRatio >= RecoverRatio;

            return ductileRatio >= FailRatio;
        }
    }
}
