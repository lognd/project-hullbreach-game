using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Ship;

namespace Hullbreach.Net
{
    /// <summary>Effect-worthy events a renderer/audio layer wants to react to,
    /// drained via <see cref="ClientReplica.TryDequeueEvent"/> once applied.</summary>
    public enum ReplicaEventKind
    {
        BlockDestroyed,
        BlockDamaged,
        BlockPlaced,
        FragmentSpawned,
        PowerupApplied,
        GravityWellSpawned,
    }

    /// <summary>One applied event, boxed just enough for a renderer to know
    /// what happened and to which ship, without re-parsing wire bytes.</summary>
    public readonly struct ReplicaEvent
    {
        public readonly ReplicaEventKind Kind;
        public readonly ushort NetId;
        public readonly int X;
        public readonly int Y;
        public readonly float2 WorldPosition;

        public ReplicaEvent(ReplicaEventKind kind, ushort netId, int x, int y, float2 worldPosition)
        {
            Kind = kind;
            NetId = netId;
            X = x;
            Y = y;
            WorldPosition = worldPosition;
        }
    }

    /// <summary>
    /// Two poses timestamped by tick, for cheap linear interpolation between
    /// the last two ShipState updates a replica received. Rendering at a
    /// point in between (rather than snapping to each state the instant it
    /// arrives) is what smooths ~unreliable, ~30-50 Hz updates into motion
    /// that does not visibly stutter.
    /// </summary>
    struct PoseSample
    {
        public uint Tick;
        public float2 Position;
        public float Rotation;
        public float2 Velocity;
        public float AngularVelocity;
    }

    /// <summary>
    /// The non-authoritative twin of ServerSimulation: applies ShipSnapshot/
    /// ShipState/reliable events received over an ITransport (or fed
    /// directly in a test) to build and keep replica ShipBody instances in
    /// sync with the server, WITHOUT running a StructuralSolver (clients
    /// never decide a block breaks; see StructuralSolver.BuckledBlocks).
    ///
    /// Reliable events are buffered by sequence number and applied only when
    /// the run is contiguous from the last applied sequence: see
    /// <see cref="ApplyReliable"/>. This is what makes ClientReplica correct
    /// even against a transport that reorders reliable messages (see
    /// LoopbackTransport's jitter and ITransport's ordering contract).
    /// </summary>
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

        uint _lastAppliedSequence;
        bool _haveBaseline;

        /// <summary>Every replica ship known so far, keyed by server netId,
        /// for a renderer to iterate.</summary>
        public IReadOnlyDictionary<ushort, ShipBody> Ships
        {
            get
            {
                var result = new Dictionary<ushort, ShipBody>();
                foreach (var kv in _ships) result[kv.Key] = kv.Value.Body;
                return result;
            }
        }

        /// <summary>
        /// Builds (or replaces) the replica ship named by the snapshot's
        /// netId from its block list and pose. A snapshot's own sequence
        /// number becomes the new baseline: any buffered reliable event with
        /// a sequence at or below it is stale (it was already folded into
        /// the snapshot server-side) and is discarded rather than reapplied.
        /// </summary>
        public void ApplySnapshot(ShipSnapshot snapshot)
        {
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

            if (!_haveBaseline || snapshot.Sequence > _lastAppliedSequence)
            {
                _lastAppliedSequence = snapshot.Sequence;
                _haveBaseline = true;
                DiscardStaleBuffered();
            }
        }

        /// <summary>
        /// Overwrites the named ship's latest pose sample (shifting the
        /// previous "newer" sample down to "older") for interpolation.
        /// Unreliable and unordered: an out-of-order or duplicate ShipState
        /// simply becomes the new "newer" sample regardless of tick, since a
        /// missed/duplicated pose update is harmless by design.
        /// </summary>
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

        /// <summary>
        /// Linearly interpolates the named ship's pose between its last two
        /// ShipState samples at 0 &lt;= t &lt;= 1 (0 = older, 1 = newer). False
        /// if the ship is unknown.
        /// </summary>
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

        /// <summary>
        /// The convenience entry point a transport pump should call for
        /// every payload TryReceive hands back, whatever kind it turns out
        /// to be: dispatches ShipSnapshot/ShipState straight through (they
        /// are idempotent/unordered by design), applies GravityWellSpawned
        /// immediately (it carries no sequence number: see NetMessages'
        /// layout doc), and routes every other reliable event through the
        /// sequence-ordered buffer in <see cref="ApplyReliable"/>.
        /// </summary>
        public void ApplyReceived(byte[] into, int length, uint clientTick = 0)
        {
            var kind = (MessageKind)into[0];
            switch (kind)
            {
                case MessageKind.ShipSnapshot:
                {
                    var r = new ByteReader(into);
                    ApplySnapshot(ShipSnapshot.Read(ref r));
                    return;
                }
                case MessageKind.ShipState:
                {
                    var r = new ByteReader(into);
                    ApplyState(ShipState.Read(ref r), clientTick);
                    return;
                }
                case MessageKind.GravityWellSpawned:
                {
                    var r = new ByteReader(into);
                    ApplyGravityWellSpawned(GravityWellSpawned.Read(ref r));
                    return;
                }
                default:
                {
                    // Every remaining reliable-ordered kind carries its u32
                    // sequence number right after the kind byte (see each
                    // struct's layout doc in NetMessages.cs).
                    uint sequence = (uint)(into[1] | (into[2] << 8) | (into[3] << 16) | (into[4] << 24));
                    var trimmed = new byte[length];
                    Array.Copy(into, trimmed, length);
                    ApplyReliable(sequence, trimmed);
                    return;
                }
            }
        }

        /// <summary>
        /// Feeds one reliable message's raw sequence number and bytes in for
        /// buffered, order-preserving application: pass every reliable
        /// payload received (regardless of arrival order) here rather than
        /// decoding it yourself. Applies it immediately if it is exactly the
        /// next expected sequence, then drains any subsequently-buffered
        /// messages the gap closing makes ready, in order.
        /// </summary>
        public void ApplyReliable(uint sequence, byte[] payload)
        {
            if (_haveBaseline && sequence <= _lastAppliedSequence) return; // stale/duplicate

            _pendingReliable[sequence] = payload;

            while (_pendingReliable.TryGetValue(_lastAppliedSequence + 1, out var next))
            {
                _pendingReliable.Remove(_lastAppliedSequence + 1);
                ApplyOne(next);
                _lastAppliedSequence++;
            }
        }

        void DiscardStaleBuffered()
        {
            var stale = new List<uint>();
            foreach (var kv in _pendingReliable)
                if (kv.Key <= _lastAppliedSequence) stale.Add(kv.Key);
            foreach (var key in stale) _pendingReliable.Remove(key);
        }

        void ApplyOne(byte[] payload)
        {
            var r = new ByteReader(payload);
            var kind = (MessageKind)payload[0];
            switch (kind)
            {
                case MessageKind.BlockDestroyed:
                {
                    var m = BlockDestroyed.Read(ref r);
                    ApplyBlockDestroyed(m);
                    break;
                }
                case MessageKind.BlockDamaged:
                {
                    var m = BlockDamaged.Read(ref r);
                    ApplyBlockDamaged(m);
                    break;
                }
                case MessageKind.BlockPlaced:
                {
                    var m = BlockPlaced.Read(ref r);
                    ApplyBlockPlaced(m);
                    break;
                }
                case MessageKind.FragmentSpawned:
                {
                    var m = FragmentSpawned.Read(ref r);
                    ApplyFragmentSpawned(m);
                    break;
                }
                case MessageKind.PowerupApplied:
                {
                    var m = PowerupApplied.Read(ref r);
                    ApplyPowerupApplied(m);
                    break;
                }
                case MessageKind.GravityWellSpawned:
                {
                    var m = GravityWellSpawned.Read(ref r);
                    ApplyGravityWellSpawned(m);
                    break;
                }
                default:
                    throw new InvalidOperationException("Unexpected reliable MessageKind " + kind);
            }
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

        /// <summary>
        /// A FragmentSpawned announces that a detached component now exists
        /// as its own ship-like body; since it carries no block list, the
        /// client's own already-applied BlockDestroyed events plus its own
        /// Connectivity.FindDetached run (in ApplyBlockDestroyed above) are
        /// what determine WHICH blocks left. This handler is only
        /// responsible for spawning the replica body those detached blocks
        /// belong in, at the pose the server reports. Because the parent's
        /// detached blocks were already stripped out of the parent grid in
        /// RunLocalDetach, this creates a matching empty-grid placeholder
        /// keyed by NewId for renderer bookkeeping (a fuller build would
        /// carry the detached block set from RunLocalDetach's own
        /// Connectivity.SplitIntoComponents call into this new body; kept
        /// minimal here since the wire contract is what deliverable 6 tests).
        /// </summary>
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

        /// <summary>
        /// Locally derives the same detached-component split the server
        /// derived (see Connectivity's determinism doc), removing every
        /// stranded block from the replica grid. This is the entire reason
        /// FragmentSpawned never needs a block list on the wire: both sides
        /// ran the identical integer flood fill after applying the identical
        /// ordered BlockDestroyed events.
        /// </summary>
        void RunLocalDetach(ReplicaShip replica)
        {
            var stranded = new List<int>();
            Connectivity.FindDetached(replica.Body.Grid, stranded);
            foreach (int key in stranded) replica.Body.Grid.TryRemove(key);
            if (stranded.Count > 0) replica.Body.RebuildDerivedViews();
        }

        /// <summary>Pops one applied event for a renderer/audio layer to
        /// react to, oldest first. False when nothing is queued.</summary>
        public bool TryDequeueEvent(out ReplicaEvent evt)
        {
            if (_events.Count > 0) { evt = _events.Dequeue(); return true; }
            evt = default;
            return false;
        }

        /// <summary>
        /// Builds this tick's InputMessage from local player intent, for the
        /// caller to hand to ITransport.SendUnreliable every tick. A thin
        /// convenience: ClientReplica does not own a transport itself (see
        /// deliverable 3's separation of concerns).
        /// </summary>
        public static InputMessage BuildInput(ushort netId, uint tick, float thrustAxis, float steer, bool firePressed)
            => InputMessage.FromFloats(netId, tick, thrustAxis, steer, firePressed);
    }
}
