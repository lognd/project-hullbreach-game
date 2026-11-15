using System;
using System.Collections.Generic;
using Hullbreach.Bots;
using Hullbreach.Builder;
using Hullbreach.Core;
using Hullbreach.Match;
using Hullbreach.Net;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace Hullbreach.NetCode.Entities
{
    /// <summary>Runs ServerSimulation in the Netcode for Entities server world.</summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class HullbreachNetCodeServerSystem : SystemBase
    {
        readonly HashSet<int> _joinedPeers = new HashSet<int>();
        readonly Dictionary<int, Entity> _connections = new Dictionary<int, Entity>();
        readonly Dictionary<int, Entity> _shipGhosts = new Dictionary<int, Entity>();
        readonly Dictionary<uint, Entity> _projectileGhosts = new Dictionary<uint, Entity>();
        ServerOutbox _outbox;
        ServerSimulation _simulation;
        EntityQuery _connectionQuery;
        EntityQuery _inputQuery;
        EntityQuery _buildQuery;
        EntityQuery _designQuery;
        EntityQuery _ticketQuery;
        uint _nextMessageId = 1;
        bool _arenaConfigured;
        bool _sparringJoined;
        BotPeer _bot;
        uint _sparringTick;
        uint _simTick; // counts _simulation.Tick() calls, the same clock as StormStarted.StartTick

        protected override void OnCreate()
        {
            _outbox = new ServerOutbox();
            _simulation = new ServerSimulation(_outbox)
            {
                TickRate = HullbreachNetCodeConstants.SimulationTickRate,
                EmitPoseMessages = false,
                RequireReady = true, // a real build phase: the match starts when both players press READY
            };
            _connectionQuery = GetEntityQuery(
                ComponentType.ReadOnly<NetworkId>(),
                ComponentType.ReadOnly<NetworkStreamConnection>());
            _inputQuery = GetEntityQuery(
                ComponentType.ReadOnly<HullbreachInputRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            _buildQuery = GetEntityQuery(
                ComponentType.ReadOnly<HullbreachBuildRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            _designQuery = GetEntityQuery(
                ComponentType.ReadOnly<HullbreachDesignRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            _ticketQuery = GetEntityQuery(
                ComponentType.ReadOnly<HullbreachJoinTicketRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            ConfigureQueueServer();
            RequireForUpdate<NetworkStreamDriver>();
        }

        // Allocation channel for a dedicated queue server (S29-3c). The platform routes only hand out a match id and an
        // address to the two players, so the server learns the id one of two ways:
        //   -hullbreachMatchId <uuid>  an orchestrator allocated this server to that match (AssignQueueMatch).
        //   -hullbreachQueueServer     nobody told it: the first valid JoinTicket names the match (AdoptFirstTicket).
        // Either flag turns RequireJoinTicket on; without them the server stays open for LAN and bot play.
        // frob:ticket 01M5XACF0K75AVMM0MCNDDWXNE
        // frob:doc docs/netcode.md#queue-connect-flow
        /// <summary>Same as -hullbreachQueueServer, for a server hosted inside the editor (may be set while it runs).</summary>
        public static bool QueueServerRequested;

        void ConfigureQueueServer()
        {
            string[] args = Environment.GetCommandLineArgs();
            string matchId = HullbreachDirectLaunch.Plan.MatchId; // DirectLaunchPlan parses -hullbreachMatchId for servers
            if (matchId == null)
            {
                int idIndex = Array.IndexOf(args, DirectLaunchPlan.MatchIdArg);
                if (idIndex >= 0 && idIndex + 1 < args.Length) matchId = args[idIndex + 1];
            }
            bool queueServer = QueueServerRequested || Array.IndexOf(args, "-hullbreachQueueServer") >= 0;
            if (matchId != null)
            {
                _simulation.RequireJoinTicket = true;
                if (!_simulation.AssignQueueMatch(matchId))
                    Debug.LogWarning("Hullbreach server: -hullbreachMatchId was not a valid match id; no match is allocated.");
            }
            else if (queueServer)
            {
                _simulation.RequireJoinTicket = true;
                _simulation.AdoptFirstTicket = true;
            }
            if (_simulation.RequireJoinTicket) Debug.Log("Hullbreach server: join tickets are required.");
        }

        void ConsumeJoinTickets()
        {
            using var entities = _ticketQuery.ToEntityArray(Allocator.Temp);
            using var tickets = _ticketQuery.ToComponentDataArray<HullbreachJoinTicketRpc>(Allocator.Temp);
            using var sources = _ticketQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity source = sources[i].SourceConnection;
                if (!EntityManager.HasComponent<NetworkId>(source)) continue;
                int peer = EntityManager.GetComponentData<NetworkId>(source).Value;
                bool ok = _simulation.PresentTicket(peer, tickets[i].MatchId.ToString(), tickets[i].Skin.ToString());
                Debug.Log($"Hullbreach server: peer #{peer} join ticket {(ok ? "accepted" : "refused")}.");
            }
            if (entities.Length > 0) EntityManager.DestroyEntity(entities);
        }

        protected override void OnUpdate()
        {
            if (QueueServerRequested && !_simulation.RequireJoinTicket) ConfigureQueueServer();
            ConsumeJoinTickets();
            SynchronizeConnections();
            PrepareGameplayIfLoaded();
            if (!_arenaConfigured) return;
            ConsumeInputs();
            ConsumeBuildRequests();
            ConsumeDesignRequests();
            ManageSparringPartner();
            _simulation.Tick();
            _simTick++;
            DropTimedOutPeers();
            SynchronizeGhosts();
            FlushOutbox();
        }

        void PrepareGameplayIfLoaded()
        {
            if (!SystemAPI.TryGetSingleton<HullbreachGhostPrefab>(out _)) return;

            if (!_arenaConfigured)
            {
                _simulation.ConfigureDemoArena();
                _arenaConfigured = true;
            }

            // Only peers the platform gate admitted get snapshots (docs/platform-link.md#handshake).
            foreach (Entity connection in _connections.Values)
                if (EntityManager.HasComponent<HullbreachPlatformAdmitted>(connection) &&
                    !EntityManager.HasComponent<NetworkStreamInGame>(connection))
                    EntityManager.AddComponent<NetworkStreamInGame>(connection);
        }

        // A bot opponent so one person can play alone: Play vs Bot, the queue's "fight a bot" offer (P0-5) and Cadet Trial
        // (P0-4) all set HullbreachSparring. It is a virtual peer with no connection that joins with a BotBuilder ship, is
        // always ready, is driven by BotPeer every server tick, and leaves as soon as a real second player joins.
        void ManageSparringPartner()
        {
            int peer = HullbreachSparring.PeerId;
            if (_sparringJoined && (_joinedPeers.Count != 1 || !HullbreachSparring.Requested))
            {
                _sparringJoined = false;
                _bot = null;
                _simulation.Leave(peer);
                Debug.Log("Hullbreach server: bot opponent left.");
            }
            else if (!_sparringJoined && HullbreachSparring.Requested && _joinedPeers.Count == 1)
            {
                int human = -1;
                foreach (int p in _joinedPeers) human = p;
                BotDifficulty difficulty = HullbreachSparring.Difficulty;
                ulong seed = (ulong)(Environment.TickCount & 0xFFFFFF) + 1;
                BotShip design = BotBuilder.Build(seed, difficulty, BotPersonality.Balanced);
                ShipSnapshot snapshot = design.ToSnapshot((ushort)peer, new float2(SpawnX(peer), 0f), 0f, float2.zero);
                _sparringJoined = _simulation.Join(peer, snapshot) == JoinResult.Joined;
                if (_sparringJoined)
                {
                    _simulation.SetReady(peer, true); // the bot is always ready; the human's READY starts the match
                    _bot = new BotPeer(peer, human, new BotPilot(BotProfile.For(difficulty, BotPersonality.Balanced), new BotRng(seed)));
                    Debug.Log($"Hullbreach server: bot opponent joined ({difficulty}, {design.BlockCount} blocks).");
                }
            }
            if (_sparringJoined && _bot != null)
            {
                // The bot sits still through the build phase: shots would land before the match has started.
                if (_simulation.Match != null && _simulation.Match.Phase == MatchPhase.Running) _bot.Drive(_simulation, _simTick);
                else _simulation.SetInput(peer, new InputMessage((ushort)peer, ++_sparringTick, 0, 0, 0));
            }
        }

        void SynchronizeConnections()
        {
            _connections.Clear();
            using var entities = _connectionQuery.ToEntityArray(Allocator.Temp);
            using var ids = _connectionQuery.ToComponentDataArray<NetworkId>(Allocator.Temp);
            var active = new HashSet<int>();
            bool gameplayLoaded = SystemAPI.HasSingleton<HullbreachGhostPrefab>();

            for (int i = 0; i < entities.Length; i++)
            {
                int peer = ids[i].Value;
                active.Add(peer);
                _connections[peer] = entities[i];
                bool admitted = EntityManager.HasComponent<HullbreachPlatformAdmitted>(entities[i]);
                bool ticketed = !_simulation.RequireJoinTicket || _simulation.IsTicketed(peer);
                if (gameplayLoaded && admitted && ticketed && _joinedPeers.Add(peer))
                {
                    _simulation.Join(peer, CreateStarterShip(peer));
                    Debug.Log($"Hullbreach server: peer #{peer} joined the match ({_joinedPeers.Count} in match).");
                }
            }

            var disconnected = new List<int>();
            foreach (int peer in _joinedPeers)
                if (!active.Contains(peer)) disconnected.Add(peer);

            foreach (int peer in disconnected)
            {
                _joinedPeers.Remove(peer);
                _simulation.Leave(peer);
                Debug.Log($"Hullbreach server: peer #{peer} left the match ({_joinedPeers.Count} in match).");
                if (_shipGhosts.TryGetValue(peer, out Entity ghost) && EntityManager.Exists(ghost))
                    EntityManager.DestroyEntity(ghost);
                _shipGhosts.Remove(peer);
            }
        }

        // The simulation already removed the ship; here the ghost and the connection go too. _joinedPeers
        // keeps the peer until its connection vanishes, otherwise SynchronizeConnections would rejoin it.
        void DropTimedOutPeers()
        {
            while (_simulation.TryDequeueDroppedPeer(out int peer))
            {
                Debug.Log($"Hullbreach server: peer #{peer} timed out; closing its connection.");
                if (_shipGhosts.TryGetValue(peer, out Entity ghost) && EntityManager.Exists(ghost))
                    EntityManager.DestroyEntity(ghost);
                _shipGhosts.Remove(peer);
                if (_connections.TryGetValue(peer, out Entity connection) && EntityManager.Exists(connection) &&
                    !EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connection))
                    EntityManager.AddComponentData(connection, new NetworkStreamRequestDisconnect
                    {
                        Reason = NetworkStreamDisconnectReason.ConnectionClose,
                    });
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

        // One-click starter ships and the remembered last design: the whole ship is replaced before the match starts.
        void ConsumeDesignRequests()
        {
            using var entities = _designQuery.ToEntityArray(Allocator.Temp);
            if (entities.Length == 0) return;
            using var requests = _designQuery.ToComponentDataArray<HullbreachDesignRpc>(Allocator.Temp);
            using var sources = _designQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity source = sources[i].SourceConnection;
                if (!EntityManager.HasComponent<NetworkId>(source)) continue;
                int peer = EntityManager.GetComponentData<NetworkId>(source).Value;
                PlacementResult result = ShipDesignCodec.TryDecode(requests[i].Design.ToString(), out var design)
                    ? _simulation.ApplyDesign(peer, design)
                    : PlacementResult.BadRequest;
                if (result != PlacementResult.Ok) Debug.Log($"Hullbreach server: peer #{peer} design refused: {result}.");
            }
            EntityManager.DestroyEntity(entities);
        }

        void ConsumeBuildRequests()
        {
            using var entities = _buildQuery.ToEntityArray(Allocator.Temp);
            using var requests = _buildQuery.ToComponentDataArray<HullbreachBuildRpc>(Allocator.Temp);
            using var sources = _buildQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                Entity source = sources[i].SourceConnection;
                if (!EntityManager.HasComponent<NetworkId>(source)) continue;
                int peer = EntityManager.GetComponentData<NetworkId>(source).Value;
                HullbreachBuildRpc request = requests[i];
                // The economy decides: stock, budget, cooldown and rules. The owner gets a PlacementResult
                // message either way (ClientReplica.Economy), so the client can say why a block was refused.
                if (request.Action == (byte)HullbreachBuildAction.Ready)
                {
                    _simulation.SetReady(peer, true);
                    continue;
                }
                PlacementResult result = request.Action == (byte)HullbreachBuildAction.Place
                    ? _simulation.PlaceBlock(peer, request.X, request.Y, request.TypeId, request.Modifiers)
                    : request.Action == (byte)HullbreachBuildAction.Remove
                        ? _simulation.Deconstruct(peer, request.X, request.Y)
                        : PlacementResult.BadRequest;
                if (result != PlacementResult.Ok && result != PlacementResult.Cooldown)
                    Debug.Log($"Hullbreach server: peer #{peer} build refused: {result}.");
            }

            if (entities.Length > 0) EntityManager.DestroyEntity(entities);
        }

        void SynchronizeGhosts()
        {
            if (!SystemAPI.TryGetSingleton<HullbreachGhostPrefab>(out var prefab) || prefab.Value == Entity.Null)
                return;

            var ships = _simulation.Ships;
            foreach (var pair in ships)
            {
                if (!_shipGhosts.TryGetValue(pair.Key, out Entity entity) || !EntityManager.Exists(entity))
                {
                    entity = EntityManager.Instantiate(prefab.Value);
                    _shipGhosts[pair.Key] = entity;
                    if (EntityManager.HasComponent<GhostOwner>(entity))
                        EntityManager.SetComponentData(entity, new GhostOwner { NetworkId = pair.Key });
                }

                var ship = pair.Value;
                EntityManager.SetComponentData(entity, new HullbreachGhostPose
                {
                    Kind = (byte)HullbreachGhostKind.Ship,
                    NetId = (ushort)pair.Key,
                    SimulationId = (uint)pair.Key,
                    SimTick = _simTick,
                    Skin = _simulation.SkinOf(pair.Key),
                    Position = ship.Position,
                    Rotation = ship.Rotation,
                    Velocity = ship.Velocity,
                    AngularVelocity = ship.AngularVelocity,
                    Radius = 0f,
                });
            }

            var liveShips = new HashSet<int>(ships.Keys);
            var staleShips = new List<int>();
            foreach (var pair in _shipGhosts)
                if (!liveShips.Contains(pair.Key)) staleShips.Add(pair.Key);
            foreach (int peer in staleShips)
            {
                if (EntityManager.Exists(_shipGhosts[peer])) EntityManager.DestroyEntity(_shipGhosts[peer]);
                _shipGhosts.Remove(peer);
            }

            IReadOnlyList<ServerSimulation.ProjectileSnapshot> projectiles = _simulation.Projectiles;
            var liveProjectiles = new HashSet<uint>();
            foreach (var projectile in projectiles)
            {
                liveProjectiles.Add(projectile.Id);
                if (!_projectileGhosts.TryGetValue(projectile.Id, out Entity entity) || !EntityManager.Exists(entity))
                {
                    entity = EntityManager.Instantiate(prefab.Value);
                    _projectileGhosts[projectile.Id] = entity;
                    if (EntityManager.HasComponent<GhostOwner>(entity))
                        EntityManager.SetComponentData(entity, new GhostOwner { NetworkId = math.max(0, projectile.OwnerPeer) });
                }

                EntityManager.SetComponentData(entity, new HullbreachGhostPose
                {
                    Kind = (byte)HullbreachGhostKind.Projectile,
                    NetId = (ushort)math.max(0, projectile.OwnerPeer),
                    SimulationId = projectile.Id,
                    SimTick = _simTick,
                    Position = projectile.Position,
                    Rotation = math.atan2(projectile.Velocity.y, projectile.Velocity.x),
                    Velocity = projectile.Velocity,
                    AngularVelocity = 0f,
                    Radius = projectile.Radius,
                });
            }

            var staleProjectiles = new List<uint>();
            foreach (var pair in _projectileGhosts)
                if (!liveProjectiles.Contains(pair.Key)) staleProjectiles.Add(pair.Key);
            foreach (uint id in staleProjectiles)
            {
                if (EntityManager.Exists(_projectileGhosts[id])) EntityManager.DestroyEntity(_projectileGhosts[id]);
                _projectileGhosts.Remove(id);
            }
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

        static float SpawnX(int peer) => ((peer & 1) == 0 ? 1f : -1f) * (3f + (peer % 4) * 2f);

        static ShipSnapshot CreateStarterShip(int peer)
        {
            float x = SpawnX(peer);
            // The balanced starter: a legal, stable ship worth the whole point budget (ShipTemplates), so a fresh player
            // is not outgunned by a bot. The build phase can swap it for another template in one click.
            var design = ShipTemplates.Default.Blocks;
            var blocks = new SnapshotBlock[design.Count];
            for (int i = 0; i < blocks.Length; i++)
                blocks[i] = new SnapshotBlock((sbyte)design[i].X, (sbyte)design[i].Y, design[i].TypeId, design[i].Modifiers, 0);
            return new ShipSnapshot(0, (ushort)peer, blocks,
                Quantization.PackPosition(x), 0, 0, 0, 0, 0);
        }
    }
}
