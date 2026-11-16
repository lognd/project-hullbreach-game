using System;
using System.Collections.Generic;
using Hullbreach.Net;
using Hullbreach.Ship;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;
using Unity.Mathematics;

namespace Hullbreach.NetCode.Entities
{
    /// <summary>Shared client-side state for presentation code in the GameObject world.</summary>
    public static class HullbreachNetCodeClient
    {
        public readonly struct ProjectileVisual
        {
            public readonly ushort OwnerNetId;
            public readonly float2 Position;
            public readonly float2 Velocity;
            public readonly float Radius;

            public ProjectileVisual(ushort ownerNetId, float2 position, float2 velocity, float radius)
            {
                OwnerNetId = ownerNetId;
                Position = position;
                Velocity = velocity;
                Radius = radius;
            }
        }

        public static ClientReplica Replica { get; internal set; }
        public static ushort LocalNetworkId { get; internal set; }
        /// <summary>ShipSkins id per ship (net id), from the ghost stream; absent means the default look.</summary>
        public static readonly Dictionary<ushort, byte> ShipSkinIds = new Dictionary<ushort, byte>();
        public static IReadOnlyDictionary<uint, ProjectileVisual> Projectiles { get; internal set; }
        // Test hook (Play Mode scripts): replaces the keyboard while flying. Null = real input.
        public static ShipInput? ScriptedInput;
        public static bool IsConnected => Replica != null && LocalNetworkId != 0;

        static NetworkEndpoint _lastEndpoint;
        static bool _haveEndpoint;

        /// <summary>Remembers where the client last dialled, so a dropped link can be redialled (ReconnectPolicy).</summary>
        public static void RememberEndpoint(NetworkEndpoint endpoint)
        {
            _lastEndpoint = endpoint;
            _haveEndpoint = true;
        }

        /// <summary>
        /// Tells the server this player is leaving on purpose so it frees the ship immediately instead of holding it for the
        /// reconnect grace window. Call it just before closing the connection; the RPC goes out on the world's next update,
        /// so callers that dispose the worlds do so a frame later. Returns false when there is no connected client world.
        /// </summary>
        public static bool SendLeave()
        {
            Unity.Entities.World client = ClientServerBootstrap.ClientWorld;
            if (client == null || !client.IsCreated || LocalNetworkId == 0) return false;
            client.EntityManager.CreateEntity(typeof(HullbreachLeaveRpc), typeof(SendRpcCommandRequest));
            return true;
        }

        // Closing the game window is a deliberate leave too. Best effort: the world may not get another update.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void HookApplicationQuit()
        {
            // Enter Play Mode runs without a domain reload, so statics outlive the session: a test script's
            // pilot would otherwise keep swallowing the keyboard in the next hand-played session.
            ScriptedInput = null;
            Application.quitting -= OnApplicationQuitting;
            Application.quitting += OnApplicationQuitting;
        }

        static void OnApplicationQuitting() => SendLeave();

        /// <summary>Asks the client world to connect to the remembered endpoint again; false when there is nothing to dial.</summary>
        public static bool TryRequestReconnect()
        {
            Unity.Entities.World client = ClientServerBootstrap.ClientWorld;
            if (!_haveEndpoint || client == null || !client.IsCreated) return false;
            Entity request = client.EntityManager.CreateEntity(typeof(NetworkStreamRequestConnect));
            client.EntityManager.SetComponentData(request, new NetworkStreamRequestConnect { Endpoint = _lastEndpoint });
            return true;
        }
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
        EntityQuery _ghostQuery;
        readonly Dictionary<uint, HullbreachNetCodeClient.ProjectileVisual> _projectiles =
            new Dictionary<uint, HullbreachNetCodeClient.ProjectileVisual>();
        uint _clientTick;
        string _lastMatchStatus;

        protected override void OnCreate()
        {
            _payloadQuery = GetEntityQuery(
                ComponentType.ReadOnly<HullbreachPayloadRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            _ghostQuery = GetEntityQuery(ComponentType.ReadOnly<HullbreachGhostPose>());
            _replica = CreateReplica();
            if (!World.IsThinClient())
            {
                HullbreachNetCodeClient.Replica = _replica;
                HullbreachNetCodeClient.Projectiles = _projectiles;
            }
            RequireForUpdate<NetworkStreamDriver>();
        }

        ClientReplica CreateReplica()
        {
            var replica = new ClientReplica();
            // The server runs ArenaConfig.Defaults() (ConfigureDemoArena); mirror it so planets exist client side.
            replica.UseArena(ArenaConfig.Defaults());
            // Remote clients never see the server's feed, so the replicated match end feeds the results screen.
            if (!World.IsThinClient()) replica.MatchFeed = Hullbreach.Platform.MatchEndFeed.Shared;
            return replica;
        }

        protected override void OnDestroy()
        {
            if (ReferenceEquals(HullbreachNetCodeClient.Replica, _replica))
            {
                HullbreachNetCodeClient.Replica = null;
                HullbreachNetCodeClient.LocalNetworkId = 0;
                HullbreachNetCodeClient.ShipSkinIds.Clear();
                HullbreachNetCodeClient.Projectiles = null;
            }
            _pending.Clear();
            _projectiles.Clear();
        }

        protected override void OnUpdate()
        {
            ReceivePayloads();
            ReceiveGhostPoses();

            if (!SystemAPI.TryGetSingleton<NetworkId>(out var networkId))
            {
                if (!World.IsThinClient())
                {
                    // A dropped link: start from a clean replica, because the rejoin snapshot restarts the ordered
                    // stream far past this one's cursor (the server re-sends storm, pickups and stock on join).
                    if (HullbreachNetCodeClient.LocalNetworkId != 0)
                    {
                        _replica = CreateReplica();
                        _projectiles.Clear();
                        HullbreachNetCodeClient.Replica = _replica;
                        _lastMatchStatus = null;
                    }
                    HullbreachNetCodeClient.LocalNetworkId = 0;
                }
                return;
            }

            if (World.IsThinClient()) return;
            HullbreachNetCodeClient.LocalNetworkId = (ushort)networkId.Value;

            // Lobby connections intentionally carry no gameplay traffic. The
            // controller only exists in MultiplayerGame after the host starts.
            if (!HullbreachNetworkGameplayController.Active) return;

            // Greppable proof that this client reached the match (scripts and docs use it).
            string matchStatus = DirectLaunchPlan.MatchStatus(networkId.Value, _replica.Ships.Count);
            if (matchStatus != _lastMatchStatus)
            {
                _lastMatchStatus = matchStatus;
                Debug.Log(matchStatus);
            }

            Entity connection = SystemAPI.GetSingletonEntity<NetworkId>();
            if (!EntityManager.HasComponent<NetworkStreamInGame>(connection))
                EntityManager.AddComponent<NetworkStreamInGame>(connection);

            _clientTick++;
            bool building = HullbreachNetworkGameplayController.IsBuildMode;
            // Unfocused windows send neutral input so no key is stuck; fire is an edge, as offline.
            // frob:ticket 01M6WV815GYEJ58MTM4HSS4Z6K
            bool live = !building && Application.isFocused;
            ShipInput shaped = live && HullbreachNetCodeClient.ScriptedInput.HasValue
                ? HullbreachNetCodeClient.ScriptedInput.Value
                : live
                ? ShipInputShaper.Shape(
                    Input.GetAxisRaw("Vertical"),
                    Input.GetAxisRaw("Horizontal"),
                    Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
                : new ShipInput(0f, 0f, false);
            InputMessage basicInput = ClientReplica.BuildInput(
                (ushort)networkId.Value,
                _clientTick,
                shaped.ThrustAxis,
                shaped.Steer,
                shaped.FirePressed,
                live && Input.GetKey(KeyCode.LeftShift));
            byte flags = basicInput.Flags;
            if (building) flags |= InputMessage.BuildModeBit;
            InputMessage input = new InputMessage(basicInput.NetId, basicInput.Tick,
                basicInput.ThrustAxis, basicInput.Steer, flags);

            Entity rpcEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(rpcEntity, new HullbreachInputRpc
            {
                Tick = input.Tick,
                ThrustAxis = input.ThrustAxis,
                Steer = input.Steer,
                Flags = input.Flags,
            });
            EntityManager.AddComponentData(rpcEntity, new SendRpcCommandRequest { TargetConnection = Entity.Null });

            while (HullbreachNetworkGameplayController.TryDequeueDesign(out string design))
            {
                Entity designEntity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(designEntity, new HullbreachDesignRpc { Design = new FixedString512Bytes(design) });
                EntityManager.AddComponentData(designEntity, new SendRpcCommandRequest { TargetConnection = Entity.Null });
            }
            while (HullbreachNetworkGameplayController.TryDequeueBuild(out var build))
            {
                Entity buildEntity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(buildEntity, new HullbreachBuildRpc
                {
                    X = build.X,
                    Y = build.Y,
                    TypeId = build.TypeId,
                    Modifiers = build.Modifiers,
                    Action = (byte)build.Action,
                });
                EntityManager.AddComponentData(buildEntity,
                    new SendRpcCommandRequest { TargetConnection = Entity.Null });
            }
        }

        void ReceiveGhostPoses()
        {
            if (World.IsThinClient()) return;

            using var poses = _ghostQuery.ToComponentDataArray<HullbreachGhostPose>(Allocator.Temp);
            var liveProjectiles = new HashSet<uint>();
            for (int i = 0; i < poses.Length; i++)
            {
                HullbreachGhostPose pose = poses[i];
                if (pose.Kind == (byte)HullbreachGhostKind.Ship)
                {
                    _replica.ObserveSimulationTick(pose.SimTick);
                    HullbreachNetCodeClient.ShipSkinIds[pose.NetId] = pose.Skin;
                    _replica.ApplyGhostPose(pose.NetId, pose.Position, pose.Rotation,
                        pose.Velocity, pose.AngularVelocity, _clientTick);
                }
                else if (pose.Kind == (byte)HullbreachGhostKind.Projectile)
                {
                    liveProjectiles.Add(pose.SimulationId);
                    _projectiles[pose.SimulationId] = new HullbreachNetCodeClient.ProjectileVisual(
                        pose.NetId, pose.Position, pose.Velocity, pose.Radius);
                }
            }

            var stale = new List<uint>();
            foreach (uint id in _projectiles.Keys)
                if (!liveProjectiles.Contains(id)) stale.Add(id);
            foreach (uint id in stale) _projectiles.Remove(id);
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
            _replica.ApplyReceived(message, message.Length);
        }
    }
}
