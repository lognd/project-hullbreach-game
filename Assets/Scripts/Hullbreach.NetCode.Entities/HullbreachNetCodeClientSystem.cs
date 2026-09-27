using System;
using System.Collections.Generic;
using Hullbreach.Net;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Hullbreach.NetCode.Entities
{
    /// <summary>Shared client-side state for presentation code in the GameObject world.</summary>
    public static class HullbreachNetCodeClient
    {
        public static ClientReplica Replica { get; internal set; }
        public static ushort LocalNetworkId { get; internal set; }
        public static bool IsConnected => Replica != null && LocalNetworkId != 0;
    }

    sealed class PendingPayload
    {
        public readonly byte[][] Chunks;
        public int Received;
        public int TotalBytes;

        public PendingPayload(int count) => Chunks = new byte[count][];
    }

    /// <summary>Bridges local input and received RPC payloads into ClientReplica.</summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class HullbreachNetCodeClientSystem : SystemBase
    {
        readonly Dictionary<uint, PendingPayload> _pending = new Dictionary<uint, PendingPayload>();
        ClientReplica _replica;
        EntityQuery _payloadQuery;
        uint _clientTick;

        protected override void OnCreate()
        {
            _payloadQuery = GetEntityQuery(
                ComponentType.ReadOnly<HullbreachPayloadRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            _replica = new ClientReplica();
            if (!World.IsThinClient()) HullbreachNetCodeClient.Replica = _replica;
            RequireForUpdate<NetworkStreamDriver>();
        }

        protected override void OnDestroy()
        {
            if (ReferenceEquals(HullbreachNetCodeClient.Replica, _replica))
            {
                HullbreachNetCodeClient.Replica = null;
                HullbreachNetCodeClient.LocalNetworkId = 0;
            }
            _pending.Clear();
        }

        protected override void OnUpdate()
        {
            ReceivePayloads();

            if (!SystemAPI.TryGetSingleton<NetworkId>(out var networkId))
            {
                HullbreachNetCodeClient.LocalNetworkId = 0;
                return;
            }

            HullbreachNetCodeClient.LocalNetworkId = (ushort)networkId.Value;
            if (World.IsThinClient()) return;

            _clientTick++;
            InputMessage input = ClientReplica.BuildInput(
                (ushort)networkId.Value,
                _clientTick,
                Input.GetAxisRaw("Vertical"),
                Input.GetAxisRaw("Horizontal"),
                Input.GetMouseButton(0));

            Entity rpcEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(rpcEntity, new HullbreachInputRpc
            {
                Tick = input.Tick,
                ThrustAxis = input.ThrustAxis,
                Steer = input.Steer,
                Flags = input.Flags,
            });
            EntityManager.AddComponentData(rpcEntity, new SendRpcCommandRequest { TargetConnection = Entity.Null });
        }

        void ReceivePayloads()
        {
            using var entities = _payloadQuery.ToEntityArray(Allocator.Temp);
            using var payloads = _payloadQuery.ToComponentDataArray<HullbreachPayloadRpc>(Allocator.Temp);

            for (int i = 0; i < payloads.Length; i++) AcceptChunk(payloads[i]);
            if (entities.Length > 0) EntityManager.DestroyEntity(entities);
        }

        void AcceptChunk(HullbreachPayloadRpc rpc)
        {
            if (rpc.ChunkCount == 0 || rpc.ChunkIndex >= rpc.ChunkCount ||
                rpc.ChunkCount > (HullbreachNetCodeConstants.MaximumMessageBytes / HullbreachNetCodeConstants.PayloadBytesPerChunk) + 1)
                return;

            if (!_pending.TryGetValue(rpc.MessageId, out PendingPayload pending))
            {
                pending = new PendingPayload(rpc.ChunkCount);
                _pending.Add(rpc.MessageId, pending);
            }
            if (pending.Chunks.Length != rpc.ChunkCount || pending.Chunks[rpc.ChunkIndex] != null) return;

            var chunk = new byte[rpc.Payload.Length];
            for (int i = 0; i < chunk.Length; i++) chunk[i] = rpc.Payload[i];
            pending.Chunks[rpc.ChunkIndex] = chunk;
            pending.Received++;
            pending.TotalBytes += chunk.Length;

            if (pending.TotalBytes > HullbreachNetCodeConstants.MaximumMessageBytes)
            {
                _pending.Remove(rpc.MessageId);
                return;
            }
            if (pending.Received != pending.Chunks.Length) return;

            var message = new byte[pending.TotalBytes];
            int offset = 0;
            foreach (byte[] part in pending.Chunks)
            {
                Buffer.BlockCopy(part, 0, message, offset, part.Length);
                offset += part.Length;
            }
            _pending.Remove(rpc.MessageId);
            _replica.ApplyReceived(message, message.Length, _clientTick);
        }
    }
}
