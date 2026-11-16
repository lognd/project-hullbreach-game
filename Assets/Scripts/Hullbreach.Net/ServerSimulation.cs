using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Builder;
using Hullbreach.Match;
using Hullbreach.Platform;
using Hullbreach.Ship;
using Hullbreach.Ship.Behaviours;
using Hullbreach.Structure;
using Hullbreach.World;

namespace Hullbreach.Net
{
    // Outcome of ServerSimulation.Join; anything but Joined leaves no ship behind.
    // frob:doc docs/reference/hullbreach-net.md#joinresult
    public enum JoinResult
    {
        Joined,
        // The transport id does not fit the peer NetId range.
        InvalidPeerId,
        // That peer already has a ship; Leave first (a join never replaces one).
        AlreadyJoined,
        // The supplied design failed DesignValidator.ValidateDesign.
        InvalidDesign,
        // RequireJoinTicket is on and the peer has not presented the allocated match id.
        TicketRejected,
        // The economy is on and the design has more blocks than the size cap.
        DesignTooLarge,
        // The economy is on and the design costs more than the build budget.
        OverBudget,
    }

    // The authoritative, plain-C# server loop; see the reference page.
    // frob:doc docs/reference/hullbreach-net.md#serversimulation
    public sealed partial class ServerSimulation
    {
        // Only used to derive Dt and the input timeout in ticks;
        // ServerSimulation never sleeps or measures wall-clock time itself.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public float TickRate = 50f;

        // Seconds without a fresh SetInput before a peer is dropped and its
        // ship removed (S47 criterion 2).
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public float TimeoutSeconds = 5f;

        // Netcode for Entities ghosts carry high-frequency pose data in the
        // Unity adapter. Plain transports and existing tests keep the legacy
        // ShipState stream enabled by default.
        public bool EmitPoseMessages = true;

        // Benefit of the doubt a shot's target gets on a graze (S49-1); HitTolerance.None disables it.
        // frob:doc docs/reference/hullbreach-net.md#hittolerance
        public HitTolerance Tolerance = HitTolerance.Default;

        // Trust events not yet forwarded by the host (for the platform's admin view).
        readonly Queue<TrustEvent> _trustEvents = new Queue<TrustEvent>();

        // Trust 0..1 of a joined peer, or -1 when unknown.
        // frob:doc docs/reference/hullbreach-net.md#trustlevel
        public float TrustOf(int peer) => _peers.TryGetValue(peer, out var state) ? state.Trust.Value : -1f;

        // Pops the oldest trust event so the host can log or report it; each event is also
        // written to MatchLog when one is set.
        // frob:doc docs/reference/hullbreach-net.md#trustlevel
        public bool TryDequeueTrustEvent(out TrustEvent evt)
        {
            if (_trustEvents.Count > 0) { evt = _trustEvents.Dequeue(); return true; }
            evt = default;
            return false;
        }

        void Distrust(int peer, PeerState state, TrustEventKind kind)
        {
            float before = state.Trust.Penalize();
            var evt = new TrustEvent(peer, kind, _tickIndex, before, state.Trust.Value);
            if (_trustEvents.Count >= 256) _trustEvents.Dequeue();
            _trustEvents.Enqueue(evt);
            MatchLog?.Warning($"Hullbreach trust: peer #{peer} {kind} at tick {_tickIndex}; trust {before:0.00} -> {state.Trust.Value:0.00}.");
        }

        // Match length in seconds of simulated time; 0 or less disables the limit (default 10 minutes).
        // frob:ticket 01M3DG5Y36WE3D2224D54MR2S6
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public double MatchTimeLimitSeconds = 600.0;

        // When true a full lobby waits for both peers to send ReadyUp before the match starts (S28-3);
        // false keeps the legacy start-on-second-join behaviour.
        // frob:ticket 01M3DG5Y1NW8M51ENVBT7EVKQY
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public bool RequireReady;

        /// <summary>
        /// With RequireReady, nobody can shoot for this long after the match starts, so a player who just pressed READY can
        /// finish building or switch to flight before the opponent (a bot included) opens fire. 0 turns the grace off.
        /// </summary>
        public float ShotGraceSeconds = 5f;
        uint _matchStartTick;

        // Where a finished match is announced; defaults to the shared feed the platform reporter watches.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public MatchEndFeed MatchFeed = MatchEndFeed.Shared;

        // Tracks who has readied while RequireReady is on; replaced once its match has ended.
        ReadyGate _gate = new ReadyGate();

        // The match whose end was already sent to the clients, so each end is announced once.
        MatchStateMachine _announcedMatch;

        // Sink for match lifecycle lines; null means silent.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public IPlatformLog MatchLog;

        float Dt => 1f / TickRate;

        // The current (or last) match; null until two peers have joined.
        // frob:ticket 01M5V9XQ3YKRVFW9RMPZTVN11E
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public MatchStateMachine Match { get; private set; }

        // Queue match id the next match is reported under; null means a fresh random uuid.
        string _queueMatchId;

        // Names the next match with the platform's queue id (S29-3); false when the id is bad or a match is live.
        // frob:ticket 01M3DG5Y1Q243GK60T3RHV8HRV
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public bool AssignQueueMatch(string matchId)
        {
            ConfigResult<string> id = QueueMatchId.Normalize(matchId);
            if (!id.IsOk)
            {
                MatchLog?.Warning($"Hullbreach match: queue match id rejected: {id.Error}.");
                return false;
            }
            if (Match != null && Match.Phase != MatchPhase.Ended)
            {
                MatchLog?.Warning($"Hullbreach match: queue match id {id.Value} ignored; a match is already live.");
                return false;
            }
            _queueMatchId = id.Value;
            MatchLog?.Info($"Hullbreach match: next match will be reported as {id.Value}.");
            return true;
        }

        // When true (a dedicated queue server) a peer must present the allocated match id before Join
        // is accepted; false keeps LAN and test servers open. Default false.
        // frob:ticket 01M5XACF0K75AVMM0MCNDDWXNE
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public bool RequireJoinTicket;

        /// <summary>
        /// Queue servers without an allocation channel: when no match id was assigned, the first valid JoinTicket names the
        /// match (see PresentTicket). Default false.
        /// </summary>
        // frob:ticket 01M5XACF0K75AVMM0MCNDDWXNE
        public bool AdoptFirstTicket;

        /// <summary>True when the peer's JoinTicket matched the allocated match id (the host joins only such peers when RequireJoinTicket is on).</summary>
        // frob:ticket 01M5XACF0K75AVMM0MCNDDWXNE
        public bool IsTicketed(int peer) => _ticketed.Contains(peer);

        // Peers whose JoinTicket matched the allocated id; cleared when they leave.
        readonly HashSet<int> _ticketed = new HashSet<int>();

        // Handles a peer's JoinTicket. True when it matches the allocated id, or when none is allocated
        // and tickets are not required (nothing to check). A wrong, malformed or unexpected id queues the
        // peer for disconnect and returns false.
        // frob:ticket 01M5XACF0K75AVMM0MCNDDWXNE
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public bool PresentTicket(int peer, string matchId, string skin = null, string identity = null)
        {
            bool ok = CheckTicket(peer, matchId);
            if (ok)
            {
                _skins[peer] = ShipSkins.IdOf(skin);
                SetIdentity(peer, identity);
            }
            return ok;
        }

        /// <summary>
        /// Names the player behind a connection (the platform user id from the join ticket, or address plus nick on a
        /// LAN) so a reconnect under a new peer id can claim the ship held for them. Null or empty clears it. A peer
        /// with no identity can still reclaim the one held ship when exactly one is held (a two-player match).
        /// </summary>
        // frob:ticket 01M7FJ5KYZDQ37M8MC4CEV1DG7
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public void SetIdentity(int peer, string identity)
        {
            if (string.IsNullOrEmpty(identity)) _identities.Remove(peer);
            else _identities[peer] = identity;
        }

        readonly Dictionary<int, string> _identities = new Dictionary<int, string>();

        // The skin id (ShipSkins) a peer presented with its ticket; 0 (default) when none, unknown, or a bot.
        // frob:ticket 01M5XACF0K75AVMM0MCNDDWXNE
        public byte SkinOf(int peer) => _skins.TryGetValue(peer, out byte id) ? id : (byte)0;

        readonly Dictionary<int, byte> _skins = new Dictionary<int, byte>();

        bool CheckTicket(int peer, string matchId)
        {
            ConfigResult<string> presented = QueueMatchId.Normalize(matchId);
            // No orchestrator told this server the match id: the first well-formed ticket names the match, and the
            // opponent's ticket must then carry the same id (the platform hands both players one match_id).
            if (AdoptFirstTicket && presented.IsOk && _queueMatchId == null) AssignQueueMatch(presented.Value);
            if (!RequireJoinTicket && _queueMatchId == null) return presented.IsOk;
            if (presented.IsOk && _queueMatchId != null && presented.Value == _queueMatchId)
            {
                _ticketed.Add(peer);
                return true;
            }
            MatchLog?.Warning($"Hullbreach match: peer #{peer} presented a wrong or unexpected match id; disconnecting.");
            _ticketed.Remove(peer);
            if (!_droppedPeers.Contains(peer)) _droppedPeers.Enqueue(peer);
            return false;
        }

        readonly GravityField _gravity = new GravityField();

        // Exposed so a test/host can add permanent bodies (planets) before
        // the first Tick.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public GravityField Gravity => _gravity;

        // How long the last axes keep applying when no newer input arrives (late or dropped packets).
        // frob:ticket 01M6WV815GYEJ58MTM4HSS4Z6K
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public const float InputHoldSeconds = 0.25f;

        sealed partial class PeerState
        {
            public ushort NetId;
            public ShipBody Ship;
            public StructuralSolver Solver;
            public InputMessage LatestInput;
            public float SecondsSinceInput;
            public bool IsBuilding;
            public bool HasInput;
            public bool PendingFire;
            // Block damage at the start of this tick step; see SnapshotDamage.
            public readonly Dictionary<int, byte> DamageBeforeStep = new Dictionary<int, byte>();
            public uint NextBuildTick;
            public bool InPushZone;
            public readonly TrustLevel Trust = new TrustLevel();
            public readonly Dictionary<int, bool> Failing = new Dictionary<int, bool>();
            // Storm damage below one whole point, per block key, carried to the next tick.
            public readonly Dictionary<int, float> StormCarry = new Dictionary<int, float>();
            // Disconnected but kept for the reconnect grace window; see Disconnect.
            public bool Held;
            public uint HeldUntilTick;
        }

        /// <summary>
        /// How long, in seconds, a ship is kept after its peer's connection drops (Disconnect) so the same player can
        /// rejoin with the ship and stock intact. Converted to ticks when the drop happens, so it is deterministic.
        /// 0 disables the grace period (a drop then behaves like Leave). Default 20.
        /// </summary>
        // frob:ticket 01M7FJ5KYZDQ37M8MC4CEV1DG7
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public float ReconnectGraceSeconds = DefaultReconnectGraceSeconds;

        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public const float DefaultReconnectGraceSeconds = 20f;

        // Per-peer counters for the match report. They live outside PeerState so the report
        // can still be read after the peer's ship is removed (forfeit on leave).
        sealed class MatchTally
        {
            public uint StartTick;
            public int DamageDealt;
            public int BlocksDestroyed;
            public int BlocksPlaced;
        }

        readonly Dictionary<int, PeerState> _peers = new Dictionary<int, PeerState>();
        readonly Dictionary<int, MatchTally> _tallies = new Dictionary<int, MatchTally>();
        readonly IServerOutbox _outbox;

        uint _sequence;
        uint _tickIndex;
        bool _projectilesOnWire;

        // Peers own NetIds 1..MaxPeerId (the transport id itself); fragments
        // cycle through FirstFragmentId..65535. Disjoint ranges mean a peer
        // ship and a fragment can never share a NetId.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public const int MaxPeerId = 0x7FFF;

        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public const ushort FirstFragmentId = 0x8000;

        ushort _nextFragmentId = FirstFragmentId;

        // Peers dropped by timeout, for the host to disconnect.
        readonly Queue<int> _droppedPeers = new Queue<int>();

        sealed class ProjectileBody
        {
            public uint Id;
            public int OwnerPeer;
            public float2 Position;
            public float2 Velocity;
            public ProjectileSpec Spec;
            public float LifeRemaining;
        }

        readonly List<ProjectileBody> _projectiles = new List<ProjectileBody>();
        uint _nextProjectileId = 1;

        public readonly struct ProjectileSnapshot
        {
            public readonly uint Id;
            public readonly int OwnerPeer;
            public readonly float2 Position;
            public readonly float2 Velocity;
            public readonly float Radius;

            public ProjectileSnapshot(uint id, int ownerPeer, float2 position, float2 velocity, float radius)
            {
                Id = id;
                OwnerPeer = ownerPeer;
                Position = position;
                Velocity = velocity;
                Radius = radius;
            }
        }

        // This server's own IWorldSink; see the reference page.
        readonly ServerWorldSink _sink;

        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public ServerSimulation(IServerOutbox outbox)
        {
            _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
            _sink = new ServerWorldSink(this);
        }

        // Test- and diagnostics-only accessor; gameplay code should not need it.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public IReadOnlyDictionary<int, ShipBody> Ships
        {
            get
            {
                var result = new Dictionary<int, ShipBody>();
                foreach (var kv in _peers) result[kv.Key] = kv.Value.Ship;
                return result;
            }
        }

        public IReadOnlyList<ProjectileSnapshot> Projectiles
        {
            get
            {
                var result = new List<ProjectileSnapshot>(_projectiles.Count);
                foreach (var p in _projectiles)
                    result.Add(new ProjectileSnapshot(p.Id, p.OwnerPeer, p.Position, p.Velocity, p.Spec.Radius));
                return result;
            }
        }

        // Shrinking-ring schedule for the next match; its tick rate is replaced by TickRate when the match starts.
        // frob:ticket 01M7EXAGAAS0CRAVC1BEXJ5598
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public StormConfig Storm = StormConfig.Default();

        // False runs matches with no storm (no damage, no despawn, no StormStarted message).
        // frob:ticket 01M7EXAGAAS0CRAVC1BEXJ5598
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public bool StormEnabled = true;

        // The live match storm; null before a match starts or when StormEnabled is off.
        // frob:ticket 01M7EXAGAAS0CRAVC1BEXJ5598
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public StormZone ActiveStorm { get; private set; }

        // Server tick the storm clock counts from (the tick the match started on).
        uint _stormStartTick;

        // What the live storm was announced with, kept so a peer who joins mid-match can be told again.
        // frob:ticket 01M7FJ5MGNZ0RSXJ5AWKZEE3R4
        uint _stormSeed;
        StormConfig _stormConfig;

        // The two peers the running match was announced for (RequireReady only); resent to a mid-match joiner.
        int _matchPeerA, _matchPeerB;

        // Planet centres the final ring may drift toward, from the arena config.
        float2[] _stormCandidates = new float2[0];

        // Stable 32-bit seed from a match id (FNV-1a), so the server and a replay agree on the ring.
        // frob:ticket 01M7EXAGAAS0CRAVC1BEXJ5598
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public static uint StormSeedFor(string matchId)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (char c in matchId ?? string.Empty) { h ^= c; h *= 16777619u; }
                return h;
            }
        }

        // Soft arena edge applied each tick; replaced by ConfigureDemoArena (S41-1).
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public ArenaBounds Bounds { get; private set; } = ArenaConfig.DefaultBounds;

        // Crash, atmosphere and warning tunables every ship on this server uses; replaced by ConfigureDemoArena.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public PlanetCrashConfig PlanetCrash { get; private set; } = PlanetCrashConfig.Default();

        // How every joining ship flies: flight assist, ramps, brake, fire buffer. The client prediction uses the
        // same values (ClientReplica.PredictionFlightTuning) so both agree.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public FlightTuning FlightTuning { get; set; } = FlightTuning.Default();

        // Diagnostics sink for arena edge events; defaults to a no-op.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public IPlatformLog Log { get; set; } = NullPlatformLog.Instance;

        // Sets up the built-in arena; same as ConfigureDemoArena(ArenaConfig.Defaults()).
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public void ConfigureDemoArena() => ConfigureDemoArena(ArenaConfig.Defaults());

        // Builds the gravity field from config and puts every ship in orbit around Bodies[0].
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public void ConfigureDemoArena(ArenaConfig config)
        {
            _gravity.Clear();
            Bounds = config.Bounds;
            Storm = config.Storm;
            PlanetCrash = config.Crash;
            foreach (var peerState in _peers.Values) peerState.Ship.PlanetCrash = PlanetCrash;
            Economy = config.Economy;
            _stormCandidates = new float2[config.Bodies.Count];
            for (int i = 0; i < _stormCandidates.Length; i++)
                _stormCandidates[i] = new float2(config.Bodies[i].X, config.Bodies[i].Y);
            Log.Info("arena bounds radius=" + Bounds.Radius + " margin=" + Bounds.Margin
                + " strength=" + Bounds.Strength);
            foreach (ArenaBodyConfig body in config.Bodies) _gravity.Add(body.ToGravityBody());

            ArenaBodyConfig primary = config.Bodies[0];
            var center = new float2(primary.X, primary.Y);
            var peers = new List<int>(_peers.Keys);
            peers.Sort();
            for (int i = 0; i < peers.Count; i++)
            {
                ShipBody ship = _peers[peers[i]].Ship;
                float angle = i * math.PI * config.SpawnAngleStep;
                float2 relative = new float2(math.sin(angle) * config.SpawnLateral,
                    config.SpawnAltitude + math.cos(angle) * config.SpawnVertical);
                ship.Position = center + relative;
                float speed = math.sqrt(primary.Mu / math.max(1f, math.length(relative)));
                float2 tangent = math.normalizesafe(new float2(-relative.y, relative.x));
                ship.Velocity = tangent * speed;
                ship.Rotation = angle;
                ship.AngularVelocity = 0f;
            }
        }

        // Shared with the client HUD so its countdown matches the server rule.
        public const float DefaultBuildCooldownSeconds = 1.5f;

        // Seconds a peer must wait between block placements while a match is running, so building
        // mid-fight is a trade-off (S34-3). Zero or less disables it; pre-match building is never limited.
        // frob:ticket 01M3DG5Y25YGZ3DV7JSDB4BNMF
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public float BuildCooldownSeconds = DefaultBuildCooldownSeconds;

        // Seconds until the peer may place again during a live match; 0 when ready or unknown.
        // frob:ticket 01M3DG5Y25YGZ3DV7JSDB4BNMF
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public float BuildCooldownRemaining(int peer)
        {
            if (!_peers.TryGetValue(peer, out var state) || !MatchIsRunning) return 0f;
            long left = (long)state.NextBuildTick - _tickIndex;
            return left <= 0 ? 0f : left * Dt;
        }

        bool MatchIsRunning => Match != null && Match.Phase == MatchPhase.Running;

        // A placement refused for cooldown is not distrusted: a client can legitimately press twice.
        // frob:ticket 01M3DG5Y25YGZ3DV7JSDB4BNMF
        public bool TryPlaceBlock(int peer, int x, int y, byte typeId, byte modifiers)
            => PlaceBlock(peer, x, y, typeId, modifiers) == PlacementResult.Ok;

        // Same as Deconstruct; kept under its old name for the NetCode server system.
        public bool TryRemoveBlock(int peer, int x, int y)
            => Deconstruct(peer, x, y) == PlacementResult.Ok;

        // Validates the untrusted design, then re-broadcasts the new snapshot
        // to every other peer; see reference page. Rejections change nothing.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        // frob:invariant INV-003
        public JoinResult Join(int peer, ShipSnapshot initialDesign)
        {
            if (peer < 1 || peer > MaxPeerId) return JoinResult.InvalidPeerId;
            if (_peers.ContainsKey(peer)) return JoinResult.AlreadyJoined;
            if (RequireJoinTicket && !_ticketed.Contains(peer)) return JoinResult.TicketRejected;
            // A returning player takes over the ship held for them; the submitted design is ignored.
            if (TryFindHeldFor(peer, out int heldPeer)) return Reattach(heldPeer, peer);
            if (DesignValidator.ValidateDesign(initialDesign) != DesignVerdict.Ok) return JoinResult.InvalidDesign;
            JoinResult? tooBig = CheckDesignAgainstEconomy(initialDesign);
            if (tooBig.HasValue) return tooBig.Value;

            // Runs before adding the newcomer to _peers; see reference page.
            // Stamped with the current sequence, not a fresh one: only the
            // joiner receives these, so a fresh number would leave a gap
            // in everyone else's ordered stream.
            foreach (var kv in _peers)
                EmitReliable(peer, BuildSnapshot(kv.Value, _sequence));

            var ship = new ShipBody
            {
                Gravity = _gravity,
                World = _sink,
                PlanetCrash = PlanetCrash,
                Tuning = FlightTuning,
            };

            foreach (var b in initialDesign.Blocks)
            {
                int key = BlockKey.Pack(b.X, b.Y);
                // Damage is never taken from the client.
                ship.Grid.TryAdd(key, new Block(b.TypeId, b.Mods, 0));
            }
            ship.Position = new float2(Quantization.UnpackPosition(initialDesign.Px), Quantization.UnpackPosition(initialDesign.Py));
            ship.Rotation = Quantization.UnpackAngle(initialDesign.Rot);
            float2 velocity = new float2(Quantization.UnpackPosition(initialDesign.Vx), Quantization.UnpackPosition(initialDesign.Vy));
            float speed = math.length(velocity);
            if (speed > DesignValidator.MaxSpawnSpeed) velocity *= DesignValidator.MaxSpawnSpeed / speed;
            ship.Velocity = velocity;
            ship.AngularVelocity = Quantization.UnpackAngle(initialDesign.Av);
            ship.RebuildDerivedViews();

            var state = new PeerState
            {
                NetId = (ushort)peer,
                Ship = ship,
                Solver = new StructuralSolver
                {
                    LoadScale = StructuralSolver.DefaultLoadScale,
                    MaterialStiffnessScale = StructuralSolver.DefaultMaterialStiffnessScale,
                },
                SecondsSinceInput = 0f,
            };
            _peers[peer] = state;

            // Includes the joiner itself; see the reference page.
            var snapshot = BuildSnapshot(state, NextSequence());
            foreach (var kv in _peers) EmitReliable(kv.Key, snapshot);
            if (RequireReady) TrackForReady(peer);
            SendLateJoinState(peer);
            StartMatchIfReady();
            return JoinResult.Joined;
        }

        // Builds and starts a fresh machine once exactly two peers are in and no match is live.
        // The clock is simulated time (ticks * Dt), so no wall clock leaks into the server.
        // frob:ticket 01M5V9XQ3YKRVFW9RMPZTVN11E
        void StartMatchIfReady()
        {
            if (_peers.Count != 2) return;
            if (Match != null && Match.Phase != MatchPhase.Ended) return;
            if (RequireReady && _gate.State != ReadyGateState.Started) return;

            var ids = new List<int>(_peers.Keys);
            ids.Sort();
            _tallies.Clear();
            foreach (int id in ids) _tallies[id] = new MatchTally { StartTick = _tickIndex };
            _matchStartTick = _tickIndex;
            string queueId = _queueMatchId;
            _queueMatchId = null;
            Match = new MatchStateMachine(
                ParticipantFor(ids[0]), ParticipantFor(ids[1]),
                () => _tickIndex * (double)Dt, MatchFeed, queueId == null ? (Func<string>)null : () => queueId,
                MatchLog, MatchTimeLimitSeconds);
            if (!Match.Start()) return;
            _matchPeerA = ids[0]; _matchPeerB = ids[1];
            if (RequireReady) AnnounceMatchStart(ids[0], ids[1]);
            StartStorm();
            StartEconomy();
        }

        // Builds the ring from the match id and tells every peer how to rebuild it.
        // frob:ticket 01M7EXAGAAS0CRAVC1BEXJ5598
        void StartStorm()
        {
            ActiveStorm = null;
            if (!StormEnabled) return;
            StormConfig config = Storm.WithTickRate(TickRate);
            string problem = config.Validate();
            if (problem != null)
            {
                MatchLog?.Warning($"Hullbreach storm: disabled for this match, bad config: {problem}.");
                return;
            }
            uint seed = StormSeedFor(Match.MatchId);
            _stormStartTick = _tickIndex;
            ActiveStorm = new StormZone(config, Bounds.Center, _stormCandidates, seed);
            _stormSeed = seed;
            _stormConfig = config;
            BroadcastStormStarted();
        }

        // Sends the live storm's announcement to every peer; the same seed and start tick rebuild the same ring.
        void BroadcastStormStarted()
        {
            var message = new StormStarted(NextSequence(), _stormSeed, _stormStartTick, Bounds.Center, _stormCandidates, _stormConfig);
            foreach (int peer in _peers.Keys)
            {
                var buffer = new byte[StormStarted.MaxByteSize];
                var w = new ByteWriter(buffer);
                if (!message.TryWrite(ref w)) return;
                SendTo(peer, reliable: true, buffer, w.Position);
            }
        }

        // A peer joining a match that is already running (a reconnect included) missed the one-shot announcements, so
        // it is told the current state: match phase, storm, every pickup in the field and its own stock. The messages are
        // sequenced like any other, so they go to everyone; each is idempotent on a client that already knows it.
        // Nothing is sent before a match starts, when the normal start-of-match announcements cover everyone.
        // frob:ticket 01M7FJ5MGNZ0RSXJ5AWKZEE3R4
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        void SendLateJoinState(int peer)
        {
            if (!MatchIsRunning) return;
            if (RequireReady) AnnounceMatchStart(_matchPeerA, _matchPeerB);
            if (ActiveStorm != null) BroadcastStormStarted();
            if (_pickups != null)
                foreach (Pickup p in _pickups.Active)
                    BroadcastReliable(new PickupSpawned(NextSequence(), p));
            if (Economy.Enabled && _peers.TryGetValue(peer, out PeerState state))
            {
                state.InventoryDirty = false;
                SendInventory(peer, state);
            }
        }

        // Adds the joiner to the gate; a gate whose match already ended is replaced by a fresh one.
        // frob:ticket 01M3DG5Y1NW8M51ENVBT7EVKQY
        void TrackForReady(int peer)
        {
            if (_gate.State == ReadyGateState.Started && (Match == null || Match.Phase == MatchPhase.Ended))
                _gate = new ReadyGate(2, MatchLog);
            foreach (int id in _peers.Keys) _gate.AddPeer(id);
        }

        // Handles a peer's ReadyUp; the second ready starts the match and tells both peers.
        // Ignored (false) when ready-up is off, the peer has no ship, or the gate already started.
        // frob:ticket 01M3DG5Y1NW8M51ENVBT7EVKQY
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public bool SetReady(int peer, bool ready)
        {
            if (!RequireReady || !_peers.ContainsKey(peer)) return false;
            if (_gate.SetReady(peer, ready) != ReadyResult.Accepted) return false;
            StartMatchIfReady();
            return true;
        }

        // Tells every connected peer, reliably and in order, that the match is running.
        // frob:ticket 01M3DG5Y1NW8M51ENVBT7EVKQY
        void AnnounceMatchStart(int peerA, int peerB)
        {
            var message = new MatchStarted(NextSequence(), peerA, peerB);
            foreach (int peer in _peers.Keys)
            {
                var buffer = new byte[MatchStarted.ByteSize];
                var w = new ByteWriter(buffer);
                message.Write(ref w);
                SendTo(peer, reliable: true, buffer, w.Position);
            }
        }

        // Stats come from the match tally; the reporter fills in the user id from the gate.
        // Time alive is the match length: a ship is alive until the match ends.
        // frob:ticket 01M5V9XQ3YKRVFW9RMPZTVN11E
        // frob:ticket 01M5VA5A9YR599CEWSFN3QWRNB
        MatchParticipant ParticipantFor(int peer)
        {
            var tally = _tallies[peer];
            return new MatchParticipant(peer, _peers[peer].Ship.Grid, () => new PlayerMatchStats(
                string.Empty, tally.DamageDealt, tally.BlocksDestroyed, tally.BlocksPlaced,
                (int)Math.Round((_tickIndex - tally.StartTick) * (double)Dt)));
        }

        // Adds to a peer's tally only while a match is live; no-op for unknown peers or no match.
        // frob:ticket 01M5VA5A9YR599CEWSFN3QWRNB
        void Tally(int peer, int damage, int destroyed, int placed)
        {
            if (Match == null || Match.Phase != MatchPhase.Running) return;
            if (!_tallies.TryGetValue(peer, out var tally)) return;
            tally.DamageDealt += damage;
            tally.BlocksDestroyed += destroyed;
            tally.BlocksPlaced += placed;
        }

        // A transport drop, as opposed to Leave: while a match is running the ship and its stock are kept for
        // ReconnectGraceSeconds, unpiloted, still damageable and still in the storm. Returns true when the ship is held.
        // Outside a running match, with no grace configured, or for an unknown peer it behaves like Leave.
        // frob:ticket 01M7FJ5KYZDQ37M8MC4CEV1DG7
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public bool Disconnect(int peer)
        {
            if (_peers.TryGetValue(peer, out var state) && state.Held) return true;
            if (state == null || ReconnectGraceSeconds <= 0f || !MatchIsRunning)
            {
                Leave(peer);
                return false;
            }
            state.Held = true;
            state.HeldUntilTick = _tickIndex + (uint)Math.Ceiling(ReconnectGraceSeconds * TickRate);
            state.HasInput = false;
            state.LatestInput = default;
            state.PendingFire = false;
            state.IsBuilding = false;
            state.SecondsSinceInput = 0f;
            _ticketed.Remove(peer);
            MatchLog?.Warning($"Hullbreach match: peer #{peer} dropped; holding its ship for {ReconnectGraceSeconds:0.#}s.");
            return true;
        }

        /// <summary>True while the peer's ship is kept after a drop and not yet claimed or expired.</summary>
        // frob:ticket 01M7FJ5KYZDQ37M8MC4CEV1DG7
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public bool IsHeld(int peer) => _peers.TryGetValue(peer, out var s) && s.Held;

        // The held ship the joining peer may claim: same identity, or (either side anonymous) the only one held.
        bool TryFindHeldFor(int peer, out int heldPeer)
        {
            heldPeer = -1;
            int heldCount = 0, anonymous = -1;
            _identities.TryGetValue(peer, out string mine);
            var ids = new List<int>(_peers.Keys);
            ids.Sort();
            foreach (int id in ids)
            {
                if (!_peers[id].Held) continue;
                heldCount++;
                _identities.TryGetValue(id, out string theirs);
                if (mine != null && theirs != null)
                {
                    if (mine != theirs) continue;
                    heldPeer = id;
                    return true;
                }
                anonymous = id;
            }
            if (heldCount == 1 && anonymous >= 0) { heldPeer = anonymous; return true; }
            return false;
        }

        // Moves the held ship to the new peer id (the match, tally and projectiles follow) and tells everyone: the old
        // NetId is removed and the ship reappears under the new one, then the joiner gets the late-join snapshot.
        JoinResult Reattach(int oldPeer, int newPeer)
        {
            PeerState state = _peers[oldPeer];
            _peers.Remove(oldPeer);
            _identities.Remove(oldPeer);
            _skins.Remove(oldPeer);
            _ticketed.Remove(oldPeer);
            state.Held = false;
            state.NetId = (ushort)newPeer;
            state.HasInput = false;
            state.SecondsSinceInput = 0f;
            BroadcastReliable(new ShipRemoved(NextSequence(), (ushort)oldPeer));
            foreach (var kv in _peers)
                EmitReliable(newPeer, BuildSnapshot(kv.Value, _sequence));
            _peers[newPeer] = state;
            if (_tallies.TryGetValue(oldPeer, out var tally)) { _tallies.Remove(oldPeer); _tallies[newPeer] = tally; }
            if (_matchPeerA == oldPeer) _matchPeerA = newPeer;
            if (_matchPeerB == oldPeer) _matchPeerB = newPeer;
            Match?.Rekey(oldPeer, newPeer);
            foreach (var p in _projectiles)
                if (p.OwnerPeer == oldPeer) p.OwnerPeer = newPeer;
            var snapshot = BuildSnapshot(state, NextSequence());
            foreach (var kv in _peers) EmitReliable(kv.Key, snapshot);
            SendLateJoinState(newPeer);
            MatchLog?.Info($"Hullbreach match: peer #{newPeer} rejoined and took over the ship held for #{oldPeer}.");
            return JoinResult.Joined;
        }

        // Frees held ships whose window ran out, and any left over once the match is no longer running (the player
        // is gone, so there is nothing to wait for). Expiry forfeits through RemovePeer like a plain leave.
        void ExpireHeldPeers()
        {
            List<int> gone = null;
            foreach (var kv in _peers)
                if (kv.Value.Held && (_tickIndex >= kv.Value.HeldUntilTick || !MatchIsRunning))
                    (gone ??= new List<int>()).Add(kv.Key);
            if (gone == null) return;
            gone.Sort();
            foreach (int peer in gone) RemovePeer(peer, _peers[peer]);
        }

        // Safe to call for an unknown peer (no-op); tells the others the ship is gone. A deliberate leave: nothing is held.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public void Leave(int peer)
        {
            if (_peers.TryGetValue(peer, out var state)) RemovePeer(peer, state);
            else
            {
                // Connected and ticketed but never joined: forget the ticket and skin too.
                _ticketed.Remove(peer);
                _skins.Remove(peer);
                _identities.Remove(peer);
            }
        }

        // Pops one peer dropped by timeout so the host can close its connection.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public bool TryDequeueDroppedPeer(out int peer)
        {
            if (_droppedPeers.Count > 0) { peer = _droppedPeers.Dequeue(); return true; }
            peer = -1;
            return false;
        }

        // Removes the ship, orphans its projectiles, and broadcasts ShipRemoved.
        void RemovePeer(int peer, PeerState state)
        {
            _peers.Remove(peer);
            _ticketed.Remove(peer);
            _skins.Remove(peer);
            _identities.Remove(peer);
            _gate.RemovePeer(peer);
            Match?.Disconnect(peer);
            AnnounceMatchEnd();
            foreach (var p in _projectiles)
                if (p.OwnerPeer == peer) p.OwnerPeer = -1;
            BroadcastReliable(new ShipRemoved(NextSequence(), state.NetId));
        }

        // Latest-wins for the axes, but a fire press is latched until the
        // next Tick consumes it, and an input older than the last accepted one
        // is ignored. See reference page.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        // frob:invariant INV-003
        public void SetInput(int peer, InputMessage input)
        {
            if (!_peers.TryGetValue(peer, out var state)) return;
            // An older tick is honest reordering or lag, never a trust event.
            if (state.HasInput && unchecked((int)(input.Tick - state.LatestInput.Tick)) < 0) return;
            if ((input.Flags & ~(InputMessage.FireBit | InputMessage.BuildModeBit | InputMessage.AssistOffBit)) != 0)
                Distrust(peer, state, TrustEventKind.ReservedInputBits);
            state.LatestInput = input;
            state.HasInput = true;
            state.IsBuilding = input.BuildMode;
            state.PendingFire |= input.FirePressed;
            state.SecondsSinceInput = 0f;
        }

        // Advances every ship by one fixed tick, in deterministic order.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public void Tick()
        {
            _tickIndex++;
            float dt = Dt;

            TimeoutStalePeers(dt);
            ExpireHeldPeers();
            _gravity.Tick(dt);
            StepProjectiles(dt);

            var peerIds = new List<int>(_peers.Keys);
            peerIds.Sort();

            foreach (int peer in peerIds)
            {
                var state = _peers[peer];
                // A late packet keeps the last axes for InputHoldSeconds instead of cutting thrust.
                // frob:ticket 01M6WV815GYEJ58MTM4HSS4Z6K
                state.Trust.Recover(dt);
                // Less trust, shorter hold: a flaky-but-honest peer keeps most of it, a distrusted one little.
                bool held = state.HasInput && state.SecondsSinceInput <= InputHoldSeconds * (0.2f + 0.8f * state.Trust.Value);
                var input = held
                    ? new ShipInput(state.LatestInput.ThrustAxisFloat, state.LatestInput.SteerFloat, state.PendingFire, state.LatestInput.AssistOff)
                    : new ShipInput(0f, 0f, false);
                state.PendingFire = false;

                if (state.IsBuilding) continue;

                SnapshotDamage(state);
                state.Ship.Step(input, dt);
                ReportBodyDamage(peer, state);
                ApplyArenaPush(peer, state, dt);
                DrainShots(peer, state.Ship);
            }

            ResolveShipContacts(peerIds, dt);

            foreach (int peer in peerIds)
            {
                var state = _peers[peer];
                if (state.IsBuilding) continue;

                var grid = state.Ship.Grid;
                if (grid.Count == 0) continue;

                state.Solver.Tick(grid, state.Ship.AppliedForcesThisStep, dt);
                ResolveStructuralFailures(peer, state);
            }

            ApplyStorm(peerIds);
            TickEconomy(peerIds);

            foreach (int peer in peerIds)
            {
                if (!_peers.TryGetValue(peer, out var state)) continue; // may have been removed by a timeout above
                if (state.Ship.Grid.Count == 0) continue;
                if (EmitPoseMessages) EmitUnreliable(peer, BuildState(state));
            }

            // One message per peer so a late joiner sees shots already in flight.
            // frob:ticket 01M6WVRTC724W6RQYAA9WCEK5F
            if (EmitPoseMessages && (_projectiles.Count > 0 || _projectilesOnWire))
            {
                BroadcastProjectileState();
                _projectilesOnWire = _projectiles.Count > 0;   // one empty message tells clients the last one is gone
            }

            // Last, so impacts and structural damage from this tick are already on the grids.
            Match?.Tick();
            AnnounceMatchEnd();
            ExpireHeldPeers();
        }

        // Hurts every block outside the ring through the normal damage path; the core is spared until
        // sudden death. Fractions under a point are carried per block so slow phases are not rounded away.
        // frob:ticket 01M7EXAGAAS0CRAVC1BEXJ5598
        void ApplyStorm(List<int> peerIds)
        {
            if (ActiveStorm == null || !MatchIsRunning) return;
            StormState zone = ActiveStorm.At((int)Math.Min(int.MaxValue, _tickIndex - _stormStartTick));
            if (zone.DamagePerTick <= 0f) return;

            foreach (int peer in peerIds)
            {
                if (!_peers.TryGetValue(peer, out var state)) continue;
                ShipBody ship = state.Ship;
                BlockGrid grid = ship.Grid;
                if (grid.Count == 0) continue;
                int coreKey = grid.CoreKeyOrDefault();
                var keys = new List<int>();
                foreach (int k in grid.SortedKeys) keys.Add(k);
                var destroyed = new List<int>();
                foreach (int key in keys)
                {
                    float2 point = ship.LocalToWorld(BlockGrid.CenterOf(key));
                    float hp = zone.DamageFor(point, key == coreKey);
                    if (hp <= 0f) continue;
                    state.StormCarry.TryGetValue(key, out float carry);
                    carry += hp;
                    int whole = (int)carry;
                    state.StormCarry[key] = carry - whole;
                    if (whole <= 0 || !grid.TryGet(key, out Block before)) continue;

                    ship.ApplyDamageAtWorldPoint(point, (byte)math.min(255, whole), out _);
                    if (!grid.TryGet(key, out Block after)) continue;
                    if (after.Damage >= Connectivity.DestroyedDamage && key != coreKey) destroyed.Add(key);
                    else if (after.Damage != before.Damage)
                    {
                        BlockKey.Unpack(key, out int x, out int y);
                        BroadcastReliable(new BlockDamaged(NextSequence(), state.NetId, (sbyte)x, (sbyte)y, after.Damage));
                    }
                }
                foreach (int key in destroyed) state.StormCarry.Remove(key);
                if (destroyed.Count > 0) DestroyAndDetach(peer, state, destroyed);
            }
        }

        // Remembers every block damage before a ship steps, so crash and heating damage done inside the
        // step can be told to the clients afterwards.
        static void SnapshotDamage(PeerState state)
        {
            state.DamageBeforeStep.Clear();
            foreach (var block in state.Ship.Grid.All) state.DamageBeforeStep[block.Key] = block.Value.Damage;
        }

        // Broadcasts what a planet did to the ship this step and removes blocks it wore down to 255; the
        // core is only ever damaged, never removed, so the match rules decide the loss.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        void ReportBodyDamage(int peer, PeerState state)
        {
            var grid = state.Ship.Grid;
            int coreKey = grid.CoreKeyOrDefault();
            List<int> destroyed = null;
            var keys = new List<int>();
            foreach (int k in grid.SortedKeys) keys.Add(k);
            foreach (int key in keys)
            {
                if (!grid.TryGet(key, out Block block)) continue;
                state.DamageBeforeStep.TryGetValue(key, out byte before);
                if (block.Damage == before) continue;
                if (block.Damage >= Connectivity.DestroyedDamage && key != coreKey) { (destroyed ??= new List<int>()).Add(key); continue; }
                BlockKey.Unpack(key, out int x, out int y);
                BroadcastReliable(new BlockDamaged(NextSequence(), state.NetId, (sbyte)x, (sbyte)y, block.Damage));
            }
            if (destroyed != null) DestroyAndDetach(peer, state, destroyed);
        }

        // Nudges a ship back toward the arena; a velocity change only, so no force reaches the
        // structural solver and no block takes damage (S41-1).
        // frob:ticket 01M3DG5Y2SN7TH11BDYFCY4SVZ
        void ApplyArenaPush(int peer, PeerState state, float dt)
        {
            ShipBody ship = state.Ship;
            bool inZone = Bounds.InPushZone(ship.Position);
            if (inZone != state.InPushZone)
            {
                state.InPushZone = inZone;
                Log.Info("peer " + peer + (inZone ? " entered" : " left") + " arena edge zone");
            }
            if (inZone) ship.Velocity += Bounds.PushAcceleration(ship.Position) * dt;
        }

        // Sends the finished match (draws included) to every connected peer, once per match.
        // frob:ticket 01M5VGS9DWNF85ZAQ0GD5X8A1J
        void AnnounceMatchEnd()
        {
            if (Match == null || ReferenceEquals(Match, _announcedMatch) || !Match.EndedArgs.HasValue) return;
            _announcedMatch = Match;
            var message = new MatchEnded(NextSequence(), Match.EndedArgs.Value);
            foreach (int peer in _peers.Keys)
            {
                var buffer = new byte[MatchEnded.MaxByteSize];
                var w = new ByteWriter(buffer);
                message.Write(ref w);
                SendTo(peer, reliable: true, buffer, w.Position);
            }
        }

        void ResolveShipContacts(List<int> peerIds, float dt)
        {
            var before = new Dictionary<int, Dictionary<int, byte>>();
            foreach (int peer in peerIds)
            {
                var damage = new Dictionary<int, byte>();
                foreach (var block in _peers[peer].Ship.Grid.All) damage[block.Key] = block.Value.Damage;
                before[peer] = damage;
            }

            for (int i = 0; i < peerIds.Count; i++)
            {
                var a = _peers[peerIds[i]];
                if (a.IsBuilding) continue;
                for (int j = i + 1; j < peerIds.Count; j++)
                {
                    var b = _peers[peerIds[j]];
                    if (!b.IsBuilding) ShipContacts.Resolve(a.Ship, b.Ship, dt);
                }
            }

            foreach (int peer in peerIds)
            {
                var state = _peers[peer];
                foreach (var block in state.Ship.Grid.All)
                {
                    byte oldDamage = before[peer].TryGetValue(block.Key, out byte value) ? value : (byte)0;
                    if (block.Value.Damage == oldDamage) continue;
                    BlockKey.Unpack(block.Key, out int x, out int y);
                    BroadcastReliable(new BlockDamaged(NextSequence(), state.NetId, (sbyte)x, (sbyte)y, block.Value.Damage));
                }
            }
        }

        void TimeoutStalePeers(float dt)
        {
            List<int> dead = null;
            foreach (var kv in _peers)
            {
                if (kv.Value.Held) continue; // already disconnected; the grace window is its clock
                kv.Value.SecondsSinceInput += dt;
                if (kv.Value.SecondsSinceInput > TimeoutSeconds)
                    (dead ??= new List<int>()).Add(kv.Key);
            }
            if (dead == null) return;
            foreach (int peer in dead)
            {
                Disconnect(peer); // holds the ship during a running match, else removes it
                _droppedPeers.Enqueue(peer);
            }
        }

        void DrainShots(int peer, ShipBody ship)
        {
            // frob:ticket 01M7FJ5NMTNP6BJQP8QBW5HVNJ
            // With a ready-up gate the build phase lasts until both sides are ready: a ship that has readied and is
            // already flying (or a bot) must not be able to shoot a peer who is still building.
            bool grace = RequireReady && (!MatchIsRunning || _tickIndex < _matchStartTick + (uint)System.Math.Ceiling(ShotGraceSeconds * TickRate));
            if (!grace) foreach (var shot in ship.PendingShots) SpawnProjectile(peer, ship, shot);
            ship.PendingShots.Clear();
        }

        void SpawnProjectile(int peer, ShipBody ship, in ShotRequest shot)
        {
            uint id = _nextProjectileId++;
            if (_nextProjectileId == 0) _nextProjectileId = 1;
            _projectiles.Add(new ProjectileBody
            {
                Id = id,
                OwnerPeer = peer,
                Position = shot.WorldOrigin,
                Velocity = shot.WorldDirection * shot.Spec.Speed + ship.Velocity,
                Spec = shot.Spec,
                LifeRemaining = shot.Spec.LifetimeSeconds,
            });
        }

        void StepProjectiles(float dt)
        {
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                var p = _projectiles[i];
                p.Velocity += _gravity.AccelerationAt(p.Position) * dt;
                p.Position += p.Velocity * dt;
                p.LifeRemaining -= dt;

                if (p.LifeRemaining <= 0f) { _projectiles.RemoveAt(i); continue; }

                // Shots that outrun the storm hard limit are gone for good rather than orbiting forever.
                if (ActiveStorm != null && MatchIsRunning
                    && !ActiveStorm.At((int)Math.Min(int.MaxValue, _tickIndex - _stormStartTick)).WithinHardLimit(p.Position))
                { _projectiles.RemoveAt(i); continue; }

                // Planets are solid to shots: despawn on contact and log it.
                // frob:doc docs/reference/hullbreach-net.md#serversimulation
                if (_gravity.TryContact(p.Position, p.Spec.Radius, out int bodyIndex, out _, out _))
                {
                    Log.Info("projectile " + p.Id + " owner=" + p.OwnerPeer + " hit planet " + bodyIndex
                        + " at (" + p.Position.x + ", " + p.Position.y + ")");
                    _projectiles.RemoveAt(i);
                    continue;
                }

                if (TryHitAnyShip(p, out int hitPeer, out int key))
                {
                    ApplyProjectileImpact(hitPeer, key, p);
                    _projectiles.RemoveAt(i);
                }
            }
        }

        bool TryHitAnyShip(ProjectileBody p, out int peer, out int key)
        {
            var peers = new List<int>(_peers.Keys);
            peers.Sort();
            foreach (int candidate in peers)
            {
                if (candidate == p.OwnerPeer) continue;
                var defender = _peers[candidate].Ship;
                float margin = Tolerance.MarginFor(math.length(defender.Velocity), _peers[candidate].Trust.Value);
                if (ProjectileHitTest.TryHitShipCell(defender, p.Position, p.Spec.Radius, margin, out int k))
                {
                    peer = candidate;
                    key = k;
                    return true;
                }
            }
            peer = -1;
            key = -1;
            return false;
        }

        void ApplyProjectileImpact(int peer, int key, ProjectileBody p)
        {
            var state = _peers[peer];
            var grid = state.Ship.Grid;
            if (!grid.TryGet(key, out var block)) return;

            // Same shove the Unity-side Projectile applies, so both paths agree.
            float speed = math.length(p.Velocity);
            if (speed > 1e-6f)
                state.Ship.ApplyImpulseAtWorldPoint(p.Position, p.Velocity / speed * p.Spec.Impulse);

            int newDamage = math.min(255, block.Damage + p.Spec.Damage);
            bool destroyed = newDamage >= 255 && key != grid.CoreKeyOrDefault();
            Tally(p.OwnerPeer, newDamage - block.Damage, destroyed ? 1 : 0, 0);

            if (destroyed)
            {
                grid.TryRemove(key);
                state.Construction.Remove(key);
                DropSalvage(state, key, block);
                BlockKey.Unpack(key, out int x, out int y);
                var evt = new BlockDestroyed(NextSequence(), state.NetId, (sbyte)x, (sbyte)y);
                BroadcastReliable(evt);
                // Blocks stranded by this kill count as the shooter's destroyed blocks too.
                // frob:ticket 01M6WVK2JM5FA72DD850NKNZP7
                Tally(p.OwnerPeer, 0, ResolveDetachAfterDestruction(peer, state), 0);
            }
            else
            {
                grid.TrySet(key, block.WithDamage((byte)newDamage));
                BlockKey.Unpack(key, out int x, out int y);
                var evt = new BlockDamaged(NextSequence(), state.NetId, (sbyte)x, (sbyte)y, (byte)newDamage);
                BroadcastReliable(evt);
            }

            if (p.Spec.Kind == ProjectileKind.GravityWell)
            {
                var body = new GravityBody(p.Position, p.Spec.Well.Mu, p.Spec.Well.Radius, 0.3f);
                _sink.AddTemporaryGravity(body, p.Spec.Well.Seconds);
            }
        }

        void ResolveStructuralFailures(int peer, PeerState state)
        {
            var grid = state.Ship.Grid;
            var toDamage = new List<int>();
            var toDetach = new List<int>();

            foreach (var kvp in state.Solver.BlockStresses)
            {
                int key = kvp.Key;
                var stress = kvp.Value;
                bool wasFailing = state.Failing.TryGetValue(key, out var f) && f;
                grid.TryGet(key, out var current);
                bool shouldDetach = DamageModel.ShouldDetach(stress.DuctileRatio, stress.BrittleRatio, wasFailing, current.Damage);

                if (stress.DuctileRatio >= 1f || stress.BrittleRatio >= 1f)
                {
                    toDamage.Add(key);
                    state.Failing[key] = true;
                }
                else
                {
                    state.Failing[key] = false;
                }

                if (shouldDetach) toDetach.Add(key);
            }

            foreach (int key in toDamage)
            {
                if (!grid.TryGet(key, out var block)) continue;
                state.Solver.BlockStresses.TryGetValue(key, out var stress);
                byte newDamage = DamageModel.Accumulate(block.Damage, DamageModel.DamageRatio(stress.DuctileRatio, stress.BrittleRatio), Dt);
                grid.TrySet(key, block.WithDamage(newDamage));
                BlockKey.Unpack(key, out int x, out int y);
                BroadcastReliable(new BlockDamaged(NextSequence(), state.NetId, (sbyte)x, (sbyte)y, newDamage));
            }

            if (toDetach.Count > 0) DestroyAndDetach(peer, state, toDetach);

            if (state.Solver.BuckledBlocks.Count > 0)
                DestroyAndDetach(peer, state, new List<int>(state.Solver.BuckledBlocks));
        }

        // Broadcasts a BlockDestroyed for each key (sorted for determinism),
        // then resolves whatever that stranded.
        void DestroyAndDetach(int peer, PeerState state, List<int> keys)
        {
            keys.Sort();
            var grid = state.Ship.Grid;
            foreach (int key in keys)
            {
                grid.TryGet(key, out Block lost);
                if (!grid.TryRemove(key)) continue;
                state.Failing.Remove(key);
                state.Construction.Remove(key);
                DropSalvage(state, key, lost);
                BlockKey.Unpack(key, out int x, out int y);
                BroadcastReliable(new BlockDestroyed(NextSequence(), state.NetId, (sbyte)x, (sbyte)y));
            }
            state.Solver.MarkTopologyChanged();
            ResolveDetachAfterDestruction(peer, state);
        }

        // Runs FindDetached/SplitIntoComponents once per batch; returns the stranded block count.
        int ResolveDetachAfterDestruction(int peer, PeerState state)
        {
            var grid = state.Ship.Grid;
            var stranded = new List<int>();
            Connectivity.FindDetached(grid, stranded);
            if (stranded.Count == 0) { state.Ship.RebuildDerivedViews(); return 0; }

            var components = new List<List<int>>();
            Connectivity.SplitIntoComponents(grid, stranded, components);

            // Deterministic order: sort components by their smallest key.
            components.Sort((a, b) =>
            {
                int minA = a.Count > 0 ? a[0] : int.MaxValue;
                int minB = b.Count > 0 ? b[0] : int.MaxValue;
                foreach (var k in a) if (k < minA) minA = k;
                foreach (var k in b) if (k < minB) minB = k;
                return minA.CompareTo(minB);
            });

            foreach (var component in components)
            {
                float2 sum = float2.zero;
                foreach (int key in component) sum += BlockGrid.CenterOf(key);
                float2 localCentroid = sum / component.Count;
                float2 worldCentroid = state.Ship.LocalToWorld(localCentroid);

                foreach (int key in component)
                {
                    if (grid.TryGet(key, out Block stranded0)) DropSalvage(state, key, stranded0);
                    grid.TryRemove(key);
                    state.Construction.Remove(key);
                }

                ushort fragmentId = AllocateFragmentId();
                var evt = new FragmentSpawned(
                    NextSequence(), state.NetId, fragmentId,
                    Quantization.PackPosition(worldCentroid.x),
                    Quantization.PackPosition(worldCentroid.y),
                    Quantization.PackAngle(state.Ship.Rotation),
                    Quantization.PackPosition(state.Ship.Velocity.x),
                    Quantization.PackPosition(state.Ship.Velocity.y),
                    Quantization.PackAngle(state.Ship.AngularVelocity));
                BroadcastReliable(evt);
            }

            state.Ship.RebuildDerivedViews();
            return stranded.Count;
        }

        // Next fragment NetId, cycling inside the fragment range so it never
        // lands on a peer's NetId.
        ushort AllocateFragmentId()
        {
            ushort id = _nextFragmentId;
            _nextFragmentId = id == ushort.MaxValue ? FirstFragmentId : (ushort)(id + 1);
            return id;
        }

        // Test/debug hook; see the reference page.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public void DebugDestroyBlock(int peer, int x, int y)
        {
            if (!_peers.TryGetValue(peer, out var state)) return;
            int key = BlockKey.Pack(x, y);
            state.Ship.Grid.TryGet(key, out Block lost);
            if (!state.Ship.Grid.TryRemove(key)) return;
            state.Failing.Remove(key);
            state.Construction.Remove(key);
            DropSalvage(state, key, lost);
            BroadcastReliable(new BlockDestroyed(NextSequence(), state.NetId, (sbyte)x, (sbyte)y));
            state.Solver.MarkTopologyChanged();
            ResolveDetachAfterDestruction(peer, state);
        }

        ShipSnapshot BuildSnapshot(PeerState state, uint sequence)
        {
            var blocks = new List<SnapshotBlock>();
            foreach (var kvp in state.Ship.Grid.All)
            {
                BlockKey.Unpack(kvp.Key, out int x, out int y);
                blocks.Add(new SnapshotBlock((sbyte)x, (sbyte)y, kvp.Value.TypeId, kvp.Value.Modifiers, kvp.Value.Damage));
            }
            return new ShipSnapshot(
                sequence, state.NetId, blocks.ToArray(),
                Quantization.PackPosition(state.Ship.Position.x),
                Quantization.PackPosition(state.Ship.Position.y),
                Quantization.PackAngle(state.Ship.Rotation),
                Quantization.PackPosition(state.Ship.Velocity.x),
                Quantization.PackPosition(state.Ship.Velocity.y),
                Quantization.PackAngle(state.Ship.AngularVelocity));
        }

        ShipState BuildState(PeerState state) => new ShipState(
            state.NetId,
            Quantization.PackPosition(state.Ship.Position.x),
            Quantization.PackPosition(state.Ship.Position.y),
            Quantization.PackAngle(state.Ship.Rotation),
            Quantization.PackPosition(state.Ship.Velocity.x),
            Quantization.PackPosition(state.Ship.Velocity.y),
            Quantization.PackAngle(state.Ship.AngularVelocity),
            state.HasInput ? state.LatestInput.Tick : 0u,
            _tickIndex);

        uint NextSequence() => ++_sequence;

        void BroadcastProjectileState()
        {
            int count = Math.Min(_projectiles.Count, ProjectileState.MaxEntries);
            var entries = new ProjectileEntry[count];
            for (int i = 0; i < count; i++)
            {
                var p = _projectiles[i];
                entries[i] = new ProjectileEntry(p.Id,
                    Quantization.PackPosition(p.Position.x), Quantization.PackPosition(p.Position.y),
                    Quantization.PackPosition(p.Velocity.x), Quantization.PackPosition(p.Velocity.y));
            }
            var message = new ProjectileState(_tickIndex, entries);
            foreach (int peer in _peers.Keys)
            {
                var buffer = new byte[message.ByteSize];
                var w = new ByteWriter(buffer);
                message.Write(ref w);
                SendTo(peer, reliable: false, buffer, w.Position);
            }
        }

        // Every outgoing byte goes through here: a held ship's connection is gone, so nothing is sent to it.
        void SendTo(int peer, bool reliable, byte[] buffer, int length)
        {
            if (_peers.TryGetValue(peer, out var state) && state.Held) return;
            _outbox.Send(peer, reliable, buffer, length);
        }

        void EmitUnreliable(int peer, ShipState state)
        {
            var buffer = new byte[ShipState.ByteSize];
            var w = new ByteWriter(buffer);
            state.Write(ref w);
            SendTo(peer, reliable: false, buffer, w.Position);
        }

        void EmitReliable(int peer, ShipSnapshot snapshot)
        {
            var buffer = new byte[snapshot.ByteSize];
            var w = new ByteWriter(buffer);
            snapshot.Write(ref w);
            SendTo(peer, reliable: true, buffer, w.Position);
        }

        // Broadcasts to every peer, not just the ship's own owner.
        void BroadcastReliable<T>(T message) where T : struct
        {
            foreach (int peer in _peers.Keys)
            {
                var buffer = new byte[64];
                var w = new ByteWriter(buffer);
                WriteAny(message, ref w);
                SendTo(peer, reliable: true, buffer, w.Position);
            }
        }

        static void WriteAny<T>(T message, ref ByteWriter w) where T : struct
        {
            switch (message)
            {
                case BlockDestroyed m: m.Write(ref w); break;
                case BlockDamaged m: m.Write(ref w); break;
                case BlockPlaced m: m.Write(ref w); break;
                case FragmentSpawned m: m.Write(ref w); break;
                case PowerupApplied m: m.Write(ref w); break;
                case PickupSpawned m: m.Write(ref w); break;
                case PickupDespawned m: m.Write(ref w); break;
                case GravityWellSpawned m: m.Write(ref w); break;
                case ShipRemoved m: m.Write(ref w); break;
                default: throw new InvalidOperationException("Unhandled reliable message type " + typeof(T));
            }
        }

        // The server's own IWorldSink; see the reference page.
        sealed class ServerWorldSink : IWorldSink
        {
            readonly ServerSimulation _owner;
            public ServerWorldSink(ServerSimulation owner) => _owner = owner;

            public void SpawnProjectile(in ShotRequest shot)
            {
                uint id = _owner._nextProjectileId++;
                if (_owner._nextProjectileId == 0) _owner._nextProjectileId = 1;
                _owner._projectiles.Add(new ProjectileBody
                {
                    Id = id,
                    OwnerPeer = -1,
                    Position = shot.WorldOrigin,
                    Velocity = shot.WorldDirection * shot.Spec.Speed,
                    Spec = shot.Spec,
                    LifeRemaining = shot.Spec.LifetimeSeconds,
                });
            }

            public void AddTemporaryGravity(GravityBody body, float seconds)
            {
                _owner._gravity.AddTemporary(body, seconds);
                var evt = GravityWellSpawned.FromFloats(body.Position.x, body.Position.y, body.Mu, body.Radius, seconds);
                _owner.BroadcastReliable(evt);
            }

            public bool TryNearestEnemy(float2 from, ShipBody self, out float2 position, out float2 velocity)
            {
                position = float2.zero;
                velocity = float2.zero;
                float bestDistSq = float.PositiveInfinity;
                bool found = false;
                foreach (var kv in _owner._peers)
                {
                    var candidate = kv.Value.Ship;
                    if (ReferenceEquals(candidate, self)) continue;
                    float distSq = math.distancesq(candidate.Position, from);
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        position = candidate.Position;
                        velocity = candidate.Velocity;
                        found = true;
                    }
                }
                return found;
            }

            public IReadOnlyList<ShipBody> Ships
            {
                get
                {
                    var list = new List<ShipBody>();
                    foreach (var kv in _owner._peers) list.Add(kv.Value.Ship);
                    return list;
                }
            }
        }
    }

    // Small BlockGrid helper; see the reference page.
    static class BlockGridExtensions
    {
        public static int CoreKeyOrDefault(this BlockGrid grid) => grid.CoreKey ?? int.MinValue;
    }
}
