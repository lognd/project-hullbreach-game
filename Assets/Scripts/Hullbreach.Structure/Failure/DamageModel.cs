using System;

namespace Hullbreach.Structure
{
    // Damage accumulation as cheap pseudo-plasticity; see
    // docs/reference/hullbreach-structure.md#damagemodel for why.
    // frob:doc docs/reference/hullbreach-structure.md#damagemodel
    public static class DamageModel
    {
        // Break above this; do not un-break until RecoverRatio
        // (hysteresis, so blocks do not chatter at the threshold).
        // frob:doc docs/reference/hullbreach-structure.md#damagemodel
        public const float FailRatio = 1.0f;
        // frob:doc docs/reference/hullbreach-structure.md#damagemodel
        public const float RecoverRatio = 0.9f;

        // Damage-fraction per (ratio-overshoot * second).
        const float AccumulationRate = 1.0f;

        // Below yield (ratio <= 1) damage does not change: this models
        // plastic accumulation, not elastic loading/unloading fatigue.
        // frob:doc docs/reference/hullbreach-structure.md#damagemodel
        public static byte Accumulate(byte currentDamage, float ductileRatio, float dt)
        {
            if (ductileRatio <= FailRatio) return currentDamage;

            float damage = currentDamage / 255f;
            damage += (ductileRatio - FailRatio) * AccumulationRate * dt;
            damage = Math.Min(1f, Math.Max(0f, damage));
            return (byte)Math.Round(damage * 255f);
        }

        // Floored at 0.05 so a block never reaches exactly zero stiffness
        // (which would make K singular).
        // frob:doc docs/reference/hullbreach-structure.md#damagemodel
        public static float SofteningFactor(float damage)
        {
            const float floor = 0.05f;
            return Math.Max(floor, 1f - damage);
        }

        // Ductile failure hystereses via RecoverRatio; brittle failure is
        // instantaneous (a crack does not partially open).
        // frob:doc docs/reference/hullbreach-structure.md#damagemodel
        public static bool ShouldDetach(float ductileRatio, float brittleRatio, bool alreadyFailing)
        {
            if (brittleRatio >= 1f) return true;

            if (alreadyFailing)
                return ductileRatio >= RecoverRatio;

            return ductileRatio >= FailRatio;
        }
    }
}
