using UnityEngine;
using Unity.Mathematics;
using Hullbreach.Hud;
using Hullbreach.World;

namespace Hullbreach.Game
{
    // Everything StormHudView draws, gathered each frame by whichever scene owns the match:
    // DemoStormHudSource offline, HullbreachStormHudSource online.
    // frob:doc docs/design/ui-port.md#stormhudview
    public struct StormHudFrame
    {
        public bool HasStorm;
        public StormState Storm;
        public float TickRate;

        public bool HasLocal;
        public Vector2 LocalPosition;

        public bool HasEnemy;
        public Vector2 EnemyPosition;

        // World-space planet discs for the radar; may be null.
        public MinimapDisc[] Planets;

        // The area the radar fits (arena centre and radius).
        public Vector2 ArenaCenter;
        public float ArenaRadius;
    }

    // frob:doc docs/design/ui-port.md#stormhudview
    public interface IStormHudSource
    {
        // False while there is nothing to show (no match yet); the view hides itself.
        bool TryGetFrame(out StormHudFrame frame);
    }

    // Shared by both sources: the same radar discs from a gravity field's bodies.
    public static class StormHudPlanets
    {
        static readonly Color32[] Palette =
        {
            new Color32(120, 150, 220, 255), new Color32(210, 150, 90, 255), new Color32(120, 200, 140, 255),
            new Color32(190, 130, 200, 255),
        };

        public static MinimapDisc[] FromField(GravityField field)
        {
            if (field == null) return System.Array.Empty<MinimapDisc>();
            var list = new System.Collections.Generic.List<MinimapDisc>();
            for (int i = 0; field.TryGetPermanent(i, out GravityBody body); i++)
            {
                Color32 c = Palette[i % Palette.Length];
                var specs = GravityWorld.Specs;
                if (i < specs.Count) c = specs[i].color;
                list.Add(new MinimapDisc(body.Position.x, body.Position.y, body.Radius, c.r, c.g, c.b));
            }
            return list.ToArray();
        }

        public static float2[] Centers(MinimapDisc[] discs)
        {
            var result = new float2[discs.Length];
            for (int i = 0; i < discs.Length; i++) result[i] = new float2(discs[i].X, discs[i].Y);
            return result;
        }
    }
}
