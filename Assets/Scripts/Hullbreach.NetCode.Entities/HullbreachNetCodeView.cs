using System.Collections.Generic;
using Hullbreach.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hullbreach.NetCode.Entities
{
    /// <summary>
    /// GameObject presentation for the Entities client replica. MultiplayerGame includes it
    /// by default; other gameplay scenes can add it without a ghost prefab or Asset Store content.
    /// </summary>
    [AddComponentMenu("Hullbreach/NetCode for Entities Client View")]
    public sealed class HullbreachNetCodeView : MonoBehaviour
    {
        [SerializeField] Color localShipColor = new Color(0.2f, 0.85f, 1f, 1f);
        [SerializeField] Color remoteShipColor = new Color(1f, 0.45f, 0.2f, 1f);

        readonly Dictionary<(ushort NetId, int BlockKey), GameObject> _visuals =
            new Dictionary<(ushort, int), GameObject>();
        readonly Dictionary<uint, GameObject> _projectileVisuals = new Dictionary<uint, GameObject>();

        void LateUpdate()
        {
            var replica = HullbreachNetCodeClient.Replica;
            if (replica == null) return;

            var live = new HashSet<(ushort, int)>();
            foreach (var shipPair in replica.Ships)
            {
                ushort netId = shipPair.Key;
                var ship = shipPair.Value;
                foreach (var block in ship.Grid.All)
                {
                    var key = (netId, block.Key);
                    live.Add(key);
                    if (!_visuals.TryGetValue(key, out GameObject visual) || visual == null)
                    {
                        visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        visual.name = $"NetCode.Block[{netId}:{block.Key}]";
                        Destroy(visual.GetComponent<Collider>());
                        visual.transform.SetParent(transform, false);
                        _visuals[key] = visual;
                    }

                    Color typeColor = ColorForType(block.Value.TypeId);
                    Color teamColor = netId == HullbreachNetCodeClient.LocalNetworkId ? localShipColor : remoteShipColor;
                    Color display = Color.Lerp(typeColor, teamColor, 0.28f);
                    display = Color.Lerp(display, Color.black, block.Value.DamageFraction * 0.8f);
                    visual.GetComponent<MeshRenderer>().material.color = display;

                    float2 local = BlockGrid.CenterOf(block.Key);
                    float2 world = ship.LocalToWorld(local);
                    visual.transform.SetPositionAndRotation(
                        new Vector3(world.x, world.y, 0f),
                        Quaternion.Euler(0f, 0f, ship.Rotation * Mathf.Rad2Deg));
                }
            }

            var stale = new List<(ushort, int)>();
            foreach (var pair in _visuals)
                if (!live.Contains(pair.Key)) stale.Add(pair.Key);
            foreach (var key in stale)
            {
                if (_visuals[key] != null) Destroy(_visuals[key]);
                _visuals.Remove(key);
            }

            UpdateProjectiles();
        }

        void UpdateProjectiles()
        {
            var projectiles = HullbreachNetCodeClient.Projectiles;
            var live = new HashSet<uint>();
            if (projectiles != null)
            {
                foreach (var pair in projectiles)
                {
                    live.Add(pair.Key);
                    if (!_projectileVisuals.TryGetValue(pair.Key, out GameObject visual) || visual == null)
                    {
                        visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        visual.name = $"NetCode.Projectile[{pair.Key}]";
                        Destroy(visual.GetComponent<Collider>());
                        visual.transform.SetParent(transform, false);
                        visual.GetComponent<MeshRenderer>().material.color = Color.yellow;
                        _projectileVisuals[pair.Key] = visual;
                    }

                    var projectile = pair.Value;
                    float diameter = Mathf.Max(0.12f, projectile.Radius * 2f);
                    visual.transform.position = new Vector3(projectile.Position.x, projectile.Position.y, -0.1f);
                    visual.transform.localScale = new Vector3(diameter, diameter, 1f);
                }
            }

            var stale = new List<uint>();
            foreach (var pair in _projectileVisuals)
                if (!live.Contains(pair.Key)) stale.Add(pair.Key);
            foreach (uint id in stale)
            {
                if (_projectileVisuals[id] != null) Destroy(_projectileVisuals[id]);
                _projectileVisuals.Remove(id);
            }
        }

        static Color ColorForType(byte typeId) => typeId switch
        {
            BlockTypes.Core => new Color(0.25f, 0.85f, 1f),
            BlockTypes.Hull => new Color(0.52f, 0.6f, 0.68f),
            BlockTypes.Armor => new Color(0.25f, 0.3f, 0.36f),
            BlockTypes.Thruster => new Color(0.92f, 0.28f, 0.12f),
            BlockTypes.Cannon => new Color(0.95f, 0.78f, 0.18f),
            BlockTypes.Fin => new Color(0.35f, 0.9f, 0.5f),
            BlockTypes.RetroThruster => new Color(0.18f, 0.9f, 0.42f),
            _ => Color.magenta,
        };

        void OnDestroy()
        {
            foreach (GameObject visual in _visuals.Values)
                if (visual != null) Destroy(visual);
            _visuals.Clear();
            foreach (GameObject visual in _projectileVisuals.Values)
                if (visual != null) Destroy(visual);
            _projectileVisuals.Clear();
        }
    }
}
