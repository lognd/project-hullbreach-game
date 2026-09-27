using Unity.Collections;
using Unity.NetCode;

namespace Hullbreach.NetCode.Entities
{
    /// <summary>Latest-wins player input sent from a client to the authoritative server.</summary>
    public struct HullbreachInputRpc : IRpcCommand
    {
        public uint Tick;
        public sbyte ThrustAxis;
        public sbyte Steer;
        public byte Flags;
    }

    public enum HullbreachBuildAction : byte
    {
        Place = 1,
        Remove = 2,
    }

    /// <summary>Server-validated edit request for the caller's own ship.</summary>
    public struct HullbreachBuildRpc : IRpcCommand
    {
        public sbyte X;
        public sbyte Y;
        public byte TypeId;
        public byte Modifiers;
        public byte Action;
    }

    /// <summary>
    /// One fragment of a Hullbreach wire message. Netcode for Entities RPCs have a 1 KiB
    /// serialized payload limit, while a full ship snapshot can be much larger.
    /// </summary>
    public struct HullbreachPayloadRpc : IRpcCommand
    {
        public uint MessageId;
        public ushort ChunkIndex;
        public ushort ChunkCount;

        [GhostFixedListCapacity(Capacity = HullbreachNetCodeConstants.PayloadBytesPerChunk)]
        public FixedList4096Bytes<byte> Payload;
    }

    public static class HullbreachNetCodeConstants
    {
        public const ushort DefaultPort = 7979;
        public const int SimulationTickRate = 50;
        public const int PayloadBytesPerChunk = 900;
        public const int MaximumMessageBytes = 1024 * 1024;
    }
}
