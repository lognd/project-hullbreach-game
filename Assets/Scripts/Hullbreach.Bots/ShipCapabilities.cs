using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Ship;

namespace Hullbreach.Bots
{
    // What a ship can do, read off its own blocks: the numbers a player learns by flying it.
    // Recomputed by BotPeer when the block layout changes.
    // frob:ticket 01M7EXX75QK41EKPV5392HXCAX
    // frob:doc docs/reference/hullbreach-bots.md#shipcapabilities
    public readonly struct ShipCapabilities
    {
        public readonly float MaxForwardAccel;
        public readonly float MaxRetroAccel;
        public readonly float MaxAngularAccel;
        public readonly float2 AimLocal;
        public readonly bool HasCannon;
        public readonly float Radius;

        ShipCapabilities(float fwd, float retro, float ang, float2 aim, bool hasCannon, float radius)
        {
            MaxForwardAccel = fwd; MaxRetroAccel = retro; MaxAngularAccel = ang;
            AimLocal = aim; HasCannon = hasCannon; Radius = radius;
        }

        // frob:doc docs/reference/hullbreach-bots.md#shipcapabilities
        public static ShipCapabilities Measure(ShipBody ship)
        {
            float mass = ship.Grid.Mass.Total;
            if (mass <= 0f) return new ShipCapabilities(0f, 0f, 0f, new float2(0f, 1f), false, 1f);

            float2 com = ship.Grid.Mass.CenterOfMass;
            float inertia = ship.Grid.Mass.InertiaAboutCenterOfMass;

            float fwd = ship.ThrusterKeys.Length * ship.ThrustPerBlock / mass;
            float retro = ship.RetroKeys.Length * ship.RetroThrustPerBlock / mass;

            // Each fin pushes along its perpendicular at its lever arm about the centre of mass.
            float torque = 0f;
            foreach (int key in ship.FinKeys)
            {
                if (!ship.Grid.TryGet(key, out Block fin)) continue;
                float2 perp = Facing.Perpendicular(fin.Modifiers);
                float2 r = BlockGrid.CenterOf(key) - com;
                torque += math.abs(r.x * perp.y - r.y * perp.x) * ship.FinForce;
            }
            float ang = inertia > 0f ? torque / inertia : 0f;

            float2 aim = float2.zero;
            foreach (int key in ship.WeaponKeys)
            {
                if (ship.Grid.TryGet(key, out Block cannon)) aim += Facing.Direction(cannon.Modifiers);
            }
            bool hasCannon = ship.WeaponKeys.Length > 0 && math.lengthsq(aim) > 1e-4f;
            aim = hasCannon ? math.normalize(aim) : new float2(0f, 1f);

            float radius = 1f;
            foreach (var kv in ship.Grid.All)
                radius = math.max(radius, math.length(BlockGrid.CenterOf(kv.Key) - com) + 0.5f);

            return new ShipCapabilities(fwd, retro, ang, aim, hasCannon, radius);
        }
    }
}
