using System;

namespace Hullbreach.Structure
{
    /// <summary>
    /// Reduces a 2D stress tensor to the scalars the failure model compares
    /// against. Both invariants are about ten flops from the same tensor, so
    /// computing both is effectively free.
    ///
    /// VON MISES is sqrt(3 J2), built from the DEVIATORIC (shape-changing) part
    /// of stress only. It deliberately ignores hydrostatic pressure -- you
    /// cannot yield metal by squeezing it uniformly from every side. That
    /// pressure-insensitivity is exactly what makes it the DUCTILE criterion.
    ///
    /// MAX PRINCIPAL (Rankine) is the BRITTLE criterion: cracks open
    /// perpendicular to maximum tension, and pressure very much does matter,
    /// which is why brittle solids are hugely stronger in compression.
    ///
    /// The ductile/brittle split is physically real, not a gameplay hack:
    /// materials embrittle at high strain rate. Slow structural loading yields;
    /// a projectile impact spalls.
    /// </summary>
    public static class StressCriteria
    {
        /// <summary>Von Mises equivalent stress, plane stress.</summary>
        public static float VonMises(float sxx, float syy, float txy)
        {
            float v2 = sxx * sxx - sxx * syy + syy * syy + 3f * txy * txy;
            return (float)Math.Sqrt(Math.Max(0.0, v2));
        }

        /// <summary>
        /// Principal stresses. Returns the larger in `major`, the smaller in
        /// `minor`.
        /// </summary>
        public static void Principal(float sxx, float syy, float txy,
                                     out float major, out float minor)
        {
            float avg = (sxx + syy) / 2f;
            float diff = (sxx - syy) / 2f;
            float radius = (float)Math.Sqrt(diff * diff + txy * txy);
            major = avg + radius;
            minor = avg - radius;
        }

        /// <summary>
        /// Ductile utilization: von Mises over the (damage-reduced) yield
        /// stress. Drives the S37 green-to-red tint. 1.0 means failing.
        /// The denominator shrinks with damage (floored via DamageModel's
        /// softening curve) so a damaged block is STRUCTURALLY weaker --
        /// sustained fire then eventually causes a structural failure rather
        /// than only an HP kill.
        /// </summary>
        public static float DuctileRatio(float vonMises, float yieldStress, float damage)
        {
            float effectiveYield = yieldStress * (1f - 0.5f * damage);
            effectiveYield = Math.Max(effectiveYield, 0.05f * yieldStress);
            return vonMises / effectiveYield;
        }

        /// <summary>
        /// Brittle utilization from the impulsive load case. Uses max TENSILE
        /// principal stress against spall strength, and the (much larger)
        /// compressive limit separately -- that asymmetry is most of what makes
        /// armor feel like armor.
        /// </summary>
        public static float BrittleRatio(float major, float minor,
                                         float spallStress, float compressiveStress)
        {
            float tensileRatio = Math.Max(major, 0f) / spallStress;
            float compressiveRatio = Math.Max(-minor, 0f) / compressiveStress;
            return Math.Max(tensileRatio, compressiveRatio);
        }
    }
}
