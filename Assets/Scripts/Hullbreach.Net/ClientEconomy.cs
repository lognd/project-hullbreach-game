using System;
using System.Collections.Generic;
using Hullbreach.Core;
using Hullbreach.World;
using Unity.Mathematics;

namespace Hullbreach.Net
{
    // A pickup as the HUD and minimap see it: where it is at a given server tick and what it holds.
    // frob:ticket 01M7EYG4KH9BT32Q4ERZVBQ3M4
    // frob:doc docs/reference/hullbreach-net.md#clientequipment
    public readonly struct PickupView
    {
        // frob:doc docs/reference/hullbreach-net.md#clientequipment
        public readonly uint Id;
        // frob:doc docs/reference/hullbreach-net.md#clientequipment
        public readonly PickupKind Kind;
        // frob:doc docs/reference/hullbreach-net.md#clientequipment
        public readonly byte Type;
        // frob:doc docs/reference/hullbreach-net.md#clientequipment
        public readonly byte Count;
        // frob:doc docs/reference/hullbreach-net.md#clientequipment
        public readonly float2 Position;
        // frob:doc docs/reference/hullbreach-net.md#clientequipment
        public readonly float2 Velocity;
        // Seconds until it vanishes if nobody takes it.
        // frob:doc docs/reference/hullbreach-net.md#clientequipment
        public readonly float SecondsLeft;

        // frob:doc docs/reference/hullbreach-net.md#clientequipment
        public PickupView(uint id, PickupKind kind, byte type, byte count, float2 position, float2 velocity, float secondsLeft)
        { Id = id; Kind = kind; Type = type; Count = count; Position = position; Velocity = velocity; SecondsLeft = secondsLeft; }
    }

    // The client's read-only picture of the block economy, fed by ClientReplica from server messages: stock,
    // pickups, the latest placement verdict, and a local preview of whether a placement would be accepted.
    // Nothing here changes server state; HUD and minimap code only read it.
    // frob:ticket 01M7EYG4KH9BT32Q4ERZVBQ3M4
    // frob:doc docs/reference/hullbreach-net.md#clientequipment
    public sealed class ClientEconomy
    {
        // Matches ServerSimulation.TickRate; used to turn ticks into seconds and positions.
        // frob:doc docs/reference/hullbreach-net.md#clientequipment
        public float TickRate = 50f;

        readonly Dictionary<uint, Pickup> _pickups = new Dictionary<uint, Pickup>();
        // Despawned ids, so a spawn that arrives after its own despawn cannot resurrect the pickup.
        readonly HashSet<uint> _gone = new HashSet<uint>();
        readonly Queue<PlacementResultMessage> _results = new Queue<PlacementResultMessage>();
        readonly Queue<PickupDespawned> _collections = new Queue<PickupDespawned>();
        byte[] _counts = new byte[0];
        bool _haveSnapshot;

        // frob:doc docs/reference/hullbreach-net.md#clientequipment
        public bool HasSnapshot => _haveSnapshot;
        // Server tick of the newest inventory snapshot.
        // frob:doc docs/reference/hullbreach-net.md#clientequipment
        public uint SnapshotTick { get; private set; }
        // Pre-match budget points unspent, or -1 during the match.
        // frob:doc docs/reference/hullbreach-net.md#clientequipment
        public int BudgetLeft { get; private set; } = -1;
        // frob:doc docs/reference/hullbreach-net.md#clientequipment
        public int BlockCount { get; private set; }
        // frob:doc docs/reference/hullbreach-net.md#clientequipment
        public int MaxBlocks { get; private set; }
        // frob:doc docs/reference/hullbreach-net.md#clientequipment
        public int CooldownTicksAtSnapshot { get; private set; }
        // Most recent build verdict from the server, if any.
        // frob:doc docs/reference/hullbreach-net.md#clientequipment
        public PlacementResultMessage? LastResult { get; private set; }

        // Spare blocks of a type.
        // frob:doc docs/reference/hullbreach-net.md#clientequipment
        public int Stock(byte typeId) => typeId < _counts.Length ? _counts[typeId] : 0;

        // frob:doc docs/reference/hullbreach-net.md#clientequipment
        public int TotalStock { get { int t = 0; foreach (byte c in _counts) t += c; return t; } }

        // Seconds of build cooldown left at `nowTick` (the replica's LatestServerTick).
        // frob:doc docs/reference/hullbreach-net.md#clientequipment
        public float CooldownSeconds(uint nowTick)
        {
            long left = (long)SnapshotTick + CooldownTicksAtSnapshot - nowTick;
            return left <= 0 ? 0f : left / TickRate;
        }

        // Pickups at `tick`, in id order, appended to `into` (cleared first). Allocation-free once `into` is sized.
        // frob:doc docs/reference/hullbreach-net.md#clientequipment
        public void GetPickups(uint tick, List<PickupView> into)
        {
            into.Clear();
            foreach (Pickup p in _pickups.Values)
                into.Add(new PickupView(p.Id, p.Kind, p.Type, p.Count, p.PositionAt((int)tick, TickRate), p.Velocity,
                    Math.Max(0f, (p.ExpireTick - (int)tick) / TickRate)));
            into.Sort((a, b) => a.Id.CompareTo(b.Id));
        }

        // frob:doc docs/reference/hullbreach-net.md#clientequipment
        public int PickupCount => _pickups.Count;

        // Dequeues the next build verdict for a toast, oldest first.
        // frob:doc docs/reference/hullbreach-net.md#clientequipment
        public bool TryDequeueResult(out PlacementResultMessage result) => _results.TryDequeue(out result);

        // Dequeues the next collected-pickup notice (any peer's), for pickup feedback; check Collector against your peer id.
        // frob:doc docs/reference/hullbreach-net.md#clientequipment
        public bool TryDequeueCollected(out PickupDespawned collected) => _collections.TryDequeue(out collected);

        // Would the server accept this placement? Mirrors the server's economy checks from local data (the
        // buckling dry-run needs the solver and stays server-side, so a WouldBuckle can still arrive).
        // frob:doc docs/reference/hullbreach-net.md#clientequipment
        public PlacementResult PreviewPlacement(BlockGrid grid, in EconomyConfig config, bool matchRunning, uint nowTick,
            int x, int y, byte typeId, byte modifiers, out byte detail)
        {
            int cooldownTicks = (int)Math.Max(0L, (long)SnapshotTick + CooldownTicksAtSnapshot - nowTick);
            int budget = EconomyRules.BudgetLeft(grid, config);
            return EconomyRules.CheckPlacement(grid, config, matchRunning, Stock(typeId), budget, cooldownTicks, x, y, typeId, modifiers, out detail);
        }

        internal void ApplySnapshot(in InventorySnapshot s)
        {
            if (_haveSnapshot && s.Tick < SnapshotTick) return;
            _haveSnapshot = true;
            SnapshotTick = s.Tick; _counts = s.Counts; BudgetLeft = s.BudgetLeft;
            BlockCount = s.BlockCount; MaxBlocks = s.MaxBlocks; CooldownTicksAtSnapshot = s.CooldownTicks;
        }

        internal void ApplyResult(in PlacementResultMessage m)
        {
            LastResult = m;
            if (_results.Count >= 16) _results.Dequeue();
            _results.Enqueue(m);
        }

        internal void ApplySpawn(in PickupSpawned m)
        {
            if (_gone.Contains(m.Pickup.Id)) return;
            _pickups[m.Pickup.Id] = m.Pickup;
        }

        internal void ApplyDespawn(in PickupDespawned m)
        {
            _pickups.Remove(m.Id);
            if (_gone.Count > 4096) _gone.Clear();
            _gone.Add(m.Id);
            if (m.Reason == PickupRemoval.Collected)
            {
                if (_collections.Count >= 16) _collections.Dequeue();
                _collections.Enqueue(m);
            }
        }
    }
}
