using Unity.Mathematics;

namespace Hullbreach.Ship.Behaviours
{
    /// <summary>
    /// The stock retro thruster: ramps toward full throttle while
    /// ThrustAxis &lt; 0 and pushes ship-local -y. RetroThruster variant 0.
    /// </summary>
    public sealed class RetroThrusterBehaviour : IBlockBehaviour
    {
        /// <summary>Ramps this block's throttle toward the reverse channel
        /// target and, if nonzero, pushes ship-local -y at Ship.RetroThrustPerBlock.</summary>
        public void Step(ref BlockContext ctx)
        {
            float target = ctx.Input.ThrustAxis < 0f ? 1f : 0f;
            float updated = ctx.Throttle(target);
            if (updated == 0f) return;
            ctx.AddForceLocal(new float2(0f, -1f) * updated * ctx.Ship.RetroThrustPerBlock);
        }
    }
}
