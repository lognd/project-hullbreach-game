using Unity.Entities;
using UnityEngine;

namespace Hullbreach.NetCode.Entities
{
    [DisallowMultipleComponent]
    public sealed class HullbreachGameplayGhostAuthoring : MonoBehaviour
    {
        sealed class GameplayGhostBaker : Baker<HullbreachGameplayGhostAuthoring>
        {
            public override void Bake(HullbreachGameplayGhostAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, default(HullbreachGhostPose));
            }
        }
    }
}
