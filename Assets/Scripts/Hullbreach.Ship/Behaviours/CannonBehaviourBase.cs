using Unity.Mathematics;

namespace Hullbreach.Ship.Behaviours
{
    // Shared fire-control for every cannon variant. Subclasses only decide
    // WHAT gets fired (BuildSpec), never how firing/cooldown works.
    // frob:doc docs/reference/hullbreach-ship.md#cannonbehaviourbase
    public abstract class CannonBehaviourBase : IBlockBehaviour
    {
        // frob:doc docs/reference/hullbreach-ship.md#cannonbehaviourbase
        protected abstract ProjectileSpec BuildSpec(in BlockContext ctx);

        // frob:doc docs/reference/hullbreach-ship.md#cannonbehaviourbase
        public void Step(ref BlockContext ctx)
        {
            float remaining = ctx.TickCooldown();
            if (!ctx.Input.FirePressed || remaining > 0f) return;

            float2 facing = ctx.Facing;
            float2 muzzleLocal = ctx.LocalCenter + facing * 0.6f;
            float2 worldDirection = ctx.Ship.RotateLocalToWorld(facing);
            float2 worldOrigin = ctx.Ship.LocalToWorld(muzzleLocal);

            var spec = BuildSpec(in ctx);
            ctx.Fire(new ShotRequest(ctx.Key, worldOrigin, worldDirection, spec));

            ctx.Ship.ApplyImpulseAtWorldPoint(ctx.WorldCenter, -worldDirection * spec.Impulse);
            ctx.ResetCooldown(ctx.Ship.CannonCooldown);
        }
    }
}
