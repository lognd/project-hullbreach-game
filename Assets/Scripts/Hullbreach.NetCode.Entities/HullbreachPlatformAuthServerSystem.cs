using System.Collections.Generic;
using Hullbreach.Platform;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Hullbreach.NetCode.Entities
{
    // Feeds connections and hello RPCs into PlatformAdmissionGate and applies its
    // decisions: tag admitted connections for HullbreachNetCodeServerSystem, tell
    // every player the verdict, and drop rejected ones shortly after telling them.
    // frob:doc docs/platform-link.md#handshake
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(HullbreachNetCodeServerSystem))]
    public partial class HullbreachPlatformAuthServerSystem : SystemBase
    {
        // Long enough for the admission RPC to leave before the connection closes.
        const double RejectDisconnectDelaySeconds = 0.5;

        readonly Dictionary<int, Entity> _connections = new Dictionary<int, Entity>();
        readonly HashSet<int> _tracked = new HashSet<int>();
        readonly Dictionary<int, double> _pendingDisconnects = new Dictionary<int, double>();
        static HullbreachPlatformAuthServerSystem s_instance;
        PlatformAdmissionGate _gate;
        MatchResultReporter _reporter;
        System.IDisposable _matchEndSubscription;
        double _now;
        EntityQuery _newConnectionQuery;
        EntityQuery _connectionQuery;
        EntityQuery _helloQuery;

        protected override void OnCreate()
        {
            PlatformConfig config = HullbreachPlatformRuntime.Config;
            _gate = new PlatformAdmissionGate(HullbreachPlatformRuntime.Client, config.RequireAuth,
                config.HelloTimeoutSeconds, HullbreachPlatformRuntime.Log);
            _reporter = new MatchResultReporter(HullbreachPlatformRuntime.Client, _gate, HullbreachPlatformRuntime.Log);
            // A match ending on MatchEndFeed.Shared is reported to the platform from here on.
            _matchEndSubscription = _reporter.Attach(MatchEndFeed.Shared, () => _now);
            s_instance = this;
            Debug.Log(config.RequireAuth
                ? "Hullbreach platform: server REQUIRES platform sign-in; unverified players are disconnected."
                : "Hullbreach platform: server allows guests; platform sign-in is optional (pass -hullbreachRequirePlatformAuth to require it).");
            if (config.RequireAuth && !config.HasServerKey)
                Debug.LogWarning($"Hullbreach platform: {PlatformConfig.ServerKeyEnv} is not set; session checks will fail once the platform requires a server key.");

            _newConnectionQuery = GetEntityQuery(
                ComponentType.ReadOnly<NetworkId>(),
                ComponentType.Exclude<HullbreachPlatformTracked>());
            _connectionQuery = GetEntityQuery(ComponentType.ReadOnly<NetworkId>());
            _helloQuery = GetEntityQuery(
                ComponentType.ReadOnly<HullbreachPlatformHelloRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            RequireForUpdate<NetworkStreamDriver>();
        }

        protected override void OnDestroy()
        {
            _matchEndSubscription?.Dispose();
            if (s_instance == this) s_instance = null;
        }

        // Direct entry point; the match-end flow normally raises MatchEndFeed.Shared instead.
        // Reports one finished two-player match. It is a no-op
        // (false) without a game server key, or when either player is a guest or unverified.
        // frob:doc docs/platform-link.md#reporting-a-finished-match
        public static bool ReportFinishedMatch(string matchId, int winnerPeer, int peerA, PlayerMatchStats statsA,
            int peerB, PlayerMatchStats statsB, int durationSeconds)
        {
            if (s_instance == null)
            {
                Debug.LogWarning($"Hullbreach platform: match {matchId} not reported: the platform auth server system is not running.");
                return false;
            }
            return s_instance._reporter.ReportFinished(matchId, winnerPeer, peerA, statsA, peerB, statsB,
                durationSeconds, s_instance._now);
        }

        protected override void OnUpdate()
        {
            double now = SystemAPI.Time.ElapsedTime;
            _now = now;
            TrackConnections(now);
            ConsumeHellos(now);
            _gate.Tick(now);
            _reporter.Tick(now);
            ApplyDecisions(now);
            DisconnectRejected(now);
        }

        void TrackConnections(double now)
        {
            using (var fresh = _newConnectionQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity connection in fresh)
                {
                    int peer = EntityManager.GetComponentData<NetworkId>(connection).Value;
                    EntityManager.AddComponent<HullbreachPlatformTracked>(connection);
                    _tracked.Add(peer);
                    // A reused NetworkId must not inherit the previous holder's pending drop.
                    _pendingDisconnects.Remove(peer);
                    _gate.OnConnected(peer, now);
                }
            }

            _connections.Clear();
            using var entities = _connectionQuery.ToEntityArray(Allocator.Temp);
            using var ids = _connectionQuery.ToComponentDataArray<NetworkId>(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
                _connections[ids[i].Value] = entities[i];

            List<int> gone = null;
            foreach (int peer in _tracked)
                if (!_connections.ContainsKey(peer)) (gone ??= new List<int>()).Add(peer);
            if (gone == null) return;
            foreach (int peer in gone)
            {
                _tracked.Remove(peer);
                _pendingDisconnects.Remove(peer);
                _gate.OnDisconnected(peer);
            }
        }

        void ConsumeHellos(double now)
        {
            using var entities = _helloQuery.ToEntityArray(Allocator.Temp);
            using var hellos = _helloQuery.ToComponentDataArray<HullbreachPlatformHelloRpc>(Allocator.Temp);
            using var requests = _helloQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity source = requests[i].SourceConnection;
                if (!EntityManager.HasComponent<NetworkId>(source)) continue;
                int peer = EntityManager.GetComponentData<NetworkId>(source).Value;
                _gate.OnHello(peer, hellos[i].Token.ToString(), hellos[i].Username.ToString(), now);
            }
            if (entities.Length > 0) EntityManager.DestroyEntity(entities);
        }

        void ApplyDecisions(double now)
        {
            while (_gate.TryTakeDecision(out AdmissionDecision decision))
            {
                if (!_connections.TryGetValue(decision.Peer, out Entity connection)) continue;

                if (decision.Admitted && !EntityManager.HasComponent<HullbreachPlatformAdmitted>(connection))
                    EntityManager.AddComponent<HullbreachPlatformAdmitted>(connection);
                if (!decision.Admitted)
                    _pendingDisconnects[decision.Peer] = now + RejectDisconnectDelaySeconds;

                var rpc = new HullbreachPlatformAdmissionRpc
                {
                    Admitted = decision.Admitted,
                    Verified = decision.Verified,
                };
                rpc.DisplayName.CopyFromTruncated(decision.DisplayName);
                rpc.Reason.CopyFromTruncated(decision.Reason);
                Entity rpcEntity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(rpcEntity, rpc);
                EntityManager.AddComponentData(rpcEntity, new SendRpcCommandRequest { TargetConnection = connection });
            }
        }

        void DisconnectRejected(double now)
        {
            if (_pendingDisconnects.Count == 0) return;
            List<int> due = null;
            foreach (var pair in _pendingDisconnects)
                if (now >= pair.Value) (due ??= new List<int>()).Add(pair.Key);
            if (due == null) return;
            foreach (int peer in due)
            {
                _pendingDisconnects.Remove(peer);
                if (!_connections.TryGetValue(peer, out Entity connection) ||
                    EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connection))
                    continue;
                Debug.Log($"Hullbreach platform: disconnecting rejected peer {peer}.");
                EntityManager.AddComponentData(connection, new NetworkStreamRequestDisconnect
                {
                    Reason = NetworkStreamDisconnectReason.ConnectionClose,
                });
            }
        }
    }
}
