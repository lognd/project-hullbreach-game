using Unity.Mathematics;

namespace Hullbreach.Ship.Behaviours
{
    // The stock forward thruster: ramps toward full throttle while
    // ThrustAxis > 0 and pushes straight ship-local +y. Thruster variant 0.
    // frob:doc docs/reference/hullbreach-ship.md#forwardthrusterbehaviour
    public sealed class ForwardThrusterBehaviour : IBlockBehaviour
    {
        // frob:doc docs/reference/hullbreach-ship.md#forwardthrusterbehaviour
        public void Step(ref BlockContext ctx)
        {
            float target = ctx.Input.ThrustAxis > 0f ? 1f : 0f;
            float updated = ctx.Throttle(target);
            if (updated == 0f) return;
            ctx.AddForceLocal(new float2(0f, 1f) * updated * ctx.Ship.ThrustPerBlock);
        }
    }
}
