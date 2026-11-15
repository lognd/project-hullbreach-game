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

        // Damage-fraction per (ratio-overshoot * second). Was 1.0 while a
        // block still detached the instant its ratio crossed FailRatio, so
        // the rate was cosmetic; now it IS the time-to-fail (see
        // SecondsToFail): ratio 2.6 breaks in about 1.2 s, ratio 1.3 in about 3 s.
        // frob:doc docs/reference/hullbreach-structure.md#damagemodel
        public const float AccumulationRate = 0.35f;

        // A brittle (tensile/spall) overload this large still snaps at once:
        // a crack does not partially open. Anything between FailRatio and
        // this goes through damage accumulation like a ductile overload.
        // frob:doc docs/reference/hullbreach-structure.md#damagemodel
        public const float BrittleSnapRatio = 3.0f;

        // At or above this (and below FailRatio) a block is "creaking": the
        // HUD / build preview warn before anything is damaged.
        // frob:doc docs/reference/hullbreach-structure.md#damagemodel
        public const float CreakRatio = 0.8f;

        // The ratio that drives damage: the worse of the two criteria.
        // frob:doc docs/reference/hullbreach-structure.md#damagemodel
        public static float DamageRatio(float ductileRatio, float brittleRatio)
            => Math.Max(ductileRatio, brittleRatio);

        // Below yield (ratio <= 1) damage does not change: this models
        // plastic accumulation, not elastic loading/unloading fatigue.
        // frob:doc docs/reference/hullbreach-structure.md#damagemodel
        public static byte Accumulate(byte currentDamage, float ductileRatio, float dt)
        {
            if (ductileRatio <= FailRatio) return currentDamage;

            // Damage is a byte, so a tiny overshoot per 20 ms tick would round
            // to nothing and a mildly overloaded block would never fail: every
            // overstressed tick adds at least one step (255 ticks, about 5 s, worst case).
            float step = (ductileRatio - FailRatio) * AccumulationRate * dt * 255f;
            int add = Math.Max(1, (int)Math.Ceiling(step));
            return (byte)Math.Min(255, currentDamage + add);
        }

        // Seconds a block holds at a constant ratio before it fails, with the
        // softening feedback (yield falls as damage rises) included. A
        // preview / HUD number, not used by the simulation itself.
        // frob:doc docs/reference/hullbreach-structure.md#damagemodel
        public static float SecondsToFail(float ductileRatio, float dt = 0.02f)
        {
            if (ductileRatio <= FailRatio) return float.PositiveInfinity;
            byte d = 0;
            float t = 0f;
            while (d < 255 && t < 600f)
            {
                float r = ductileRatio / (1f - 0.5f * (d / 255f));
                d = Accumulate(d, r, dt);
                t += dt;
            }
            return t;
        }

        // The simulation's rule: a block detaches when it is overstressed AND
        // its accumulated damage has saturated (yield is a process, not a
        // threshold), or at once on a brittle overload past BrittleSnapRatio.
        // The ductile hysteresis band still applies to "overstressed".
        // frob:doc docs/reference/hullbreach-structure.md#damagemodel
        public static bool ShouldDetach(float ductileRatio, float brittleRatio, bool alreadyFailing, byte damage)
        {
            if (brittleRatio >= BrittleSnapRatio) return true;
            if (damage < 255) return false;
            float r = DamageRatio(ductileRatio, brittleRatio);
            return alreadyFailing ? r >= RecoverRatio : r >= FailRatio;
        }

        // Delegates to BlockTypes.SofteningFactor, the single source of the
        // 0.05 floor, so DamageModel and EffectiveStiffness cannot drift.
        // frob:doc docs/reference/hullbreach-structure.md#damagemodel
        public static float SofteningFactor(float damage)
            => Hullbreach.Core.BlockTypes.SofteningFactor(damage);

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
