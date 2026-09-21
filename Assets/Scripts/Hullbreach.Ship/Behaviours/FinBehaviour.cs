using Unity.Mathematics;
using Hullbreach.Core;

namespace Hullbreach.Ship.Behaviours
{
    /// <summary>
    /// The stock control fin: ramps its own throttle toward the steer
    /// channel target, then pushes perpendicular to its facing with a sign
    /// chosen so the resulting torque about the center of mass matches the
    /// sign of its current ramped throttle: this is what lets torque fade
    /// out smoothly after the steer key is released. Fin variant 0.
    /// </summary>
    public sealed class FinBehaviour : IBlockBehaviour
    {
        /// <summary>Ramps toward the clamped steer axis and, if nonzero,
        /// applies a perpendicular force sized/signed to turn the requested way.</summary>
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
