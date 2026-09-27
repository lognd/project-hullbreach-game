using Unity.Entities;
using UnityEngine;

namespace Hullbreach.NetCode.Entities
{
    [DisallowMultipleComponent]
    public sealed class HullbreachGhostPrefabAuthoring : MonoBehaviour
    {
        public GameObject GhostPrefab;

        sealed class GhostPrefabBaker : Baker<HullbreachGhostPrefabAuthoring>
        {
            public override void Bake(HullbreachGhostPrefabAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new HullbreachGhostPrefab
                {
                    Value = GetEntity(authoring.GhostPrefab, TransformUsageFlags.Dynamic),
                });
            }
        }
    }
}
