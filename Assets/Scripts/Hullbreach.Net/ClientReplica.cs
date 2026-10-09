using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Ship;

namespace Hullbreach.Net
{
    // Effect-worthy events a renderer/audio layer wants to react to, drained
    // via ClientReplica.TryDequeueEvent once applied.
    // frob:doc docs/reference/hullbreach-net.md#replicaeventkind
    public enum ReplicaEventKind
    {
        BlockDestroyed,
        BlockDamaged,
        BlockPlaced,
        FragmentSpawned,
        PowerupApplied,
        GravityWellSpawned,
        ShipRemoved,
    }

    // What ApplyReceived/ApplyReliable did with one payload; wire data never
    // throws, it is classified here instead.
    // frob:doc docs/reference/hullbreach-net.md#replicaapplyresult
    public enum ReplicaApplyResult
    {
        // Applied (or consumed in order) right now.
        Applied,
        // Held in the reorder buffer until the gap before it fills.
        Buffered,
        // Duplicate or already covered by a newer snapshot; ignored.
        Stale,
        // Sequence too far past the last applied one (INV-002); dropped.
        OutOfWindow,
        // Truncated, unknown kind or implausible content; dropped.
        Malformed,
    }

    // One applied event, boxed just enough for a renderer to know what
    // happened and to which ship, without re-parsing wire bytes.
    // frob:doc docs/reference/hullbreach-net.md#replicaevent
    public readonly struct ReplicaEvent
    {
        // frob:doc docs/reference/hullbreach-net.md#replicaevent
        public readonly ReplicaEventKind Kind;

        // frob:doc docs/reference/hullbreach-net.md#replicaevent
        public readonly ushort NetId;

        // frob:doc docs/reference/hullbreach-net.md#replicaevent
        public readonly int X;

        // frob:doc docs/reference/hullbreach-net.md#replicaevent
        public readonly int Y;

        // frob:doc docs/reference/hullbreach-net.md#replicaevent
        public readonly float2 WorldPosition;

        // frob:doc docs/reference/hullbreach-net.md#replicaevent
        public ReplicaEvent(ReplicaEventKind kind, ushort netId, int x, int y, float2 worldPosition)
        {
            Kind = kind;
            NetId = netId;
            X = x;
            Y = y;
            WorldPosition = worldPosition;
        }
    }

    // Two poses timestamped by tick, for cheap linear interpolation between
    // the last two ShipState updates a replica received.
    struct PoseSample
    {
        public uint Tick;
        public float2 Position;
        public float Rotation;
        public float2 Velocity;
        public float AngularVelocity;
    }

    // The non-authoritative twin of ServerSimulation; see the reference page.
    // frob:doc docs/reference/hullbreach-net.md#clientreplica
    public sealed class ClientReplica
    {
        sealed class ReplicaShip
        {
            public ShipBody Body = new ShipBody();
            public PoseSample? Older;
            public PoseSample? Newer;
        }

        readonly Dictionary<ushort, ReplicaShip> _ships = new Dictionary<ushort, ReplicaShip>();
        readonly Dictionary<uint, byte[]> _pendingReliable = new Dictionary<uint, byte[]>();
        readonly Queue<ReplicaEvent> _events = new Queue<ReplicaEvent>();

        // Highest sequence already folded into each ship (its snapshot's
        // sequence, or the removal's): older events for it are skipped.
        readonly Dictionary<ushort, uint> _shipBaseline = new Dictionary<ushort, uint>();

        // Events for a ship whose snapshot has not arrived yet, replayed when it does.
        readonly List<DeferredEvent> _deferred = new List<DeferredEvent>();

        readonly struct DeferredEvent
        {
            public readonly ushort NetId;
            public readonly uint Sequence;
            public readonly byte[] Payload;

            public DeferredEvent(ushort netId, uint sequence, byte[] payload)
            {
                NetId = netId;
                Sequence = sequence;
                Payload = payload;
            }
        }

        // How far past the last applied sequence a reliable message may be and
        // still be buffered; also caps the buffered and deferred counts, so
        // remote data can never grow memory without bound (INV-002).
        // frob:doc docs/reference/hullbreach-net.md#clientreplica
        // frob:invariant INV-002
        public const int MaxReliableWindow = 512;

        const uint HalfSequenceRange = 0x80000000u;

        uint _lastAppliedSequence;
        bool _haveBaseline;

        // True when `sequence` is at or behind `reference` in wrapping uint order.
        static bool AtOrBehind(uint sequence, uint reference)
        {
            uint ahead = sequence - reference;
            return ahead == 0 || ahead >= HalfSequenceRange;
        }

        // Every replica ship known so far, keyed by server netId.
        // frob:doc docs/reference/hullbreach-net.md#clientreplica
        public IReadOnlyDictionary<ushort, ShipBody> Ships
        {
            get
            {
                var result = new Dictionary<ushort, ShipBody>();
                foreach (var kv in _ships) result[kv.Key] = kv.Value.Body;
                return result;
            }
        }

        // Installs the ship unless a newer snapshot or removal already covers it
        // (false then); the first snapshot also sets the stream baseline.
        // frob:doc docs/reference/hullbreach-net.md#clientreplica
        public bool ApplySnapshot(ShipSnapshot snapshot)
        {
            if (_shipBaseline.TryGetValue(snapshot.NetId, out uint covered) && AtOrBehind(snapshot.Sequence, covered))
                return false;

            var replica = new ReplicaShip();
            foreach (var b in snapshot.Blocks)
            {
                int key = BlockKey.Pack(b.X, b.Y);
                replica.Body.Grid.TryAdd(key, new Block(b.TypeId, b.Mods, b.Damage));
            }
            replica.Body.RebuildDerivedViews();

            float2 position = new float2(Quantization.UnpackPosition(snapshot.Px), Quantization.UnpackPosition(snapshot.Py));
            float rotation = Quantization.UnpackAngle(snapshot.Rot);
            float2 velocity = new float2(Quantization.UnpackPosition(snapshot.Vx), Quantization.UnpackPosition(snapshot.Vy));
            float angularVelocity = Quantization.UnpackAngle(snapshot.Av);

            replica.Body.Position = position;
            replica.Body.Rotation = rotation;
            replica.Body.Velocity = velocity;
            replica.Body.AngularVelocity = angularVelocity;

            var sample = new PoseSample { Tick = 0, Position = position, Rotation = rotation, Velocity = velocity, AngularVelocity = angularVelocity };
            replica.Newer = sample;
            replica.Older = sample;

            _ships[snapshot.NetId] = replica;
            _shipBaseline[snapshot.NetId] = snapshot.Sequence;

            if (!_haveBaseline)
            {
                _lastAppliedSequence = snapshot.Sequence;
                _haveBaseline = true;
                DiscardStaleBuffered();
            }
            else if (!AtOrBehind(snapshot.Sequence, _lastAppliedSequence)
                && snapshot.Sequence - _lastAppliedSequence <= MaxReliableWindow)
            {
                // The snapshot owns a slot in the ordered stream; reserve it
                // (an empty payload is a no-op) so the cursor can pass it.
                _pendingReliable[snapshot.Sequence] = Array.Empty<byte>();
            }

            ReplayDeferred(snapshot.NetId);
            DrainPending();
            return true;
        }

        // Unreliable and unordered by design; see the reference page.
        // frob:doc docs/reference/hullbreach-net.md#clientreplica
        public void ApplyState(ShipState state, uint tick)
        {
            if (!_ships.TryGetValue(state.NetId, out var replica)) return;

            var sample = new PoseSample
            {
                Tick = tick,
                Position = new float2(Quantization.UnpackPosition(state.Px), Quantization.UnpackPosition(state.Py)),
                Rotation = Quantization.UnpackAngle(state.Rot),
                Velocity = new float2(Quantization.UnpackPosition(state.Vx), Quantization.UnpackPosition(state.Vy)),
                AngularVelocity = Quantization.UnpackAngle(state.Av),
            };

            replica.Older = replica.Newer;
            replica.Newer = sample;

            replica.Body.Position = sample.Position;
            replica.Body.Rotation = sample.Rotation;
            replica.Body.Velocity = sample.Velocity;
            replica.Body.AngularVelocity = sample.AngularVelocity;
        }

        // 0 <= t <= 1 (0 = older, 1 = newer). False if the ship is unknown.
        // frob:doc docs/reference/hullbreach-net.md#clientreplica
        public bool TryInterpolate(ushort netId, float t, out float2 position, out float rotation)
        {
            position = float2.zero;
            rotation = 0f;
            if (!_ships.TryGetValue(netId, out var replica) || replica.Older == null || replica.Newer == null) return false;

            var a = replica.Older.Value;
            var b = replica.Newer.Value;
            position = math.lerp(a.Position, b.Position, math.clamp(t, 0f, 1f));
            rotation = math.lerp(a.Rotation, b.Rotation, math.clamp(t, 0f, 1f));
            return true;
        }

        // Netcode for Entities ghosts own the high-frequency pose stream in
        // the Unity adapter. Reliable topology still arrives through the
        // existing ordered protocol, so a pose is ignored until its snapshot
        // has established the ship.
        public bool ApplyGhostPose(ushort netId, float2 position, float rotation,
            float2 velocity, float angularVelocity, uint tick)
        {
            if (!_ships.TryGetValue(netId, out var replica)) return false;

            var sample = new PoseSample
            {
                Tick = tick,
                Position = position,
                Rotation = rotation,
                Velocity = velocity,
                AngularVelocity = angularVelocity,
            };
            replica.Older = replica.Newer;
            replica.Newer = sample;
            replica.Body.Position = position;
            replica.Body.Rotation = rotation;
            replica.Body.Velocity = velocity;
            replica.Body.AngularVelocity = angularVelocity;
            return true;
        }

        // Entry point a transport pump should call for every received payload;
        // only the first `length` bytes are read, and wire data never throws.
        // frob:doc docs/reference/hullbreach-net.md#clientreplica
        // frob:invariant INV-001
        public ReplicaApplyResult ApplyReceived(byte[] into, int length, uint clientTick = 0)
        {
            if (into == null || length < 1 || length > into.Length) return ReplicaApplyResult.Malformed;

            var r = new ByteReader(into, 0, length);
            switch ((MessageKind)into[0])
            {
                case MessageKind.ShipSnapshot:
                {
                    var snapshot = ShipSnapshot.Read(ref r);
                    if (r.Failed) return ReplicaApplyResult.Malformed;
                    return ApplySnapshot(snapshot) ? ReplicaApplyResult.Applied : ReplicaApplyResult.Stale;
                }
                case MessageKind.ShipState:
                {
                    var state = ShipState.Read(ref r);
                    if (r.Failed) return ReplicaApplyResult.Malformed;
                    ApplyState(state, clientTick);
                    return ReplicaApplyResult.Applied;
                }
                case MessageKind.GravityWellSpawned:
                {
                    var well = GravityWellSpawned.Read(ref r);
                    if (r.Failed) return ReplicaApplyResult.Malformed;
                    ApplyGravityWellSpawned(well);
                    return ReplicaApplyResult.Applied;
                }
                case MessageKind.BlockPlaced:
                case MessageKind.BlockDestroyed:
                case MessageKind.FragmentSpawned:
                case MessageKind.BlockDamaged:
                case MessageKind.PowerupApplied:
                case MessageKind.ShipRemoved:
                {
                    // u32 sequence number right after the kind byte; a body that
                    // turns out short is skipped in order by ApplyOne.
                    r.ReadU8();
                    uint sequence = r.ReadU32();
                    if (r.Failed) return ReplicaApplyResult.Malformed;
                    var trimmed = new byte[length];
                    Array.Copy(into, trimmed, length);
                    return ApplyReliable(sequence, trimmed);
                }
                default:
                    // Includes Input and any unknown byte: never a server->client message.
                    return ReplicaApplyResult.Malformed;
            }
        }

        // Buffers by sequence and applies in order; see the reference page.
        // Sequences outside MaxReliableWindow are dropped (INV-002).
        // frob:doc docs/reference/hullbreach-net.md#clientreplica
        // frob:invariant INV-002
        public ReplicaApplyResult ApplyReliable(uint sequence, byte[] payload)
        {
            if (payload == null) return ReplicaApplyResult.Malformed;

            if (_haveBaseline)
            {
                if (AtOrBehind(sequence, _lastAppliedSequence)) return ReplicaApplyResult.Stale; // stale/duplicate
                if (sequence - _lastAppliedSequence > MaxReliableWindow) return ReplicaApplyResult.OutOfWindow;
            }
            else if (_pendingReliable.Count >= MaxReliableWindow && !_pendingReliable.ContainsKey(sequence))
            {
                return ReplicaApplyResult.OutOfWindow;
            }

            _pendingReliable[sequence] = payload;
            DrainPending();
            return _pendingReliable.ContainsKey(sequence) ? ReplicaApplyResult.Buffered : ReplicaApplyResult.Applied;
        }

        // Applies the contiguous run after the last applied sequence. A
        // payload that fails to decode still consumes its slot, so one bad
        // message can never stall the stream.
        void DrainPending()
        {
            if (!_haveBaseline) return;
            while (_pendingReliable.TryGetValue(_lastAppliedSequence + 1, out var next))
            {
                _pendingReliable.Remove(_lastAppliedSequence + 1);
                _lastAppliedSequence++;
                ApplyOne(_lastAppliedSequence, next);
            }
        }

        void DiscardStaleBuffered()
        {
            var stale = new List<uint>();
            foreach (var kv in _pendingReliable)
                if (AtOrBehind(kv.Key, _lastAppliedSequence)) stale.Add(kv.Key);
            foreach (var key in stale) _pendingReliable.Remove(key);
        }

        // True when the ship already includes everything up to `sequence`.
        bool IsCovered(ushort netId, uint sequence)
            => _shipBaseline.TryGetValue(netId, out uint covered) && AtOrBehind(sequence, covered);

        // Holds an event for a ship whose snapshot is still in flight.
        void Defer(ushort netId, uint sequence, byte[] payload)
        {
            if (_deferred.Count >= MaxReliableWindow) _deferred.RemoveAt(0);
            _deferred.Add(new DeferredEvent(netId, sequence, payload));
        }

        void ReplayDeferred(ushort netId)
        {
            if (_deferred.Count == 0) return;
            var replay = new List<DeferredEvent>();
            _deferred.RemoveAll(d =>
            {
                if (d.NetId != netId) return false;
                replay.Add(d);
                return true;
            });
            replay.Sort((x, y) => x.Sequence.CompareTo(y.Sequence));
            foreach (var d in replay) ApplyOne(d.Sequence, d.Payload);
        }

        // Decodes and applies one sequenced event; false when it was malformed
        // (skipped). Never throws on wire data.
        bool ApplyOne(uint sequence, byte[] payload)
        {
            if (payload.Length == 0) return true; // reserved slot, already applied

            var r = new ByteReader(payload);
            switch ((MessageKind)payload[0])
            {
                case MessageKind.BlockDestroyed:
                {
                    var m = BlockDestroyed.Read(ref r);
                    if (r.Failed) return false;
                    if (IsCovered(m.NetId, sequence)) return true;
                    if (!_ships.ContainsKey(m.NetId)) { Defer(m.NetId, sequence, payload); return true; }
                    ApplyBlockDestroyed(m);
                    return true;
                }
                case MessageKind.BlockDamaged:
                {
                    var m = BlockDamaged.Read(ref r);
                    if (r.Failed) return false;
                    if (IsCovered(m.NetId, sequence)) return true;
                    if (!_ships.ContainsKey(m.NetId)) { Defer(m.NetId, sequence, payload); return true; }
                    ApplyBlockDamaged(m);
                    return true;
                }
                case MessageKind.BlockPlaced:
                {
                    var m = BlockPlaced.Read(ref r);
                    if (r.Failed) return false;
                    if (IsCovered(m.NetId, sequence)) return true;
                    if (!_ships.ContainsKey(m.NetId)) { Defer(m.NetId, sequence, payload); return true; }
                    ApplyBlockPlaced(m);
                    return true;
                }
                case MessageKind.FragmentSpawned:
                {
                    var m = FragmentSpawned.Read(ref r);
                    if (r.Failed) return false;
                    ApplyFragmentSpawned(m);
                    return true;
                }
                case MessageKind.PowerupApplied:
                {
                    var m = PowerupApplied.Read(ref r);
                    if (r.Failed) return false;
                    if (IsCovered(m.NetId, sequence)) return true;
                    if (!_ships.ContainsKey(m.NetId)) { Defer(m.NetId, sequence, payload); return true; }
                    ApplyPowerupApplied(m);
                    return true;
                }
                case MessageKind.ShipRemoved:
                {
                    var m = ShipRemoved.Read(ref r);
                    if (r.Failed) return false;
                    ApplyShipRemoved(m);
                    return true;
                }
                default:
                    return false;
            }
        }

        // Drops the ship; its baseline stays so a late duplicate snapshot or
        // event for it cannot resurrect it.
        void ApplyShipRemoved(ShipRemoved m)
        {
            if (IsCovered(m.NetId, m.Sequence)) return;
            _shipBaseline[m.NetId] = m.Sequence;
            _deferred.RemoveAll(d => d.NetId == m.NetId);
            if (_ships.Remove(m.NetId))
                _events.Enqueue(new ReplicaEvent(ReplicaEventKind.ShipRemoved, m.NetId, 0, 0, float2.zero));
        }

        void ApplyBlockDestroyed(BlockDestroyed m)
        {
            if (!_ships.TryGetValue(m.NetId, out var replica)) return;
            int key = BlockKey.Pack(m.X, m.Y);
            if (replica.Body.Grid.TryRemove(key))
            {
                RunLocalDetach(replica);
            }
            _events.Enqueue(new ReplicaEvent(ReplicaEventKind.BlockDestroyed, m.NetId, m.X, m.Y, replica.Body.LocalToWorld(BlockGrid.CenterOf(key))));
        }

        void ApplyBlockDamaged(BlockDamaged m)
        {
            if (!_ships.TryGetValue(m.NetId, out var replica)) return;
            int key = BlockKey.Pack(m.X, m.Y);
            if (replica.Body.Grid.TryGet(key, out var block))
                replica.Body.Grid.TrySet(key, block.WithDamage(m.Damage));
            _events.Enqueue(new ReplicaEvent(ReplicaEventKind.BlockDamaged, m.NetId, m.X, m.Y, replica.Body.LocalToWorld(BlockGrid.CenterOf(key))));
        }

        void ApplyBlockPlaced(BlockPlaced m)
        {
            if (!_ships.TryGetValue(m.NetId, out var replica)) return;
            int key = BlockKey.Pack(m.X, m.Y);
            replica.Body.Grid.TryAdd(key, new Block(m.TypeId, m.Mods));
            replica.Body.RebuildDerivedViews();
            _events.Enqueue(new ReplicaEvent(ReplicaEventKind.BlockPlaced, m.NetId, m.X, m.Y, replica.Body.LocalToWorld(BlockGrid.CenterOf(key))));
        }

        // Spawns the replica body for an already-derived detached component;
        // see the reference page for why it carries no block list.
        void ApplyFragmentSpawned(FragmentSpawned m)
        {
            var fragment = new ShipBody
            {
                Position = new float2(Quantization.UnpackPosition(m.Px), Quantization.UnpackPosition(m.Py)),
                Rotation = Quantization.UnpackAngle(m.Rot),
                Velocity = new float2(Quantization.UnpackPosition(m.Vx), Quantization.UnpackPosition(m.Vy)),
                AngularVelocity = Quantization.UnpackAngle(m.Av),
            };
            _ships[m.NewId] = new ReplicaShip { Body = fragment };
            _events.Enqueue(new ReplicaEvent(ReplicaEventKind.FragmentSpawned, m.NewId, 0, 0, fragment.Position));
        }

        void ApplyPowerupApplied(PowerupApplied m)
        {
            if (_ships.TryGetValue(m.NetId, out var replica))
            {
                int key = BlockKey.Pack(m.X, m.Y);
                if (replica.Body.Grid.TryGet(key, out var block))
                    replica.Body.Grid.TrySet(key, block.WithModifiers(BlockVariants.With(block.Modifiers, m.Variant)));
            }
            _events.Enqueue(new ReplicaEvent(ReplicaEventKind.PowerupApplied, m.NetId, m.X, m.Y, float2.zero));
        }

        void ApplyGravityWellSpawned(GravityWellSpawned m)
        {
            _events.Enqueue(new ReplicaEvent(ReplicaEventKind.GravityWellSpawned, 0, 0, 0, new float2(m.PxFloat, m.PyFloat)));
        }

        // Derives the same detached-component split the server derived;
        // see the reference page.
        void RunLocalDetach(ReplicaShip replica)
        {
            var stranded = new List<int>();
            Connectivity.FindDetached(replica.Body.Grid, stranded);
            foreach (int key in stranded) replica.Body.Grid.TryRemove(key);
            if (stranded.Count > 0) replica.Body.RebuildDerivedViews();
        }

        // Pops one applied event oldest first. False when nothing is queued.
        // frob:doc docs/reference/hullbreach-net.md#clientreplica
        public bool TryDequeueEvent(out ReplicaEvent evt)
        {
            if (_events.Count > 0) { evt = _events.Dequeue(); return true; }
            evt = default;
            return false;
        }

        // A thin convenience: ClientReplica does not own a transport itself.
        // frob:doc docs/reference/hullbreach-net.md#clientreplica
        public static InputMessage BuildInput(ushort netId, uint tick, float thrustAxis, float steer, bool firePressed)
            => InputMessage.FromFloats(netId, tick, thrustAxis, steer, firePressed);
    }
}
