using System;
using Hullbreach.Game;
using Hullbreach.Hud;
using Unity.Mathematics;
using UnityEngine;

namespace Hullbreach.NetCode.Entities
{
    /// <summary>Feeds <see cref="StormHudView"/> in the online scene from the replica: the storm the server announced
    /// (StormStarted: seed, start tick, candidates), the newest server tick, and the replicated ships.</summary>
    [AddComponentMenu("Hullbreach/Online/Storm HUD Source")]
    public sealed class HullbreachStormHudSource : MonoBehaviour, IStormHudSource
    {
        // The client does not know the planet radii, only where the server's bodies are (the storm's
        // final-ring candidates), so planets show as fixed small discs.
        const float PlanetMarkRadius = 5f;

        MinimapDisc[] _planets = Array.Empty<MinimapDisc>();
        object _planetsFor;
        System.Collections.Generic.List<Hullbreach.Net.PickupView> _pickupViews;
        Vector2[] _pickupPos = new Vector2[24];

        public bool TryGetFrame(out StormHudFrame frame)
        {
            frame = default;
            var replica = HullbreachNetCodeClient.Replica;
            if (replica == null || !HullbreachNetCodeClient.IsConnected) return false;

            var zone = replica.Storm;
            var start = replica.StormStart;
            if (zone != null && start.HasValue)
            {
                long tick = (long)replica.SimulationTick - start.Value.StartTick;
                frame.HasStorm = true;
                frame.Storm = zone.At((int)Math.Max(0L, Math.Min(int.MaxValue, tick)));
                frame.TickRate = zone.Config.TickRate;
                frame.ArenaCenter = new Vector2(start.Value.ArenaCenter.x, start.Value.ArenaCenter.y);
                frame.ArenaRadius = zone.Config.StartRadius;
                if (!ReferenceEquals(_planetsFor, start.Value.Candidates))
                {
                    _planetsFor = start.Value.Candidates;
                    var c = start.Value.Candidates ?? Array.Empty<float2>();
                    _planets = new MinimapDisc[c.Length];
                    for (int i = 0; i < c.Length; i++)
                        _planets[i] = new MinimapDisc(c[i].x, c[i].y, PlanetMarkRadius, 110, 140, 200);
                }
                frame.Planets = _planets;
            }
            else
            {
                frame.ArenaRadius = 100f; // no storm announced yet: still show the radar around the origin
                frame.Planets = _planets;
            }

            if (_pickupViews == null) _pickupViews = new System.Collections.Generic.List<Hullbreach.Net.PickupView>(32);
            replica.Economy.GetPickups(replica.SimulationTick, _pickupViews);
            int pc = Math.Min(_pickupViews.Count, 24);
            if (_pickupPos.Length < pc) _pickupPos = new Vector2[24];
            for (int i = 0; i < pc; i++) _pickupPos[i] = new Vector2(_pickupViews[i].Position.x, _pickupViews[i].Position.y);
            frame.Pickups = _pickupPos; frame.PickupCount = pc;

            ushort local = HullbreachNetCodeClient.LocalNetworkId;
            foreach (var kv in replica.Ships)
            {
                var p = kv.Value.Position;
                if (kv.Key == local) { frame.HasLocal = true; frame.LocalPosition = new Vector2(p.x, p.y); }
                else if (!frame.HasEnemy) { frame.HasEnemy = true; frame.EnemyPosition = new Vector2(p.x, p.y); }
            }
            return true;
        }
    }
}
