using Unity.Mathematics;

namespace Hullbreach.Ship.Behaviours
{
    /// <summary>
    /// Shared fire-control for every cannon variant: ticks the per-block
    /// cooldown, and on FirePressed while ready, builds the muzzle
    /// origin/direction, records a ShotRequest with the spec from
    /// <see cref="BuildSpec"/>, applies recoil, and resets the cooldown.
    /// Subclasses only decide WHAT gets fired, never how firing/cooldown
    /// itself works: that is the whole point of factoring this out, since
    /// the gravity gun and anti-gravity gun differ from the stock cannon only
    /// in the ProjectileSpec they attach to the shot.
    /// </summary>
    public abstract class CannonBehaviourBase : IBlockBehaviour
    {
        /// <summary>The ProjectileSpec this variant's shot should carry.</summary>
        protected abstract ProjectileSpec BuildSpec(in BlockContext ctx);

        /// <summary>Ticks cooldown and fires (recording the shot and applying
        /// recoil) if the fire button was pressed this Step and the cooldown
        /// has elapsed.</summary>
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
