using Unity.Mathematics;

namespace Hullbreach.Ship.Behaviours
{
    // Thruster variant 1, the "inconvenient thruster": pushes toward the
    // nearest enemy instead of ship-forward; falls back when none is known.
    // frob:doc docs/reference/hullbreach-ship.md#seekingthrusterbehaviour
    public sealed class SeekingThrusterBehaviour : IBlockBehaviour
    {
        // frob:doc docs/reference/hullbreach-ship.md#seekingthrusterbehaviour
        public void Step(ref BlockContext ctx)
        {
            float target = ctx.Input.ThrustAxis > 0f ? 1f : 0f;
            float updated = ctx.Throttle(target);
            if (updated == 0f) return;

            float2 localDirection = new float2(0f, 1f);
            if (ctx.World != null &&
                ctx.World.TryNearestEnemy(ctx.Ship.Position, ctx.Ship, out var enemyPosition, out _))
            {
                float2 toEnemy = enemyPosition - ctx.Ship.Position;
                float distance = math.length(toEnemy);
                if (distance > 1e-6f)
                {
                    float2 worldDirection = toEnemy / distance;
                    localDirection = ctx.Ship.WorldVectorToLocal(worldDirection);
                }
            }

            ctx.AddForceLocal(localDirection * updated * ctx.Ship.ThrustPerBlock);
        }
    }
}
