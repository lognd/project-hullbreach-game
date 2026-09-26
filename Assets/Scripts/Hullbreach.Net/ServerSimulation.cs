using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Ship;
using Hullbreach.Ship.Behaviours;
using Hullbreach.Structure;
using Hullbreach.World;

namespace Hullbreach.Net
{
    // The authoritative, plain-C# server loop: one ShipBody + StructuralSolver
    // per connected peer, a shared GravityField, and a minimal point-body
    // projectile simulation (its own IWorldSink), all driven at a fixed tick
    // rate. Never touches ITransport directly: every Tick call fills the
    // IServerOutbox handed to the constructor, and whatever owns the real
    // transport drains that outbox and forwards it, so this class (and the
    // entire server simulation) compiles and tests without sockets existing.
    // frob:doc docs/reference/hullbreach-net.md#serversimulation
    public sealed class ServerSimulation
    {
        // Only used to derive Dt and the input timeout in ticks;
        // ServerSimulation never sleeps or measures wall-clock time itself.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public float TickRate = 50f;

        // Seconds without a fresh SetInput before a peer is dropped and its
        // ship removed (S47 criterion 2).
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public float TimeoutSeconds = 5f;

        float Dt => 1f / TickRate;

        readonly GravityField _gravity = new GravityField();

        // Exposed so a test/host can add permanent bodies (planets) before
        // the first Tick.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public GravityField Gravity => _gravity;

        sealed class PeerState
        {
            public ushort NetId;
            public ShipBody Ship;
            public StructuralSolver Solver;
            public InputMessage LatestInput;
            public bool HasFreshInput;
            public float SecondsSinceInput;
            public readonly Dictionary<int, bool> Failing = new Dictionary<int, bool>();
        }

        readonly Dictionary<int, PeerState> _peers = new Dictionary<int, PeerState>();
        readonly IServerOutbox _outbox;

        uint _sequence;
        uint _tickIndex;
        ushort _nextFragmentId = 1;

        sealed class ProjectileBody
        {
            public float2 Position;
            public float2 Velocity;
            public ProjectileSpec Spec;
            public float LifeRemaining;
        }

        readonly List<ProjectileBody> _projectiles = new List<ProjectileBody>();

        // This server's own IWorldSink: routes cannon shots into the
        // point-body projectile list above and gravity-well drops into the
        // shared GravityField (broadcasting a GravityWellSpawned event,
        // since a dropped well is a CAUSE clients cannot derive).
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

        // Re-broadcasts the snapshot (reliable) to every OTHER connected
        // peer so they can build a replica for the new ship; the joining
        // peer already has `initialDesign` locally and does not need it
        // echoed back.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public void Join(int peer, ShipSnapshot initialDesign)
        {
            // Catch the newcomer up on every ship that already exists,
            // before adding theirs to _peers, so the loop below (which
            // broadcasts the newcomer's own snapshot to everyone ELSE)
            // never doubles back and resends an existing ship to itself.
            foreach (var kv in _peers)
                EmitReliable(peer, BuildSnapshot(kv.Value));

            var ship = new ShipBody
            {
                Gravity = _gravity,
                World = _sink,
            };

            foreach (var b in initialDesign.Blocks)
            {
                int key = BlockKey.Pack(b.X, b.Y);
                ship.Grid.TryAdd(key, new Block(b.TypeId, b.Mods, b.Damage));
            }
            ship.Position = new float2(Quantization.UnpackPosition(initialDesign.Px), Quantization.UnpackPosition(initialDesign.Py));
            ship.Rotation = Quantization.UnpackAngle(initialDesign.Rot);
            ship.Velocity = new float2(Quantization.UnpackPosition(initialDesign.Vx), Quantization.UnpackPosition(initialDesign.Vy));
            ship.AngularVelocity = Quantization.UnpackAngle(initialDesign.Av);
            ship.RebuildDerivedViews();

            var state = new PeerState
            {
                NetId = (ushort)peer,
                Ship = ship,
                Solver = new StructuralSolver(),
                SecondsSinceInput = 0f,
            };
            _peers[peer] = state;

            // Every connected peer (including the joiner itself: it needs a
            // snapshot of its own ship exactly like everyone else does) gets
            // this ship's snapshot.
            var snapshot = BuildSnapshot(state);
            foreach (var kv in _peers) EmitReliable(kv.Key, snapshot);
        }

        // Safe to call for an unknown peer (no-op).
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public void Leave(int peer) => _peers.Remove(peer);

        // Latest-wins: a peer that sends every tick simply always has fresh
        // input, and one that drops a packet loses nothing but that tick's
        // precision.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public void SetInput(int peer, InputMessage input)
        {
            if (!_peers.TryGetValue(peer, out var state)) return;
            state.LatestInput = input;
            state.HasFreshInput = true;
            state.SecondsSinceInput = 0f;
        }

        // Advances every ship by one fixed tick: apply latest input, step
        // the ship body, tick its structural solver, resolve any damage/
        // detachment/buckling produced, then emit one ShipState (unreliable)
        // per surviving ship and reliable events for everything else this
        // tick produced, in a fixed order (peer id, then event kind) so
        // sequence numbers are reproducible given the same inputs.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public void Tick()
        {
            _tickIndex++;
            float dt = Dt;

            TimeoutStalePeers(dt);
            _gravity.Tick(dt);
            StepProjectiles(dt);

            var peerIds = new List<int>(_peers.Keys);
            peerIds.Sort();

            foreach (int peer in peerIds)
            {
                var state = _peers[peer];
                var input = state.HasFreshInput
                    ? new ShipInput(state.LatestInput.ThrustAxisFloat, state.LatestInput.SteerFloat, state.LatestInput.FirePressed)
                    : new ShipInput(0f, 0f, false);
                state.HasFreshInput = false;

                state.Ship.Step(input, dt);
                DrainShots(peer, state.Ship);

                var grid = state.Ship.Grid;
                if (grid.Count == 0) continue;

                state.Solver.Tick(grid, state.Ship.AppliedForcesThisStep, dt);
                ResolveStructuralFailures(peer, state);
            }

            foreach (int peer in peerIds)
            {
                if (!_peers.TryGetValue(peer, out var state)) continue; // may have been removed by a timeout above
                if (state.Ship.Grid.Count == 0) continue;
                EmitUnreliable(peer, BuildState(state));
            }
        }

        void TimeoutStalePeers(float dt)
        {
            List<int> dead = null;
            foreach (var kv in _peers)
            {
                kv.Value.SecondsSinceInput += dt;
                if (kv.Value.SecondsSinceInput > TimeoutSeconds)
                    (dead ??= new List<int>()).Add(kv.Key);
            }
            if (dead == null) return;
            foreach (int peer in dead) _peers.Remove(peer);
        }

        void DrainShots(int peer, ShipBody ship)
        {
            foreach (var shot in ship.PendingShots) _sink.SpawnProjectile(shot);
            ship.PendingShots.Clear();
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
                var ship = _peers[candidate].Ship;
                float2 local = ship.WorldToLocal(p.Position);
                int x = (int)math.floor(local.x);
                int y = (int)math.floor(local.y);
                if (!BlockKey.InRange(x, y)) continue;
                int k = BlockKey.Pack(x, y);
                if (ship.Grid.Contains(k))
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

            int newDamage = math.min(255, block.Damage + p.Spec.Damage);
            bool destroyed = newDamage >= 255 && key != grid.CoreKeyOrDefault();

            if (destroyed)
            {
                grid.TryRemove(key);
                BlockKey.Unpack(key, out int x, out int y);
                var evt = new BlockDestroyed(NextSequence(), state.NetId, (sbyte)x, (sbyte)y);
                BroadcastReliable(evt);
                ResolveDetachAfterDestruction(peer, state);
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
                bool shouldDetach = DamageModel.ShouldDetach(stress.DuctileRatio, stress.BrittleRatio, wasFailing);

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
                byte newDamage = DamageModel.Accumulate(block.Damage, stress.DuctileRatio, Dt);
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
                if (!grid.TryRemove(key)) continue;
                state.Failing.Remove(key);
                BlockKey.Unpack(key, out int x, out int y);
                BroadcastReliable(new BlockDestroyed(NextSequence(), state.NetId, (sbyte)x, (sbyte)y));
            }
            state.Solver.MarkTopologyChanged();
            ResolveDetachAfterDestruction(peer, state);
        }

        // Runs Connectivity.FindDetached/SplitIntoComponents once for the
        // whole batch (never per block), removes every stranded block from
        // the authoritative grid without individually announcing them
        // (clients derive the same set from the BlockDestroyed events
        // already broadcast), and broadcasts one FragmentSpawned per
        // resulting component so both sides spawn matching debris bodies.
        void ResolveDetachAfterDestruction(int peer, PeerState state)
        {
            var grid = state.Ship.Grid;
            var stranded = new List<int>();
            Connectivity.FindDetached(grid, stranded);
            if (stranded.Count == 0) { state.Ship.RebuildDerivedViews(); return; }

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

                foreach (int key in component) grid.TryRemove(key);

                ushort fragmentId = _nextFragmentId++;
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
        }

        // Test/debug hook: destroys the block at (x,y) as if a hit landed
        // there, going through the exact same broadcast + detach-resolution
        // path a real projectile impact would, so a test can force a split
        // without simulating the ballistics.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public void DebugDestroyBlock(int peer, int x, int y)
        {
            if (!_peers.TryGetValue(peer, out var state)) return;
            int key = BlockKey.Pack(x, y);
            if (!state.Ship.Grid.TryRemove(key)) return;
            state.Failing.Remove(key);
            BroadcastReliable(new BlockDestroyed(NextSequence(), state.NetId, (sbyte)x, (sbyte)y));
            state.Solver.MarkTopologyChanged();
            ResolveDetachAfterDestruction(peer, state);
        }

        ShipSnapshot BuildSnapshot(PeerState state)
        {
            var blocks = new List<SnapshotBlock>();
            foreach (var kvp in state.Ship.Grid.All)
            {
                BlockKey.Unpack(kvp.Key, out int x, out int y);
                blocks.Add(new SnapshotBlock((sbyte)x, (sbyte)y, kvp.Value.TypeId, kvp.Value.Modifiers, kvp.Value.Damage));
            }
            return new ShipSnapshot(
                NextSequence(), state.NetId, blocks.ToArray(),
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
            Quantization.PackAngle(state.Ship.AngularVelocity));

        uint NextSequence() => ++_sequence;

        void EmitUnreliable(int peer, ShipState state)
        {
            var buffer = new byte[ShipState.ByteSize];
            var w = new ByteWriter(buffer);
            state.Write(ref w);
            _outbox.Send(peer, reliable: false, buffer, w.Position);
        }

        void EmitReliable(int peer, ShipSnapshot snapshot)
        {
            var buffer = new byte[snapshot.ByteSize];
            var w = new ByteWriter(buffer);
            snapshot.Write(ref w);
            _outbox.Send(peer, reliable: true, buffer, w.Position);
        }

        // Everyone needs to know every ship's destruction/damage/fragment
        // events to keep their replicas in sync, not just the ship's own
        // owner.
        void BroadcastReliable<T>(T message) where T : struct
        {
            foreach (int peer in _peers.Keys)
            {
                var buffer = new byte[64];
                var w = new ByteWriter(buffer);
                WriteAny(message, ref w);
                _outbox.Send(peer, reliable: true, buffer, w.Position);
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
                case GravityWellSpawned m: m.Write(ref w); break;
                default: throw new InvalidOperationException("Unhandled reliable message type " + typeof(T));
            }
        }

        // The server's own IWorldSink: spawns cannon shots as simple point
        // bodies (no rigid body, no scene object) and routes gravity-well
        // drops into the shared field while broadcasting the cause.
        sealed class ServerWorldSink : IWorldSink
        {
            readonly ServerSimulation _owner;
            public ServerWorldSink(ServerSimulation owner) => _owner = owner;

            public void SpawnProjectile(in ShotRequest shot)
            {
                _owner._projectiles.Add(new ProjectileBody
                {
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

    // Small BlockGrid helper: the core key, or an out-of-range sentinel when
    // the grid has none, so a comparison against a candidate key never needs
    // a separate HasValue branch.
    static class BlockGridExtensions
    {
        public static int CoreKeyOrDefault(this BlockGrid grid) => grid.CoreKey ?? int.MinValue;
    }
}
