using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Builder;
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
    }

    // The authoritative, plain-C# server loop; see the reference page.
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

        // Netcode for Entities ghosts carry high-frequency pose data in the
        // Unity adapter. Plain transports and existing tests keep the legacy
        // ShipState stream enabled by default.
        public bool EmitPoseMessages = true;

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
            public bool IsBuilding;
            public bool HasInput;
            public bool PendingFire;
            public readonly Dictionary<int, bool> Failing = new Dictionary<int, bool>();
        }

        readonly Dictionary<int, PeerState> _peers = new Dictionary<int, PeerState>();
        readonly IServerOutbox _outbox;

        uint _sequence;
        uint _tickIndex;

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

        public void ConfigureDemoArena()
        {
            _gravity.Clear();
            _gravity.Add(new GravityBody(new float2(0f, -60f), 900f, 18f, 0.2f));
            _gravity.Add(new GravityBody(new float2(45f, 20f), 120f, 6f, 0.2f));

            var peers = new List<int>(_peers.Keys);
            peers.Sort();
            for (int i = 0; i < peers.Count; i++)
            {
                ShipBody ship = _peers[peers[i]].Ship;
                float angle = i * math.PI * 0.18f;
                float2 relative = new float2(math.sin(angle) * 12f, 30f + math.cos(angle) * 2f);
                ship.Position = new float2(0f, -60f) + relative;
                float speed = math.sqrt(900f / math.max(1f, math.length(relative)));
                float2 tangent = math.normalizesafe(new float2(-relative.y, relative.x));
                ship.Velocity = tangent * speed;
                ship.Rotation = angle;
                ship.AngularVelocity = 0f;
            }
        }

        public bool TryPlaceBlock(int peer, int x, int y, byte typeId, byte modifiers)
        {
            if (!_peers.TryGetValue(peer, out var state) || !state.IsBuilding ||
                typeId >= BlockTypes.Count || !BlockKey.InRange(x, y) ||
                state.Ship.Grid.Count >= ShipSnapshot.MaxBlocks) return false;

            int key = BlockKey.Pack(x, y);
            if (!PlacementRules.CanPlace(state.Ship.Grid, key, typeId, modifiers, out _)) return false;
            if (!state.Ship.Grid.TryAdd(key, new Block(typeId, modifiers))) return false;

            state.Ship.RebuildDerivedViews();
            state.Solver.MarkTopologyChanged();
            BroadcastReliable(new BlockPlaced(NextSequence(), state.NetId, (sbyte)x, (sbyte)y, typeId, modifiers));
            return true;
        }

        public bool TryRemoveBlock(int peer, int x, int y)
        {
            if (!_peers.TryGetValue(peer, out var state) || !state.IsBuilding || !BlockKey.InRange(x, y))
                return false;

            int key = BlockKey.Pack(x, y);
            if (!PlacementRules.CanRemove(state.Ship.Grid, key)) return false;

            var before = new HashSet<int>();
            foreach (int existingKey in state.Ship.Grid.SortedKeys) before.Add(existingKey);
            var removed = new List<int>();
            if (!PlacementRules.Detach(state.Ship.Grid, key, removed)) return false;
            removed.Sort();
            foreach (int removedKey in removed)
            {
                if (!before.Contains(removedKey)) continue;
                BlockKey.Unpack(removedKey, out int rx, out int ry);
                BroadcastReliable(new BlockDestroyed(NextSequence(), state.NetId, (sbyte)rx, (sbyte)ry));
                state.Failing.Remove(removedKey);
            }
            state.Ship.RebuildDerivedViews();
            state.Solver.MarkTopologyChanged();
            return true;
        }

        // Validates the untrusted design, then re-broadcasts the new snapshot
        // to every other peer; see reference page. Rejections change nothing.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        // frob:invariant INV-003
        public JoinResult Join(int peer, ShipSnapshot initialDesign)
        {
            if (peer < 1 || peer > MaxPeerId) return JoinResult.InvalidPeerId;
            if (_peers.ContainsKey(peer)) return JoinResult.AlreadyJoined;
            if (DesignValidator.ValidateDesign(initialDesign) != DesignVerdict.Ok) return JoinResult.InvalidDesign;

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
            return JoinResult.Joined;
        }

        // Safe to call for an unknown peer (no-op); tells the others the ship is gone.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public void Leave(int peer)
        {
            if (_peers.TryGetValue(peer, out var state)) RemovePeer(peer, state);
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
            if (state.HasInput && unchecked((int)(input.Tick - state.LatestInput.Tick)) < 0) return;
            state.LatestInput = input;
            state.HasInput = true;
            state.IsBuilding = input.BuildMode;
            state.HasFreshInput = true;
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
            _gravity.Tick(dt);
            StepProjectiles(dt);

            var peerIds = new List<int>(_peers.Keys);
            peerIds.Sort();

            foreach (int peer in peerIds)
            {
                var state = _peers[peer];
                var input = state.HasFreshInput
                    ? new ShipInput(state.LatestInput.ThrustAxisFloat, state.LatestInput.SteerFloat, state.PendingFire)
                    : new ShipInput(0f, 0f, false);
                state.HasFreshInput = false;
                state.PendingFire = false;

                if (state.IsBuilding) continue;

                state.Ship.Step(input, dt);
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

            foreach (int peer in peerIds)
            {
                if (!_peers.TryGetValue(peer, out var state)) continue; // may have been removed by a timeout above
                if (state.Ship.Grid.Count == 0) continue;
                if (EmitPoseMessages) EmitUnreliable(peer, BuildState(state));
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
                kv.Value.SecondsSinceInput += dt;
                if (kv.Value.SecondsSinceInput > TimeoutSeconds)
                    (dead ??= new List<int>()).Add(kv.Key);
            }
            if (dead == null) return;
            foreach (int peer in dead)
            {
                RemovePeer(peer, _peers[peer]);
                _droppedPeers.Enqueue(peer);
            }
        }

        void DrainShots(int peer, ShipBody ship)
        {
            foreach (var shot in ship.PendingShots) SpawnProjectile(peer, ship, shot);
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
                if (ProjectileHitTest.TryHitShipCell(_peers[candidate].Ship, p.Position, p.Spec.Radius, out int k))
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

        // Runs FindDetached/SplitIntoComponents once per batch; see reference page.
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
            if (!state.Ship.Grid.TryRemove(key)) return;
            state.Failing.Remove(key);
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

        // Broadcasts to every peer, not just the ship's own owner.
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
