using System;

namespace Hullbreach.Structure
{
    // Reduces a 2D stress tensor to the ductile (von Mises) and brittle
    // (max principal) scalars; see docs/reference/hullbreach-structure.md#stresscriteria.
    // frob:doc docs/reference/hullbreach-structure.md#stresscriteria
    public static class StressCriteria
    {
        // frob:doc docs/reference/hullbreach-structure.md#stresscriteria
        public static float VonMises(float sxx, float syy, float txy)
        {
            float v2 = sxx * sxx - sxx * syy + syy * syy + 3f * txy * txy;
            return (float)Math.Sqrt(Math.Max(0.0, v2));
        }

        // Returns the larger in `major`, the smaller in `minor`.
        // frob:doc docs/reference/hullbreach-structure.md#stresscriteria
        public static void Principal(float sxx, float syy, float txy,
                                     out float major, out float minor)
        {
            float avg = (sxx + syy) / 2f;
            float diff = (sxx - syy) / 2f;
            float radius = (float)Math.Sqrt(diff * diff + txy * txy);
            major = avg + radius;
            minor = avg - radius;
        }

        // Drives the S37 green-to-red tint (1.0 = failing); denominator
        // shrinks with damage so a damaged block is STRUCTURALLY weaker.
        // frob:doc docs/reference/hullbreach-structure.md#stresscriteria
        public static float DuctileRatio(float vonMises, float yieldStress, float damage)
        {
            float effectiveYield = yieldStress * (1f - 0.5f * damage);
            effectiveYield = Math.Max(effectiveYield, 0.05f * yieldStress);
            return vonMises / effectiveYield;
        }

        // Max TENSILE stress against spall strength, and the (much
        // larger) compressive limit separately: armor's asymmetry.
        // frob:doc docs/reference/hullbreach-structure.md#stresscriteria
        public static float BrittleRatio(float major, float minor,
                                         float spallStress, float compressiveStress)
        {
            float tensileRatio = Math.Max(major, 0f) / spallStress;
            float compressiveRatio = Math.Max(-minor, 0f) / compressiveStress;
            return Math.Max(tensileRatio, compressiveRatio);
        }
    }
}
