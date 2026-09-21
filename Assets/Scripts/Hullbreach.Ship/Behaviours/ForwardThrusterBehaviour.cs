using Unity.Mathematics;

namespace Hullbreach.Ship.Behaviours
{
    /// <summary>
    /// The stock forward thruster: ramps toward full throttle while
    /// ThrustAxis &gt; 0 and pushes straight ship-local +y. Thruster variant 0.
    /// </summary>
    public sealed class ForwardThrusterBehaviour : IBlockBehaviour
    {
        /// <summary>Ramps this block's throttle toward the forward channel
        /// target and, if nonzero, pushes ship-local +y at Ship.ThrustPerBlock.</summary>
        public void Step(ref BlockContext ctx)
        {
            float target = ctx.Input.ThrustAxis > 0f ? 1f : 0f;
            float updated = ctx.Throttle(target);
            if (updated == 0f) return;
            ctx.AddForceLocal(new float2(0f, 1f) * updated * ctx.Ship.ThrustPerBlock);
        }
    }
}
