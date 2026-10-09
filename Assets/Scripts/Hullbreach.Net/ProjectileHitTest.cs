using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Ship;

namespace Hullbreach.Net
{
    // Server-side projectile versus ship geometry, kept engine-free so it can
    // be tested without a running ServerSimulation.
    // frob:doc docs/reference/hullbreach-net.md#projectilehittest
    public static class ProjectileHitTest
    {
        // Finds the occupied cell nearest a circle of `radius` at `worldPoint`
        // that the circle overlaps (a zero radius is the point test).
        // frob:doc docs/reference/hullbreach-net.md#projectilehittest
        public static bool TryHitShipCell(ShipBody ship, float2 worldPoint, float radius, out int hitKey)
        {
            float2 local = ship.WorldToLocal(worldPoint);
            float reach = math.max(0f, radius);
            int minX = (int)math.floor(local.x - reach);
            int maxX = (int)math.floor(local.x + reach);
            int minY = (int)math.floor(local.y - reach);
            int maxY = (int)math.floor(local.y + reach);

            hitKey = -1;
            float bestDistSq = float.PositiveInfinity;
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (!BlockKey.InRange(x, y)) continue;
                    int k = BlockKey.Pack(x, y);
                    if (!ship.Grid.Contains(k)) continue;
                    // Distance from the circle centre to the unit cell [x,x+1] x [y,y+1].
                    float dx = local.x - math.clamp(local.x, x, x + 1f);
                    float dy = local.y - math.clamp(local.y, y, y + 1f);
                    float distSq = dx * dx + dy * dy;
                    if (distSq > reach * reach || distSq >= bestDistSq) continue;
                    bestDistSq = distSq;
                    hitKey = k;
                }
            }
            return hitKey != -1;
        }
    }
}
