# Netcode

Everything under `Assets/Scripts/Hullbreach.Net` is plain C#: no
`UnityEngine`, no specific transport package. It compiles and tests under
the stock .NET SDK (`tools/plaincs/run_tests.sh`) exactly like Ship, World
and Structure do, and for the same reason: a headless server process can
run `ServerSimulation` with no Unity install, and a transport implementer
can build and unit-test their half of the wire without touching a scene.

## The governing rule: send causes, not effects

Integer flood fill (`Hullbreach.Core.Topology.Connectivity`) is
bit-deterministic on every platform. The float FE solve
(`Hullbreach.Structure.StructuralSolver`) is not: SIMD width, FMA
contraction, and iteration counts all diverge across machines. So the
server never puts a block list on the wire when a ship splits. It sends
"block (x,y) died" (`BlockDestroyed`), and BOTH sides independently run
`Connectivity.FindDetached`/`SplitIntoComponents` over their own copy of the
grid to derive the identical set of detached fragments. A hit that splits a
10,000-block ship in half costs 9 bytes on the wire (`BlockDestroyed`), not
5,000 blocks. `FragmentSpawned` then announces the new body's pose with NO
block list at all, because both sides already agree on which blocks it
contains.

The FE result (`StructuralSolver.BlockStresses`, `BuckledBlocks`) never goes
on the wire. Clients run their own FE solve purely to drive the S37 stress
tint locally; divergence there is cosmetic (a slightly different color) and
invisible to gameplay. `StructuralSolver.BuckledBlocks` is explicitly
documented SERVER-AUTHORITATIVE for this reason: only `ServerSimulation`
acts on it to break a block and broadcast `BlockDestroyed`; a client must
never detach a block from its own buckling analysis, or it desyncs.

## Message table

Every message starts with a `MessageKind` byte (`NetMessages.cs`). Every
reliable message that participates in the ordered event stream also carries
a `u32 Sequence` right after the kind byte (see "Ordering" below for exactly
which ones and why `ShipSnapshot`/`BlockDamaged`/`PowerupApplied` carry one
too, even though the original design note only called it out for
`BlockPlaced`/`BlockDestroyed`/`FragmentSpawned`).

| Kind | Channel | Layout (after the kind byte) | Size |
|---|---|---|---|
| `InputMessage` | client->server, unreliable | `u16 netId, u32 tick, i8 thrustAxis, i8 steer, u8 flags` | 9 B |
| `ShipSnapshot` | server->client, reliable | `u32 seq, u16 netId, u16 count, {i8 x, i8 y, u8 typeId, u8 mods, u8 damage}[count], i16 px, i16 py, u16 rot, i16 vx, i16 vy, u16 av` | 17 + 5*count B |
| `ShipState` | server->client, unreliable, ~50 Hz | `u16 netId, i16 px, i16 py, u16 rot, i16 vx, i16 vy, u16 av` | 15 B |
| `BlockPlaced` | server->client, reliable ordered | `u32 seq, u16 netId, i8 x, i8 y, u8 typeId, u8 mods` | 11 B |
| `BlockDestroyed` | server->client, reliable ordered | `u32 seq, u16 netId, i8 x, i8 y` | 9 B |
| `FragmentSpawned` | server->client, reliable ordered | `u32 seq, u16 parentId, u16 newId, i16 px, i16 py, u16 rot, i16 vx, i16 vy, u16 av` | 21 B |
| `BlockDamaged` | server->client, reliable | `u32 seq, u16 netId, i8 x, i8 y, u8 damage` | 10 B |
| `PowerupApplied` | server->client, reliable | `u32 seq, u16 netId, i8 x, i8 y, u8 variant, u16 seconds10` | 12 B |
| `GravityWellSpawned` | server->client, reliable | `i16 px, i16 py, i16 mu, u8 radius, u16 seconds10` | 10 B |

All multi-byte fields are little-endian (`ByteWriter`/`ByteReader` in
`Wire.cs`). Position/velocity are quantized at 1/256 world unit
(`Quantization.PackPosition`); angles as a 16-bit turn fraction
(`Quantization.PackAngle`), which wraps for free on any input, including
many-turn or negative radian values. `GravityWellSpawned.Mu` is a signed
fixed-point value at 1/16 per unit (`GravityWellSpawned.MuScale`), wide
enough to carry the anti-gravity gun's negative `Mu`. `seconds10` fields are
tenths of a second in a `u16`, so a multi-minute buff still fits without a
float on the wire.

Two ships' `ShipState` traffic is 2 * 15 = 30 bytes/tick of unreliable
traffic at the default 50 Hz tick rate (see
`ServerClientEndToEndTests.Poses_Events_Ordering_Timeout_And_Bandwidth`,
which asserts this stays under 200 B/tick).

## Ordering: what the transport must (and must not) guarantee

`ITransport.SendReliable` guarantees **exactly-once delivery**, never drop,
never duplicate. It does **not** guarantee order relative to other
`SendReliable` calls to the same peer. That is deliberate: a real transport
(Unity Transport, Netcode for GameObjects) is free to fan reliable traffic
across internal channels or priorities that reorder relative to each other,
and building an ordering guarantee on top of that in the transport layer is
unnecessary work the application layer already does better.

Ordering instead lives in the `u32 Sequence` on `ShipSnapshot`,
`BlockPlaced`, `BlockDestroyed`, `FragmentSpawned`, `BlockDamaged` and
`PowerupApplied`: `ServerSimulation` hands out one strictly increasing
sequence number across ALL of these (not per-message-kind, one global
counter), and `ClientReplica.ApplyReliable` buffers by sequence, applying a
message only when it is exactly `lastApplied + 1` and draining whatever that
unblocks. `ClientReplica.ApplySnapshot`'s own sequence becomes the new
baseline (a snapshot already reflects every event up to its own sequence,
so anything buffered at or below it is stale and is discarded, not
reapplied).

`ShipState` and `GravityWellSpawned` are the two exceptions:

- `ShipState` is unreliable and carries no sequence at all: a dropped,
  duplicated, or reordered pose update is harmless by design (the next one
  supersedes it), so `ClientReplica.ApplyState` just overwrites the latest
  pose sample unconditionally.
- `GravityWellSpawned` carries no sequence either (see the literal layout
  above): a well's effect on gravity does not depend on destruction
  ordering the way `FragmentSpawned` does, so it is applied to the client's
  `GravityField`-equivalent state immediately on arrival.

`LoopbackTransport` (the in-memory transport used by tests and local play)
exercises this on purpose: its `JitterTicks` knob lets two reliable messages
sent in order arrive out of order, and
`LoopbackTransportTests.Jitter_CanReorderTwoReliableMessages` proves it can.
`ServerClientEndToEndTests` runs the whole pipeline against a jittery hub
and still ends up with matching client/server state, because the sequence
buffer (not arrival order) is what the client trusts.

## The tick loop

`ServerSimulation.Tick()`, called once per fixed tick (default 50 Hz,
`TickRate`):

1. Time out peers that have gone `TimeoutSeconds` (default 5 s) without a
   fresh `SetInput` call, removing their ship (S47 criterion 2).
2. Tick the shared `GravityField` (expires temporary wells) and step the
   server's own point-body projectile simulation (gravity + block hit
   tests), which is this class's own minimal `IWorldSink`.
3. For each connected peer, in ascending peer-id order (deterministic):
   apply its latest buffered input (or a neutral input if none arrived this
   tick), `ShipBody.Step`, drain `PendingShots` into the projectile sim,
   then `StructuralSolver.Tick` and resolve any resulting damage/detach/
   buckling exactly like `Hullbreach.Game.ShipStructure` does today, except
   every destroy/damage/fragment event is broadcast as it happens.
4. Emit one `ShipState` (unreliable) per surviving ship.

Every reliable event goes through `IServerOutbox`
(`ServerOutbox`/`OutboxEntry`): `ServerSimulation` never touches
`ITransport` directly. Whatever owns the real transport (a MonoBehaviour's
`FixedUpdate`, a headless loop) drains `ServerOutbox.Entries` once per tick,
forwards each to `transport.SendReliable`/`SendUnreliable`, and calls
`Clear()`. This is what lets `ServerSimulation` be tested with nothing but a
`ServerOutbox` and no transport at all (see `ServerClientEndToEndTests` and
every `ServerSimulation`-touching test in `MessageRoundTripTests`'
neighbors).

`Join(peer, ShipSnapshot)` sends every already-connected ship's snapshot to
the newcomer, then broadcasts the newcomer's own ship's snapshot to
**every** connected peer, including the newcomer itself: a player needs a
snapshot of their own ship exactly like everyone else does, since the
server is the only place the authoritative block layout lives.

## What a transport implementer must do

Implement `ITransport` (`SendReliable`, `SendUnreliable`, `TryReceive`,
`PeerConnected`, `PeerDisconnected`) against Unity Transport, Netcode for
GameObjects, or a raw socket:

- `SendReliable`/`SendUnreliable` copy `payload` before returning (the
  caller reuses its buffer immediately after the call).
- `TryReceive` never blocks; it returns `false` when nothing is queued.
  Reliable and unreliable payloads share one receive queue: the
  `MessageKind` byte at offset 0 is how a caller distinguishes them, not
  which channel they arrived on.
- Fire `PeerConnected`/`PeerDisconnected` exactly once per connect/
  disconnect.
- Do **not** try to guarantee order on the reliable channel beyond
  exactly-once delivery; see "Ordering" above. A transport that already
  happens to preserve order (many do) is not wrong to use here, it is just
  not required, and `ClientReplica` does not rely on it.

Everything above `ITransport` (`ServerSimulation`, `ClientReplica`, every
message struct) is done: a transport implementer's entire job is one class
satisfying that interface, then pumping `ServerSimulation.Tick`/
`IServerOutbox.Entries` on one side and `ClientReplica.ApplyReceived` on the
other. `LoopbackTransport` is a complete, if intentionally lossy and
jittery, reference implementation to copy the shape of.

## What is deliberately NOT sent, and why

- **FE results** (`StructuralSolver.BlockStresses`, in particular
  `BlockStress.VonMises`/`DuctileRatio`/`BrittleRatio`/`BucklingRatio`).
  Not bit-identical across machines; sending them would either desync (if
  trusted) or be pure waste (if only used for a cosmetic tint, which every
  client can compute for itself from data it already has).
- **`StructuralSolver.BuckledBlocks`**. Same reason, doubly so: this is the
  list a client must NEVER act on locally (see its own doc comment). Only
  the server reads it, and only to decide to broadcast `BlockDestroyed`.
- **Detached block lists**. `FragmentSpawned` carries a pose and two IDs,
  nothing else. Both sides derive the block membership from
  `Connectivity.FindDetached` after applying the same ordered
  `BlockDestroyed` events, so listing the blocks would be redundant data
  that also does not survive a 10,000-block ship splitting in half.
- **Full grid diffs for damage**. `BlockDamaged` sends exactly one block's
  new damage byte, because unlike destruction, damage accumulation is a
  genuine cause the client cannot derive (the FE solve that computed the
  overshoot is not deterministic across machines).
