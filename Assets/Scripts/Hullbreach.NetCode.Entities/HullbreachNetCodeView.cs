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
                        visual.GetComponent<MeshRenderer>().material.color =
                            netId == HullbreachNetCodeClient.LocalNetworkId ? localShipColor : remoteShipColor;
                        _visuals[key] = visual;
                    }

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
        }

        void OnDestroy()
        {
            foreach (GameObject visual in _visuals.Values)
                if (visual != null) Destroy(visual);
            _visuals.Clear();
        }
    }
}
