using Hullbreach.Core;
using Hullbreach.World;
using Unity.Mathematics;

namespace Hullbreach.Net
{
    // What an attempt to place or deconstruct a block came to. Everything except the first four members is a
    // normal, never-distrusted refusal a HUD can explain; BadRequest and NotBuilding are the cheating cases.
    // frob:ticket 01M7EYG4KH9BT32Q4ERZVBQ3M4
    // frob:doc docs/reference/hullbreach-net.md#placementresult
    public enum PlacementResult : byte
    {
        Ok = 0,
        // The peer has no ship, or has been cut off for distrust.
        NoShip = 1,
        // The request came while not in build mode (distrusted).
        NotBuilding = 2,
        // Malformed: unknown type, coordinates off the grid, or a rule breach (distrusted).
        BadRequest = 3,
        // Still inside the build cooldown; the message carries the ticks left.
        Cooldown = 4,
        // No spare block of that type in stock.
        OutOfStock = 5,
        // Would exceed the ship size cap.
        SizeCap = 6,
        // Pre-match only: not enough budget points left.
        OverBudget = 7,
        // The core cannot be built mid-match (or at all, once it exists).
        CoreNotBuildable = 8,
        // The cell touches no block of the ship, so the new block would float free.
        Disconnected = 9,
        // The structure solver says the new block would fail or buckle at once.
        WouldBuckle = 10,
        // Deconstruct only: the block is the core, welded, or absent.
        CannotRemove = 11,
    }

    // Whether the request was a place or a deconstruct.
    // frob:doc docs/reference/hullbreach-net.md#placementresult
    public enum BuildAction : byte
    {
        Place = 0,
        Deconstruct = 1,
    }

    // Server -> owning peer only, no sequence number (it does not take part in the shared reliable stream):
    // the outcome of one build request, so the HUD can show why a placement was refused.
    // frob:doc docs/reference/hullbreach-net.md#placementresultmessage
    public readonly struct PlacementResultMessage
    {
        // frob:doc docs/reference/hullbreach-net.md#placementresultmessage
        public const int ByteSize = 1 + 1 + 1 + 1 + 1 + 1 + 1 + 2;

        // frob:doc docs/reference/hullbreach-net.md#placementresultmessage
        public readonly BuildAction Action;
        // frob:doc docs/reference/hullbreach-net.md#placementresultmessage
        public readonly sbyte X;
        // frob:doc docs/reference/hullbreach-net.md#placementresultmessage
        public readonly sbyte Y;
        // frob:doc docs/reference/hullbreach-net.md#placementresultmessage
        public readonly byte TypeId;
        // frob:doc docs/reference/hullbreach-net.md#placementresultmessage
        public readonly PlacementResult Result;
        // The PlacementVerdict byte for a rule refusal, else 0.
        // frob:doc docs/reference/hullbreach-net.md#placementresultmessage
        public readonly byte Detail;
        // Ticks of cooldown left after this request (0 when ready).
        // frob:doc docs/reference/hullbreach-net.md#placementresultmessage
        public readonly ushort CooldownTicks;

        // frob:doc docs/reference/hullbreach-net.md#placementresultmessage
        public PlacementResultMessage(BuildAction action, sbyte x, sbyte y, byte typeId, PlacementResult result,
            byte detail, ushort cooldownTicks)
        {
            Action = action; X = x; Y = y; TypeId = typeId; Result = result; Detail = detail; CooldownTicks = cooldownTicks;
        }

        // frob:doc docs/reference/hullbreach-net.md#placementresultmessage
        public bool Accepted => Result == PlacementResult.Ok;

        // frob:doc docs/reference/hullbreach-net.md#placementresultmessage
        public void Write(ref ByteWriter w)
        {
            w.WriteU8((byte)MessageKind.PlacementResult);
            w.WriteU8((byte)Action); w.WriteI8(X); w.WriteI8(Y); w.WriteU8(TypeId);
            w.WriteU8((byte)Result); w.WriteU8(Detail); w.WriteU16(CooldownTicks);
        }

        // Reads the kind byte too; an out-of-range result fails the reader.
        // frob:doc docs/reference/hullbreach-net.md#placementresultmessage
        public static PlacementResultMessage Read(ref ByteReader r)
        {
            r.ReadU8();
            byte action = r.ReadU8(); sbyte x = r.ReadI8(), y = r.ReadI8(); byte type = r.ReadU8();
            byte result = r.ReadU8(); byte detail = r.ReadU8(); ushort cooldown = r.ReadU16();
            if (action > (byte)BuildAction.Deconstruct || result > (byte)PlacementResult.CannotRemove) r.Fail();
            return new PlacementResultMessage((BuildAction)action, x, y, type, (PlacementResult)result, detail, cooldown);
        }
    }

    // Server -> owning peer only, no sequence number, latest-wins by Tick: the peer's stock and build limits.
    // Sent whenever stock, cooldown, or the ship's size changes, and on request via the match start.
    // frob:doc docs/reference/hullbreach-net.md#inventorysnapshot
    public readonly struct InventorySnapshot
    {
        // frob:doc docs/reference/hullbreach-net.md#inventorysnapshot
        public const int MaxTypes = 16;

        // frob:doc docs/reference/hullbreach-net.md#inventorysnapshot
        public readonly uint Tick;
        // Spare blocks per block type; the core entry is always 0.
        // frob:doc docs/reference/hullbreach-net.md#inventorysnapshot
        public readonly byte[] Counts;
        // Pre-match budget points still unspent, or -1 once the match is running.
        // frob:doc docs/reference/hullbreach-net.md#inventorysnapshot
        public readonly short BudgetLeft;
        // Blocks on the ship now and the cap.
        // frob:doc docs/reference/hullbreach-net.md#inventorysnapshot
        public readonly ushort BlockCount;
        // frob:doc docs/reference/hullbreach-net.md#inventorysnapshot
        public readonly ushort MaxBlocks;
        // Build cooldown left at Tick.
        // frob:doc docs/reference/hullbreach-net.md#inventorysnapshot
        public readonly ushort CooldownTicks;

        // frob:doc docs/reference/hullbreach-net.md#inventorysnapshot
        public InventorySnapshot(uint tick, byte[] counts, short budgetLeft, ushort blockCount, ushort maxBlocks, ushort cooldownTicks)
        {
            Tick = tick; Counts = counts ?? new byte[0]; BudgetLeft = budgetLeft;
            BlockCount = blockCount; MaxBlocks = maxBlocks; CooldownTicks = cooldownTicks;
        }

        // frob:doc docs/reference/hullbreach-net.md#inventorysnapshot
        public int ByteSize => 1 + 4 + 1 + Counts.Length + 2 + 2 + 2 + 2;

        // frob:doc docs/reference/hullbreach-net.md#inventorysnapshot
        public void Write(ref ByteWriter w)
        {
            w.WriteU8((byte)MessageKind.InventorySnapshot);
            w.WriteU32(Tick);
            w.WriteU8((byte)Counts.Length);
            foreach (byte c in Counts) w.WriteU8(c);
            w.WriteI16(BudgetLeft); w.WriteU16(BlockCount); w.WriteU16(MaxBlocks); w.WriteU16(CooldownTicks);
        }

        // Reads the kind byte too; more than MaxTypes counts fail the reader.
        // frob:doc docs/reference/hullbreach-net.md#inventorysnapshot
        public static InventorySnapshot Read(ref ByteReader r)
        {
            r.ReadU8();
            uint tick = r.ReadU32();
            int n = r.ReadU8();
            if (n > MaxTypes) { r.Fail(); return default; }
            var counts = new byte[n];
            for (int i = 0; i < n; i++) counts[i] = r.ReadU8();
            short budget = r.ReadI16(); ushort blocks = r.ReadU16(), max = r.ReadU16(), cooldown = r.ReadU16();
            return new InventorySnapshot(tick, counts, budget, blocks, max, cooldown);
        }
    }

    // Server -> everyone, sequenced: a pickup appeared. Its position afterwards is origin + velocity * time,
    // so no further updates are needed.
    // frob:doc docs/reference/hullbreach-net.md#pickupspawned
    public readonly struct PickupSpawned
    {
        // frob:doc docs/reference/hullbreach-net.md#pickupspawned
        public const int ByteSize = 1 + 4 + 4 + 1 + 1 + 1 + 8 + 8 + 4 + 4;

        // frob:doc docs/reference/hullbreach-net.md#pickupspawned
        public readonly uint Sequence;
        // frob:doc docs/reference/hullbreach-net.md#pickupspawned
        public readonly Pickup Pickup;

        // frob:doc docs/reference/hullbreach-net.md#pickupspawned
        public PickupSpawned(uint sequence, Pickup pickup) { Sequence = sequence; Pickup = pickup; }

        // frob:doc docs/reference/hullbreach-net.md#pickupspawned
        public void Write(ref ByteWriter w)
        {
            w.WriteU8((byte)MessageKind.PickupSpawned);
            w.WriteU32(Sequence);
            w.WriteU32(Pickup.Id);
            w.WriteU8((byte)Pickup.Kind); w.WriteU8(Pickup.Type); w.WriteU8(Pickup.Count);
            w.WriteF32(Pickup.Origin.x); w.WriteF32(Pickup.Origin.y);
            w.WriteF32(Pickup.Velocity.x); w.WriteF32(Pickup.Velocity.y);
            w.WriteI32(Pickup.SpawnTick); w.WriteI32(Pickup.ExpireTick);
        }

        // Reads the kind byte too; a core/unknown block type, a zero count or non-finite numbers fail the reader.
        // frob:doc docs/reference/hullbreach-net.md#pickupspawned
        public static PickupSpawned Read(ref ByteReader r)
        {
            r.ReadU8();
            uint seq = r.ReadU32(), id = r.ReadU32();
            byte kind = r.ReadU8(), type = r.ReadU8(), count = r.ReadU8();
            var origin = new float2(r.ReadF32(), r.ReadF32());
            var vel = new float2(r.ReadF32(), r.ReadF32());
            int spawn = r.ReadI32(), expire = r.ReadI32();
            if (kind > (byte)PickupKind.Crate || type == BlockTypes.Core || type >= BlockTypes.Count || count == 0
                || !math.all(math.isfinite(origin)) || !math.all(math.isfinite(vel)) || expire < spawn) r.Fail();
            return new PickupSpawned(seq, new Pickup(id, (PickupKind)kind, type, count, origin, vel, spawn, expire));
        }
    }

    // Server -> everyone, sequenced: a pickup is gone (collected by Collector, or it left the field).
    // frob:doc docs/reference/hullbreach-net.md#pickupdespawned
    public readonly struct PickupDespawned
    {
        // frob:doc docs/reference/hullbreach-net.md#pickupdespawned
        public const int ByteSize = 1 + 4 + 4 + 1 + 2;

        // frob:doc docs/reference/hullbreach-net.md#pickupdespawned
        public readonly uint Sequence;
        // frob:doc docs/reference/hullbreach-net.md#pickupdespawned
        public readonly uint Id;
        // frob:doc docs/reference/hullbreach-net.md#pickupdespawned
        public readonly PickupRemoval Reason;
        // The collecting peer when Reason is Collected, else 0.
        // frob:doc docs/reference/hullbreach-net.md#pickupdespawned
        public readonly ushort Collector;

        // frob:doc docs/reference/hullbreach-net.md#pickupdespawned
        public PickupDespawned(uint sequence, uint id, PickupRemoval reason, ushort collector)
        { Sequence = sequence; Id = id; Reason = reason; Collector = collector; }

        // frob:doc docs/reference/hullbreach-net.md#pickupdespawned
        public void Write(ref ByteWriter w)
        {
            w.WriteU8((byte)MessageKind.PickupDespawned);
            w.WriteU32(Sequence); w.WriteU32(Id); w.WriteU8((byte)Reason); w.WriteU16(Collector);
        }

        // frob:doc docs/reference/hullbreach-net.md#pickupdespawned
        public static PickupDespawned Read(ref ByteReader r)
        {
            r.ReadU8();
            uint seq = r.ReadU32(), id = r.ReadU32();
            byte reason = r.ReadU8(); ushort collector = r.ReadU16();
            if (reason > (byte)PickupRemoval.Culled) r.Fail();
            return new PickupDespawned(seq, id, (PickupRemoval)reason, collector);
        }
    }
}
