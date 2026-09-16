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
        // TODO [C6]: von Mises, plane stress:
        //   sqrt(sxx^2 - sxx*syy + syy^2 + 3*txy^2)
        public static float VonMises(float sxx, float syy, float txy)
            => throw new NotImplementedException();

        // TODO [C6]: Principal stresses:
        //   s1,s2 = (sxx+syy)/2 +- sqrt(((sxx-syy)/2)^2 + txy^2)
        //   Return the larger in `major`, the smaller in `minor`.
        public static void Principal(float sxx, float syy, float txy,
                                     out float major, out float minor)
            => throw new NotImplementedException();

        /// <summary>
        /// Ductile utilization: von Mises over the (damage-reduced) yield
        /// stress. Drives the S37 green-to-red tint. 1.0 means failing.
        /// </summary>
        // TODO [D4]: Fold damage into the denominator so a damaged block is
        //            STRUCTURALLY weaker -- sustained fire then eventually
        //            causes a structural failure rather than only an HP kill.
        public static float DuctileRatio(float vonMises, float yieldStress, float damage)
            => throw new NotImplementedException();

        /// <summary>
        /// Brittle utilization from the impulsive load case. Uses max TENSILE
        /// principal stress against spall strength, and the (much larger)
        /// compressive limit separately -- that asymmetry is most of what makes
        /// armor feel like armor.
        /// </summary>
        // TODO [D6]
        public static float BrittleRatio(float major, float minor,
                                         float spallStress, float compressiveStress)
            => throw new NotImplementedException();
    }
}
