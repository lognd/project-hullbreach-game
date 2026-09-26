using Unity.Mathematics;
using Hullbreach.Core;

namespace Hullbreach.Ship.Behaviours
{
    // The stock control fin: ramps toward the steer target, then pushes
    // perpendicular to its facing, signed to match the throttle's sign.
    // frob:doc docs/reference/hullbreach-ship.md#finbehaviour
    public sealed class FinBehaviour : IBlockBehaviour
    {
        // frob:doc docs/reference/hullbreach-ship.md#finbehaviour
        public void Step(ref BlockContext ctx)
        {
            float steerTarget = math.clamp(ctx.Input.Steer, -1f, 1f);
            float updated = ctx.Throttle(steerTarget);
            if (updated == 0f) return;

            float2 perp = Facing.Perpendicular(ctx.Block.Modifiers);
            float2 com = ctx.Ship.Grid.Mass.CenterOfMass;
            float2 r = ctx.LocalCenter - com;
            float crossRPerp = r.x * perp.y - r.y * perp.x;

            float2 direction = (crossRPerp >= 0f) == (updated >= 0f) ? perp : -perp;
            float2 force = direction * math.abs(updated) * ctx.Ship.FinForce;
            ctx.AddForceLocal(force);
        }
    }
}
