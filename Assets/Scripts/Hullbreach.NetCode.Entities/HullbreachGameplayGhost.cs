using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Hullbreach.NetCode.Entities
{
    public enum HullbreachGhostKind : byte
    {
        Ship = 1,
        Projectile = 2,
    }

    /// <summary>Interpolated server-authoritative motion replicated by the ghost snapshot stream.</summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct HullbreachGhostPose : IComponentData
    {
        [GhostField] public byte Kind;
        [GhostField] public ushort NetId;
        [GhostField] public uint SimulationId;

        [GhostField(Quantization = 1000, Smoothing = SmoothingAction.InterpolateAndExtrapolate)]
        public float2 Position;

        [GhostField(Quantization = 1000, Smoothing = SmoothingAction.Interpolate)]
        public float Rotation;

        [GhostField(Quantization = 1000)] public float2 Velocity;
        [GhostField(Quantization = 1000)] public float AngularVelocity;
        [GhostField(Quantization = 1000)] public float Radius;
    }

    /// <summary>Singleton baked from the gameplay SubScene so both worlds know the ghost prefab.</summary>
    public struct HullbreachGhostPrefab : IComponentData
    {
        public Entity Value;
    }

}
