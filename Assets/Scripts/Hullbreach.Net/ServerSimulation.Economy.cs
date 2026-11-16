using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Hullbreach.Builder;
using Hullbreach.Core;
using Hullbreach.Ship;
using Hullbreach.World;

namespace Hullbreach.Net
{
    // The block economy half of the server (S45): build budget before the match, stock and pickups during it.
    // Kept in its own file so the main loop stays readable; see docs/design/block-economy.md for the why.
    // frob:ticket 01M7EYG4KH9BT32Q4ERZVBQ3M4
    public sealed partial class ServerSimulation
    {
        sealed partial class PeerState
        {
            // Spare blocks; replaced with the starter kit when the match starts.
            public Inventory Stock = new Inventory();
            // Blocks still "under construction": key -> damage points left to heal off.
            public readonly Dictionary<int, int> Construction = new Dictionary<int, int>();
            public float TrickleCredit;
            public int TrickleGranted;
            public bool InventoryDirty = true;
        }

        // Prices, caps, salvage, refund and trickle numbers; replaced by the arena config in ConfigureDemoArena.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public EconomyConfig Economy = EconomyConfig.Default();

        PickupField _pickups;
        readonly List<PickupEvent> _pickupEvents = new List<PickupEvent>();
        uint _economySeed;

        // Pickups currently in the field, in id order; empty outside a live economy match. Read-only: bots and
        // the minimap use it, only the server collects.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public IReadOnlyList<Pickup> Pickups => _pickups != null ? _pickups.Active : (IReadOnlyList<Pickup>)Array.Empty<Pickup>();

        // The peer's spare blocks per block type (a copy); empty for an unknown peer.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public int[] StockOf(int peer) => _peers.TryGetValue(peer, out var s) ? s.Stock.ToArray() : new int[BlockTypes.Count];

        // Pre-match points the peer can still spend (the budget minus its ship's cost).
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public int BudgetLeftOf(int peer) =>
            _peers.TryGetValue(peer, out var s) ? EconomyRules.BudgetLeft(s.Ship.Grid, Economy) : 0;

        // Whole ticks until the peer may build again; 0 when ready.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public int BuildCooldownTicksRemaining(int peer) =>
            _peers.TryGetValue(peer, out var s) ? CooldownTicks(s) : 0;

        int CooldownTicks(PeerState state)
        {
            if (BuildCooldownSeconds <= 0f || !MatchIsRunning) return 0;
            long left = (long)state.NextBuildTick - _tickIndex;
            return left <= 0 ? 0 : (int)left;
        }

        // Test and scenario hook: puts blocks in a peer's stock without a source. Not for gameplay code.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public bool GrantStock(int peer, byte typeId, int count)
        {
            if (!_peers.TryGetValue(peer, out var s) || !s.Stock.TryAdd(typeId, count)) return false;
            s.InventoryDirty = true;
            return true;
        }

        // Would PlaceBlock accept this right now? Read-only (no cooldown, stock or trust is touched), so a
        // bot or HUD can ask first. detail carries the PlacementVerdict byte for a rule refusal.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public PlacementResult CheckPlacement(int peer, int x, int y, byte typeId, byte modifiers, out byte detail)
        {
            detail = 0;
            if (!_peers.TryGetValue(peer, out var state) || state.Trust.AtFloor) return PlacementResult.NoShip;
            return EvaluatePlacement(state, x, y, typeId, modifiers, out detail);
        }

        PlacementResult EvaluatePlacement(PeerState state, int x, int y, byte typeId, byte modifiers, out byte detail)
        {
            detail = 0;
            if (!state.IsBuilding) return PlacementResult.NotBuilding;
            bool running = MatchIsRunning;
            BlockGrid grid = state.Ship.Grid;
            PlacementResult r = EconomyRules.CheckPlacement(grid, Economy, running, state.Stock.Get(typeId),
                EconomyRules.BudgetLeft(grid, Economy), CooldownTicks(state), x, y, typeId, modifiers, out detail);
            if (r != PlacementResult.Ok) return r;
            if (!Economy.Enabled && grid.Count >= ShipSnapshot.MaxBlocks) return PlacementResult.BadRequest;
            if (running && Economy.Enabled
                && EconomyRules.WouldFailImmediately(grid, BlockKey.Pack(x, y), new Block(typeId, modifiers, ConstructionDamage()),
                    state.Ship.AppliedForcesThisStep, Dt))
                return PlacementResult.WouldBuckle;
            return PlacementResult.Ok;
        }

        byte ConstructionDamage() => Economy.ConstructionSeconds > 0f ? Economy.ConstructionDamage : (byte)0;

        // Places one block. Mid-match it spends one from stock and the block starts fragile (see EconomyConfig);
        // before the match it spends budget points instead. Refusals other than a malformed request or building
        // outside build mode are never distrusted; the owner gets a PlacementResultMessage either way.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public PlacementResult PlaceBlock(int peer, int x, int y, byte typeId, byte modifiers)
        {
            if (!_peers.TryGetValue(peer, out var state) || state.Trust.AtFloor) return PlacementResult.NoShip;
            PlacementResult result = EvaluatePlacement(state, x, y, typeId, modifiers, out byte detail);
            if (result != PlacementResult.Ok)
            {
                if (result == PlacementResult.NotBuilding) Distrust(peer, state, TrustEventKind.BuildOutsideBuildMode);
                else if (result == PlacementResult.BadRequest) Distrust(peer, state, TrustEventKind.RejectedBuild);
                SendPlacementResult(peer, state, BuildAction.Place, x, y, typeId, result, detail);
                return result;
            }

            bool live = MatchIsRunning && Economy.Enabled;
            if (live && !state.Stock.TryTake(typeId)) result = PlacementResult.OutOfStock;
            byte damage = live ? ConstructionDamage() : (byte)0;
            int key = BlockKey.Pack(x, y);
            if (result == PlacementResult.Ok && !state.Ship.Grid.TryAdd(key, new Block(typeId, modifiers, damage)))
            {
                if (live) state.Stock.TryAdd(typeId);
                result = PlacementResult.BadRequest;
            }
            if (result != PlacementResult.Ok)
            {
                SendPlacementResult(peer, state, BuildAction.Place, x, y, typeId, result, 0);
                return result;
            }

            if (BuildCooldownSeconds > 0f && MatchIsRunning)
                state.NextBuildTick = _tickIndex + (uint)Math.Ceiling(BuildCooldownSeconds * TickRate);
            state.Ship.RebuildDerivedViews();
            state.Solver.MarkTopologyChanged();
            Tally(peer, 0, 0, 1);
            BroadcastReliable(new BlockPlaced(NextSequence(), state.NetId, (sbyte)x, (sbyte)y, typeId, modifiers));
            if (damage > 0)
            {
                state.Construction[key] = damage;
                BroadcastReliable(new BlockDamaged(NextSequence(), state.NetId, (sbyte)x, (sbyte)y, damage));
            }
            state.InventoryDirty = true;
            SendPlacementResult(peer, state, BuildAction.Place, x, y, typeId, PlacementResult.Ok, 0);
            return PlacementResult.Ok;
        }

        // Replaces the peer's whole ship with a saved or template design (one-click starters, S45-style "last design").
        // Only before the match: once it runs, building goes through the stock rules. The design is untrusted, so it is
        // replayed through the placement rules, the net design validator and the economy before anything changes.
        // A refusal changes nothing and is not distrusted: a stale client can honestly send an old design.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public PlacementResult ApplyDesign(int peer, IReadOnlyList<DesignBlock> design)
        {
            if (!_peers.TryGetValue(peer, out var state) || state.Trust.AtFloor) return PlacementResult.NoShip;
            if (MatchIsRunning) return PlacementResult.NotBuilding;
            if (design == null || !ShipDesignCodec.TryBuildGrid(design, out BlockGrid target, out _)) return PlacementResult.BadRequest;

            var wire = new SnapshotBlock[target.Count];
            int n = 0;
            foreach (var kv in target.All)
            {
                BlockKey.Unpack(kv.Key, out int bx, out int by);
                wire[n++] = new SnapshotBlock((sbyte)bx, (sbyte)by, kv.Value.TypeId, kv.Value.Modifiers, 0);
            }
            var probe = new ShipSnapshot(0, (ushort)peer, wire, 0, 0, 0, 0, 0, 0);
            if (DesignValidator.ValidateDesign(probe) != DesignVerdict.Ok || CheckDesignAgainstEconomy(probe).HasValue)
                return PlacementResult.BadRequest;

            BlockGrid grid = state.Ship.Grid;
            if (ShipDesignCodec.Encode(grid) == ShipDesignCodec.Encode(target)) return PlacementResult.Ok;
            var stale = new List<int>();
            foreach (var kv in grid.All)
            {
                // Every block comes out and goes back in, not just the changed ones: a client drops blocks that lose their
                // link to the core when a neighbour is destroyed, and would never redraw the ones left standing.
                if (kv.Value.TypeId != BlockTypes.Core) stale.Add(kv.Key);
            }
            stale.Sort();
            foreach (int key in stale)
            {
                grid.TryRemove(key);
                state.Failing.Remove(key);
                state.Construction.Remove(key);
                state.StormCarry.Remove(key);
                BlockKey.Unpack(key, out int rx, out int ry);
                BroadcastReliable(new BlockDestroyed(NextSequence(), state.NetId, (sbyte)rx, (sbyte)ry));
            }
            var fresh = new List<int>();
            foreach (var kv in target.All) if (!grid.Contains(kv.Key)) fresh.Add(kv.Key);
            // Nearest the core first so every block is adjacent to one that is already in.
            fresh.Sort((a, b) =>
            {
                BlockKey.Unpack(a, out int ax, out int ay); BlockKey.Unpack(b, out int bx2, out int by2);
                int da = Math.Abs(ax) + Math.Abs(ay), db = Math.Abs(bx2) + Math.Abs(by2);
                return da != db ? da.CompareTo(db) : a.CompareTo(b);
            });
            foreach (int key in fresh)
            {
                target.TryGet(key, out Block add);
                grid.TryAdd(key, new Block(add.TypeId, add.Modifiers, 0));
                BlockKey.Unpack(key, out int px, out int py);
                BroadcastReliable(new BlockPlaced(NextSequence(), state.NetId, (sbyte)px, (sbyte)py, add.TypeId, add.Modifiers));
            }
            state.Ship.RebuildDerivedViews();
            state.Solver.MarkTopologyChanged();
            state.InventoryDirty = true;
            Tally(peer, 0, 0, fresh.Count);
            return PlacementResult.Ok;
        }

        // Takes one of the peer's own blocks back. Mid-match it pays EconomyConfig.RefundPercent of a block (so two
        // deconstructs make one block, never a free one), shares the build cooldown, and any blocks it strands
        // become salvage instead of vanishing. Core and welded blocks refuse.
        // frob:doc docs/reference/hullbreach-net.md#serversimulation
        public PlacementResult Deconstruct(int peer, int x, int y)
        {
            if (!_peers.TryGetValue(peer, out var state) || state.Trust.AtFloor) return PlacementResult.NoShip;
            if (!state.IsBuilding)
            {
                Distrust(peer, state, TrustEventKind.BuildOutsideBuildMode);
                SendPlacementResult(peer, state, BuildAction.Deconstruct, x, y, 0, PlacementResult.NotBuilding, 0);
                return PlacementResult.NotBuilding;
            }
            if (!BlockKey.InRange(x, y))
            {
                Distrust(peer, state, TrustEventKind.RejectedBuild);
                return PlacementResult.BadRequest;
            }
            bool live = MatchIsRunning && Economy.Enabled;
            if (live && CooldownTicks(state) > 0)
            {
                SendPlacementResult(peer, state, BuildAction.Deconstruct, x, y, 0, PlacementResult.Cooldown, 0);
                return PlacementResult.Cooldown;
            }

            int key = BlockKey.Pack(x, y);
            BlockGrid grid = state.Ship.Grid;
            if (!PlacementRules.CanRemove(grid, key) || !grid.TryGet(key, out Block target))
            {
                Distrust(peer, state, TrustEventKind.RejectedBuild);
                SendPlacementResult(peer, state, BuildAction.Deconstruct, x, y, 0, PlacementResult.CannotRemove, 0);
                return PlacementResult.CannotRemove;
            }

            var before = new Dictionary<int, Block>();
            foreach (var kv in grid.All) before[kv.Key] = kv.Value;
            var removed = new List<int>();
            if (!PlacementRules.Detach(grid, key, removed)) return PlacementResult.CannotRemove;
            removed.Sort();
            foreach (int removedKey in removed)
            {
                if (!before.TryGetValue(removedKey, out Block gone)) continue;
                BlockKey.Unpack(removedKey, out int rx, out int ry);
                BroadcastReliable(new BlockDestroyed(NextSequence(), state.NetId, (sbyte)rx, (sbyte)ry));
                state.Failing.Remove(removedKey);
                state.Construction.Remove(removedKey);
                if (removedKey != key) DropSalvage(state, removedKey, gone);
            }
            state.Ship.RebuildDerivedViews();
            state.Solver.MarkTopologyChanged();

            if (live)
            {
                state.Stock.AddRefundCredit(target.TypeId, Economy.RefundPercent);
                if (BuildCooldownSeconds > 0f)
                    state.NextBuildTick = _tickIndex + (uint)Math.Ceiling(BuildCooldownSeconds * TickRate);
            }
            state.InventoryDirty = true;
            FlushPickupEvents();
            Tally(peer, 0, 0, 0, 1);
            SendPlacementResult(peer, state, BuildAction.Deconstruct, x, y, target.TypeId, PlacementResult.Ok, 0);
            return PlacementResult.Ok;
        }

        void SendPlacementResult(int peer, PeerState state, BuildAction action, int x, int y, byte typeId,
            PlacementResult result, byte detail)
        {
            var msg = new PlacementResultMessage(action, (sbyte)x, (sbyte)y, typeId, result, detail,
                (ushort)Math.Min(ushort.MaxValue, CooldownTicks(state)));
            var buffer = new byte[PlacementResultMessage.ByteSize];
            var w = new ByteWriter(buffer);
            msg.Write(ref w);
            SendTo(peer, reliable: true, buffer, w.Position);
        }

        // Validates a joining design against the economy; null when it fits.
        JoinResult? CheckDesignAgainstEconomy(ShipSnapshot design)
        {
            if (!Economy.Enabled) return null;
            if (design.Blocks.Length > Economy.Costs.MaxBlocks) return JoinResult.DesignTooLarge;
            long cost = 0;
            foreach (var b in design.Blocks) cost += Economy.Costs.CostOf(b.TypeId);
            return cost > Economy.Costs.Budget ? JoinResult.OverBudget : (JoinResult?)null;
        }

        // Resets stock and builds the pickup field when a match starts.
        void StartEconomy()
        {
            _pickups = null;
            _pickupEvents.Clear();
            foreach (var kv in _peers)
            {
                PeerState state = kv.Value;
                state.Stock = new Inventory(Economy.MaxStockPerType);
                state.Construction.Clear();
                state.TrickleCredit = 0f;
                state.TrickleGranted = 0;
                if (Economy.Enabled)
                    for (byte t = 1; t < BlockTypes.Count && t < Economy.StartingStock.Length; t++)
                        state.Stock.TryAdd(t, Economy.StartingStock[t]);
                state.InventoryDirty = true;
            }
            if (!Economy.Enabled) return;
            _economySeed = StormSeedFor(Match.MatchId) ^ 0x5CA77E55u;
            _pickups = new PickupField(Economy.Pickups, _economySeed, TickRate, (int)_tickIndex, Bounds.Center,
                Bounds.Radius, _gravity, ActiveStorm, (int)_stormStartTick);
        }

        // One tick of the economy: pickups drift and spawn, ships collect, fresh blocks harden, the comeback trickle
        // pays out, and changed inventories go to their owners.
        void TickEconomy(List<int> peerIds)
        {
            if (!Economy.Enabled) return;
            if (MatchIsRunning && _pickups != null)
            {
                int tick = (int)_tickIndex;
                _pickups.Tick(tick, _pickupEvents);
                CollectPickups(peerIds, tick);
                HardenNewBlocks(peerIds);
                Trickle(peerIds);
                FlushPickupEvents();
            }
            foreach (int peer in peerIds)
            {
                if (!_peers.TryGetValue(peer, out var state) || !state.InventoryDirty) continue;
                state.InventoryDirty = false;
                SendInventory(peer, state);
            }
        }

        void SendInventory(int peer, PeerState state)
        {
            int[] counts = state.Stock.ToArray();
            var bytes = new byte[counts.Length];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)Math.Min(255, counts[i]);
            short budget = MatchIsRunning ? (short)-1 : (short)Math.Max(short.MinValue + 1, Math.Min(short.MaxValue, EconomyRules.BudgetLeft(state.Ship.Grid, Economy)));
            var snapshot = new InventorySnapshot(_tickIndex, bytes, budget, (ushort)state.Ship.Grid.Count,
                (ushort)Economy.Costs.MaxBlocks, (ushort)Math.Min(ushort.MaxValue, CooldownTicks(state)));
            var buffer = new byte[snapshot.ByteSize];
            var w = new ByteWriter(buffer);
            snapshot.Write(ref w);
            SendTo(peer, reliable: true, buffer, w.Position);
        }

        // Each pickup goes to the ship whose nearest block is closest (lower peer id on an exact tie), and only if
        // that ship is slow enough relative to it, has no planet in the way, and has shelf space for it.
        void CollectPickups(List<int> peerIds, int tick)
        {
            if (_pickups.Active.Count == 0) return;
            var points = new Dictionary<int, List<float2>>();
            foreach (int peer in peerIds)
            {
                if (!_peers.TryGetValue(peer, out var s) || s.Ship.Grid.Count == 0) continue;
                var list = new List<float2>(s.Ship.Grid.Count);
                foreach (var kv in s.Ship.Grid.All) list.Add(s.Ship.LocalToWorld(BlockGrid.CenterOf(kv.Key)));
                points[peer] = list;
            }
            var snapshot = new List<Pickup>(_pickups.Active);
            foreach (Pickup p in snapshot)
            {
                int best = -1;
                float bestDistance = float.PositiveInfinity;
                foreach (int peer in peerIds)
                {
                    if (!points.TryGetValue(peer, out var list)) continue;
                    PeerState s = _peers[peer];
                    if (!s.Stock.CanAdd(p.Type, p.Count)) continue;
                    float d = _pickups.Reach(p, tick, list, s.Ship.Velocity);
                    if (d < bestDistance) { bestDistance = d; best = peer; }
                }
                if (best < 0) continue;
                if (!_pickups.Remove(p.Id, PickupRemoval.Collected, best, _pickupEvents)) continue;
                _peers[best].Stock.TryAdd(p.Type, p.Count);
                _peers[best].InventoryDirty = true;
            }
        }

        // A fresh block starts fragile and recovers in half-second steps; damage taken meanwhile stays.
        void HardenNewBlocks(List<int> peerIds)
        {
            int step = Math.Max(1, (int)(TickRate * 0.5f));
            if (_tickIndex % (uint)step != 0) return;
            int steps = Math.Max(1, (int)Math.Ceiling(Economy.ConstructionSeconds / 0.5f));
            int amount = Math.Max(1, (Economy.ConstructionDamage + steps - 1) / steps);
            foreach (int peer in peerIds)
            {
                if (!_peers.TryGetValue(peer, out var state) || state.Construction.Count == 0) continue;
                var keys = new List<int>(state.Construction.Keys);
                keys.Sort();
                foreach (int key in keys)
                {
                    if (!state.Ship.Grid.TryGet(key, out Block block)) { state.Construction.Remove(key); continue; }
                    int left = state.Construction[key];
                    int heal = Math.Min(Math.Min(amount, left), block.Damage);
                    if (heal > 0)
                    {
                        state.Ship.Grid.TrySet(key, block.WithDamage((byte)(block.Damage - heal)));
                        BlockKey.Unpack(key, out int x, out int y);
                        BroadcastReliable(new BlockDamaged(NextSequence(), state.NetId, (sbyte)x, (sbyte)y, (byte)(block.Damage - heal)));
                    }
                    left -= amount;
                    if (left <= 0) state.Construction.Remove(key); else state.Construction[key] = left;
                }
            }
        }

        // Comeback income: only the ship with fewer blocks earns, faster the further behind it is, a hull block at a
        // time, never past TrickleStockCap spare blocks or TrickleMatchCap blocks per match.
        void Trickle(List<int> peerIds)
        {
            if (Economy.TrickleSeconds <= 0f || peerIds.Count != 2) return;
            for (int i = 0; i < 2; i++)
            {
                if (!_peers.TryGetValue(peerIds[i], out var me) || !_peers.TryGetValue(peerIds[1 - i], out var them)) return;
                int deficit = them.Ship.Grid.Count - me.Ship.Grid.Count;
                if (deficit <= 0 || me.Ship.Grid.Count == 0) continue;
                float multiplier = Math.Min(Economy.TrickleMaxMultiplier, 1f + deficit * Economy.TrickleDeficitStep);
                me.TrickleCredit = Math.Min(1f, me.TrickleCredit + multiplier / (Economy.TrickleSeconds * TickRate));
                if (me.TrickleCredit < 1f) continue;
                if (me.TrickleGranted >= Economy.TrickleMatchCap || me.Stock.Total >= Economy.TrickleStockCap) continue;
                if (!me.Stock.TryAdd(BlockTypes.Hull)) continue;
                me.TrickleCredit -= 1f;
                me.TrickleGranted++;
                me.InventoryDirty = true;
            }
        }

        // A destroyed block may leave a pickup drifting with the wreck's velocity plus a small fan-out. Whether it does is
        // a pure function of the match seed, tick and cell, so a replay agrees; at most one pickup per block, so
        // destruction can never mint stock.
        void DropSalvage(PeerState state, int key, Block block)
        {
            if (_pickups == null || !MatchIsRunning || !Economy.Enabled || block.TypeId == BlockTypes.Core) return;
            uint h = PickupField.Hash(_economySeed, _tickIndex, (uint)key * 31u + state.NetId);
            if (h % 100u >= (uint)Economy.SalvagePercent) return;
            ShipBody ship = state.Ship;
            float2 world = ship.LocalToWorld(BlockGrid.CenterOf(key));
            float2 r = world - ship.Position;
            float2 velocity = ship.Velocity + ship.AngularVelocity * new float2(-r.y, r.x);
            float angle = ((h >> 8) & 0xFFFFu) / 65536f * 2f * math.PI;
            float speed = Economy.SalvageSpread * (0.5f + 0.5f * (((h >> 24) & 0xFFu) / 255f));
            velocity += new float2(math.cos(angle), math.sin(angle)) * speed;
            _pickups.AddSalvage((int)_tickIndex, block.TypeId, world, velocity, _pickupEvents);
        }

        void FlushPickupEvents()
        {
            if (_pickupEvents.Count == 0) return;
            var events = new List<PickupEvent>(_pickupEvents);
            _pickupEvents.Clear();
            foreach (PickupEvent e in events)
            {
                if (e.Spawned) BroadcastReliable(new PickupSpawned(NextSequence(), e.Pickup));
                else BroadcastReliable(new PickupDespawned(NextSequence(), e.Pickup.Id, e.Reason, (ushort)e.Collector));
            }
        }
    }
}
