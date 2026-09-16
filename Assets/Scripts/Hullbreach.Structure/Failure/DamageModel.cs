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

        // TODO [D3]: Accumulate damage proportional to the overshoot above
        //            yield, clamped to 0..1. Returns the new damage byte.
        public static byte Accumulate(byte currentDamage, float ductileRatio, float dt)
            => throw new NotImplementedException();

        // TODO [D3]: Stiffness multiplier from damage. Must reach a small but
        //            NONZERO floor -- a block with exactly zero stiffness makes
        //            K singular in a way inertia relief does not account for.
        public static float SofteningFactor(float damage)
            => throw new NotImplementedException();

        // TODO [D4]: Should this block detach this tick? Apply the hysteresis
        //            band above, not a bare threshold compare.
        public static bool ShouldDetach(float ductileRatio, float brittleRatio, bool alreadyFailing)
            => throw new NotImplementedException();
    }
}
