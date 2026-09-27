using System;
using System.Collections.Generic;
using Hullbreach.Core;
using Hullbreach.Net;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Hullbreach.NetCode.Entities
{
    /// <summary>Runs ServerSimulation in the Netcode for Entities server world.</summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class HullbreachNetCodeServerSystem : SystemBase
    {
        readonly HashSet<int> _joinedPeers = new HashSet<int>();
        readonly Dictionary<int, Entity> _connections = new Dictionary<int, Entity>();
        ServerOutbox _outbox;
        ServerSimulation _simulation;
        EntityQuery _connectionQuery;
        EntityQuery _inputQuery;
        uint _nextMessageId = 1;

        protected override void OnCreate()
        {
            _outbox = new ServerOutbox();
            _simulation = new ServerSimulation(_outbox)
            {
                TickRate = HullbreachNetCodeConstants.SimulationTickRate,
            };
            _connectionQuery = GetEntityQuery(
                ComponentType.ReadOnly<NetworkId>(),
                ComponentType.ReadOnly<NetworkStreamConnection>());
            _inputQuery = GetEntityQuery(
                ComponentType.ReadOnly<HullbreachInputRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            RequireForUpdate<NetworkStreamDriver>();
        }

        protected override void OnUpdate()
        {
            SynchronizeConnections();
            ConsumeInputs();
            _simulation.Tick();
            FlushOutbox();
        }

        void SynchronizeConnections()
        {
            _connections.Clear();
            using var entities = _connectionQuery.ToEntityArray(Allocator.Temp);
            using var ids = _connectionQuery.ToComponentDataArray<NetworkId>(Allocator.Temp);
            var active = new HashSet<int>();

            for (int i = 0; i < entities.Length; i++)
            {
                int peer = ids[i].Value;
                active.Add(peer);
                _connections[peer] = entities[i];
                if (_joinedPeers.Add(peer))
                    _simulation.Join(peer, CreateStarterShip(peer));
            }

            var disconnected = new List<int>();
            foreach (int peer in _joinedPeers)
                if (!active.Contains(peer)) disconnected.Add(peer);

            foreach (int peer in disconnected)
            {
                _joinedPeers.Remove(peer);
                _simulation.Leave(peer);
            }
        }

        void ConsumeInputs()
        {
            using var entities = _inputQuery.ToEntityArray(Allocator.Temp);
            using var inputs = _inputQuery.ToComponentDataArray<HullbreachInputRpc>(Allocator.Temp);
            using var requests = _inputQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                Entity source = requests[i].SourceConnection;
                if (EntityManager.HasComponent<NetworkId>(source))
                {
                    int peer = EntityManager.GetComponentData<NetworkId>(source).Value;
                    var rpc = inputs[i];
                    _simulation.SetInput(peer, new InputMessage((ushort)peer, rpc.Tick, rpc.ThrustAxis, rpc.Steer, rpc.Flags));
                }
            }

            if (entities.Length > 0) EntityManager.DestroyEntity(entities);
        }

        void FlushOutbox()
        {
            foreach (var entry in _outbox.Entries)
            {
                if (!_connections.TryGetValue(entry.Peer, out Entity target)) continue;
                SendPayload(target, entry.Bytes);
            }
            _outbox.Clear();
        }

        void SendPayload(Entity target, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > HullbreachNetCodeConstants.MaximumMessageBytes)
                throw new InvalidOperationException($"Invalid Hullbreach network payload size: {bytes?.Length ?? 0}");

            int chunkSize = HullbreachNetCodeConstants.PayloadBytesPerChunk;
            int chunkCount = (bytes.Length + chunkSize - 1) / chunkSize;
            uint messageId = _nextMessageId++;
            if (_nextMessageId == 0) _nextMessageId = 1;

            for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                int start = chunkIndex * chunkSize;
                int count = math.min(chunkSize, bytes.Length - start);
                var payload = new FixedList4096Bytes<byte>();
                for (int i = 0; i < count; i++) payload.Add(bytes[start + i]);

                Entity rpcEntity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(rpcEntity, new HullbreachPayloadRpc
                {
                    MessageId = messageId,
                    ChunkIndex = (ushort)chunkIndex,
                    ChunkCount = (ushort)chunkCount,
                    Payload = payload,
                });
                EntityManager.AddComponentData(rpcEntity, new SendRpcCommandRequest { TargetConnection = target });
            }
        }

        static ShipSnapshot CreateStarterShip(int peer)
        {
            float x = ((peer & 1) == 0 ? 1f : -1f) * (3f + (peer % 4) * 2f);
            var blocks = new[]
            {
                new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0),
                new SnapshotBlock(0, -1, BlockTypes.Thruster, 0, 0),
                new SnapshotBlock(0, 1, BlockTypes.Cannon, 0, 0),
                new SnapshotBlock(-1, 0, BlockTypes.Hull, 0, 0),
                new SnapshotBlock(1, 0, BlockTypes.Hull, 0, 0),
            };
            return new ShipSnapshot(0, (ushort)peer, blocks,
                Quantization.PackPosition(x), 0, 0, 0, 0, 0);
        }
    }
}
