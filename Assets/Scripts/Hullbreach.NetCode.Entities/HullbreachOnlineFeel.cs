using System.Collections.Generic;
using Hullbreach.Core;
using Hullbreach.Game;
using Hullbreach.Net;
using Hullbreach.Ship;
using UnityEngine;

namespace Hullbreach.NetCode.Entities
{
    /// <summary>
    /// Online counterpart of what ShipController and the projectile spawner do offline: turns the replica's applied
    /// events (BlockDamaged / BlockDestroyed / BlockPlaced / PowerupApplied) and newly seen projectiles into
    /// <see cref="FeelFx"/> impacts, muzzle flashes and shake, and shows the planet-impact warning and assist
    /// indicator for the local replicated ship. Created once at start-up; idle outside an online match.
    /// </summary>
    public sealed class HullbreachOnlineFeel : MonoBehaviour
    {
        readonly Dictionary<(ushort, int), byte> _damage = new Dictionary<(ushort, int), byte>();
        readonly HashSet<uint> _seenProjectiles = new HashSet<uint>();
        readonly List<uint> _scratch = new List<uint>();
        bool _hud;

        // Diagnostics for Play Mode checks: how many events and shots were turned into effects.
        public static int EventsHandled, ImpactsFired, MuzzlesFired;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("OnlineFeel");
            DontDestroyOnLoad(go);
            go.AddComponent<HullbreachOnlineFeel>();
        }

        static ShipBody LocalShip()
        {
            var replica = HullbreachNetCodeClient.Replica;
            return replica != null && replica.Ships.TryGetValue(HullbreachNetCodeClient.LocalNetworkId, out ShipBody ship) ? ship : null;
        }

        void Update()
        {
            var replica = HullbreachNetCodeClient.Replica;
            if (replica == null || !HullbreachNetworkGameplayController.Active || !HullbreachNetCodeClient.IsConnected)
            {
                _damage.Clear(); _seenProjectiles.Clear(); _hud = false;
                return;
            }

            if (!_hud)
            {
                _hud = true;
                FlightHudView.Ensure(() => HullbreachNetworkGameplayController.IsBuildMode ? null : LocalShip(), () => !HullbreachNetworkGameplayController.IsBuildMode && !Input.GetKey(KeyCode.LeftShift));
            }

            ushort local = HullbreachNetCodeClient.LocalNetworkId;
            while (replica.TryDequeueEvent(out ReplicaEvent evt))
            {
                Vector2 pos = new Vector2(evt.WorldPosition.x, evt.WorldPosition.y);
                Vector2 dir = Vector2.up;
                if (replica.Ships.TryGetValue(evt.NetId, out ShipBody owner))
                {
                    Vector2 away = pos - new Vector2(owner.Position.x, owner.Position.y);
                    if (away.sqrMagnitude > 0.01f) dir = away;
                }
                bool mine = evt.NetId == local;
                EventsHandled++;
                switch (evt.Kind)
                {
                    case ReplicaEventKind.BlockDamaged:
                        ImpactsFired++;
                        FeelFx.Impact(pos, -dir, DamageDealt(replica, evt), mine);
                        break;
                    case ReplicaEventKind.BlockDestroyed:
                        FeelFx.Impact(pos, -dir, 90f, mine);
                        FeelFx.AddShake(mine ? 0.5f : 0.2f);
                        break;
                    case ReplicaEventKind.BlockPlaced:
                    case ReplicaEventKind.PowerupApplied:
                        FeelFx.PlayClick();
                        break;
                }
            }

            // Remember every block's damage so the next BlockDamaged can say how much it was.
            foreach (var shipPair in replica.Ships)
                foreach (var block in shipPair.Value.Grid.All)
                    _damage[(shipPair.Key, block.Key)] = block.Value.Damage;

            var projectiles = HullbreachNetCodeClient.Projectiles;
            if (projectiles == null) return;
            foreach (var pair in projectiles)
            {
                if (!_seenProjectiles.Add(pair.Key)) continue;
                var v = pair.Value;
                var p = new Vector2(v.Position.x, v.Position.y);
                var d = new Vector2(v.Velocity.x, v.Velocity.y);
                // A shot already well away from its owner was fired before we looked; no flash for it.
                if (replica.Ships.TryGetValue(v.OwnerNetId, out ShipBody shooter) &&
                    (p - new Vector2(shooter.Position.x, shooter.Position.y)).magnitude < 8f)
                { MuzzlesFired++; FeelFx.Muzzle(p, d, Vector2.zero); }
            }
            if (_seenProjectiles.Count > 256)
            {
                _scratch.Clear();
                foreach (uint id in _seenProjectiles) if (!projectiles.ContainsKey(id)) _scratch.Add(id);
                foreach (uint id in _scratch) _seenProjectiles.Remove(id);
            }
        }

        float DamageDealt(ClientReplica replica, ReplicaEvent evt)
        {
            if (!replica.Ships.TryGetValue(evt.NetId, out ShipBody ship)) return 25f;
            int key = BlockKey.Pack(evt.X, evt.Y);
            if (!ship.Grid.TryGet(key, out Block block)) return 25f;
            return _damage.TryGetValue((evt.NetId, key), out byte before) && block.Damage > before ? block.Damage - before : 25f;
        }
    }
}
