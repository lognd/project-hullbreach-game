# Hullbreach.Net reference

Per-type reference for `Assets/Scripts/Hullbreach.Net`, linked from the code by
`// frob:doc docs/reference/hullbreach-net.md#<anchor>`. One heading per
public type; each heading carries the `frob:describes` lines for that
type and its public members. Architecture-level context lives in
[architecture.md](../architecture.md).

### ByteWriter

<!-- frob:describes Assets/Scripts/Hullbreach.Net/Wire.cs::ByteWriter -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Wire.cs::ByteWriter.ByteWriter -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Wire.cs::ByteWriter.Position -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Wire.cs::ByteWriter.WriteU8 -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Wire.cs::ByteWriter.WriteI8 -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Wire.cs::ByteWriter.WriteU16 -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Wire.cs::ByteWriter.WriteI16 -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Wire.cs::ByteWriter.WriteU32 -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Wire.cs::ByteWriter.WriteI32 -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Wire.cs::ByteWriter.WriteF32 -->

Little-endian cursor over a caller-owned byte buffer. No allocation per
message: every `Write` call takes a `ByteWriter` wrapping a buffer the
caller already owns (a pooled send buffer, a stackalloc span, etc).
Throws `IndexOutOfRangeException` on overflow rather than growing, since
growing would mean allocating, which is exactly what this type exists to
avoid.

### ByteReader

<!-- frob:describes Assets/Scripts/Hullbreach.Net/Wire.cs::ByteReader -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Wire.cs::ByteReader.ByteReader -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Wire.cs::ByteReader.Position -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Wire.cs::ByteReader.Remaining -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Wire.cs::ByteReader.Failed -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Wire.cs::ByteReader.Fail -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Wire.cs::ByteReader.ReadU8 -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Wire.cs::ByteReader.ReadI8 -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Wire.cs::ByteReader.ReadU16 -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Wire.cs::ByteReader.ReadI16 -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Wire.cs::ByteReader.ReadU32 -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Wire.cs::ByteReader.ReadI32 -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Wire.cs::ByteReader.ReadF32 -->

The inverse of `ByteWriter`, bounded by the received length (INV-001):
construct it with the transport's `length` so stale buffer bytes are never
readable. Reading past the end never throws or reads stale data; it sets
`Failed` and returns zero from then on, so a decoder reads the whole
message and checks `Failed` once. `Fail()` lets a decoder mark a message
malformed for other reasons (an implausible count); `Remaining` is the
byte budget a count must fit in before anything is allocated.

### Quantization

<!-- frob:describes Assets/Scripts/Hullbreach.Net/Quantization.cs::Quantization -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Quantization.cs::Quantization.PositionScale -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Quantization.cs::Quantization.PositionLimit -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Quantization.cs::Quantization.PackPosition -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Quantization.cs::Quantization.UnpackPosition -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Quantization.cs::Quantization.PackAngle -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/Quantization.cs::Quantization.UnpackAngle -->

Fixed-point packing for the per-tick state. See docs/netcode.md's message
table for the quantization scales used on the wire.

`PackPosition` rounds to nearest and saturates rather than truncating: this
makes the round trip idempotent (`Unpack(Pack(x))` lands exactly on a
lattice point, and packing that again returns the same `short`), so
repeated quantization never crawls. `PositionLimit` matters because the
arena (S41) must fit inside `+-PositionLimit` or the packing saturates and
a ship near the edge stops moving on the wire.

`PackAngle` encodes a full turn in 16 bits; the cast to `ushort` wraps,
which is exactly modular arithmetic on the circle, so any radian value
(negative, many turns) packs without normalization.

### ITransport

<!-- frob:describes Assets/Scripts/Hullbreach.Net/ITransport.cs::ITransport -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ITransport.cs::ITransport.SendReliable -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ITransport.cs::ITransport.SendUnreliable -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ITransport.cs::ITransport.TryReceive -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ITransport.cs::ITransport.PeerConnected -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ITransport.cs::ITransport.PeerDisconnected -->

The one interface a real transport (Unity Transport, Netcode for
GameObjects, a raw socket) must implement to plug into
`ServerSimulation`/`ClientReplica`. Everything above this line (message
structs, `ServerSimulation`, `ClientReplica`) never touches sockets,
threads or a specific networking package: it only calls this.

Contract the implementer must satisfy:

- `SendReliable`: delivered EXACTLY ONCE (no drops, no duplicates) to the
  named peer. Relative ORDER between two `SendReliable` calls is NOT
  required at this layer: a real transport may fan messages across
  internal channels/priorities that reorder relative to each other.
  Ordering for the messages that need it is instead carried by the u32
  sequence number embedded in the message itself; see
  docs/netcode.md#ordering-what-the-transport-must-and-must-not-guarantee.
- `SendUnreliable`: best-effort. May be dropped, duplicated, or arrive out
  of order; callers (`ShipState`) are designed to tolerate all three.
- `TryReceive`: never blocks. Returns false when nothing is queued.
  Reliable and unreliable payloads share the same receive queue; the
  `MessageKind` byte at offset 0 is how the caller tells them apart, not
  the channel they arrived on.
- `PeerConnected`/`PeerDisconnected`: fired exactly once per connect and
  per disconnect, never for the same peer twice in a row without the
  other event between.

### IServerOutbox

<!-- frob:describes Assets/Scripts/Hullbreach.Net/IServerOutbox.cs::IServerOutbox -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/IServerOutbox.cs::IServerOutbox.Send -->

Everything `ServerSimulation` emits in one `Tick`, before it touches an
`ITransport`. Keeping this as a plain list of (peer, reliable, bytes)
tuples is what lets the transport stay a separate concern: a test can
inspect the outbox directly with no transport at all, and the
transport-pumping code (a `MonoBehaviour`, a headless loop) is the only
thing that ever needs to know both `ServerSimulation` and `ITransport`
exist.

### OutboxEntry

<!-- frob:describes Assets/Scripts/Hullbreach.Net/IServerOutbox.cs::OutboxEntry -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/IServerOutbox.cs::OutboxEntry.Peer -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/IServerOutbox.cs::OutboxEntry.Reliable -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/IServerOutbox.cs::OutboxEntry.Bytes -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/IServerOutbox.cs::OutboxEntry.OutboxEntry -->

One queued send: which peer, which channel, and the exact bytes to
deliver.

### ServerOutbox

<!-- frob:describes Assets/Scripts/Hullbreach.Net/IServerOutbox.cs::ServerOutbox -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/IServerOutbox.cs::ServerOutbox.Entries -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/IServerOutbox.cs::ServerOutbox.Send -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/IServerOutbox.cs::ServerOutbox.Clear -->

The default `IServerOutbox`: a plain growable list a caller drains once per
tick and clears. `ServerSimulation.Tick` fills this; whatever pumps the
real `ITransport` (a `MonoBehaviour`'s `FixedUpdate`, a headless server's
loop) reads `Entries` and forwards each one to
`transport.SendReliable`/`SendUnreliable`, then calls `Clear`.

### LoopbackTransport

<!-- frob:describes Assets/Scripts/Hullbreach.Net/LoopbackTransport.cs::LoopbackTransport -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/LoopbackTransport.cs::LoopbackTransport.DelayTicks -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/LoopbackTransport.cs::LoopbackTransport.JitterTicks -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/LoopbackTransport.cs::LoopbackTransport.UnreliableDropRate -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/LoopbackTransport.cs::LoopbackTransport.LoopbackTransport -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/LoopbackTransport.cs::LoopbackTransport.CreateEndpoint -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/LoopbackTransport.cs::LoopbackTransport.Connect -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/LoopbackTransport.cs::LoopbackTransport.Disconnect -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/LoopbackTransport.cs::LoopbackTransport.Tick -->

In-memory transport hub for tests and local (same-process) play: no
sockets, no threads. `CreateEndpoint` hands back one `ITransport` per
logical participant (the server, each client); every endpoint can address
every other by the peer id `CreateEndpoint` returned for it.

Simulates a lossy, jittery network on purpose (see `UnreliableDropRate`/
`DelayTicks`/`JitterTicks`): unreliable sends may be dropped, and BOTH
channels may be delivered out of send order, exactly like a real
transport's internal channels can. Nothing here ever drops or duplicates a
reliable message; it only reorders and delays it, which is why
`ClientReplica` buffers reliable events by sequence number rather than
trusting arrival order.

Advance time by calling `Tick` once per simulation tick; nothing is
delivered until enough ticks have passed.

### NetMessages

<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::NetMessages -->

Wire formats. The governing rule (send CAUSES, never EFFECTS) and the full
message table live in [docs/netcode.md](../netcode.md); this file only
holds the struct layouts themselves.

### MessageKind

<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::MessageKind -->

One byte identifying which message struct follows in a buffer. Kept as its
own byte (not folded into a discriminated union) so a receiver can
dispatch with a single switch before deserializing anything.

### InputMessage

<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::InputMessage -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::InputMessage.NetId -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::InputMessage.Tick -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::InputMessage.ThrustAxis -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::InputMessage.Steer -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::InputMessage.Flags -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::InputMessage.FireBit -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::InputMessage.InputMessage -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::InputMessage.FromFloats -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::InputMessage.ThrustAxisFloat -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::InputMessage.SteerFloat -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::InputMessage.FirePressed -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::InputMessage.Write -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::InputMessage.Read -->

Client -> server, unreliable, one per tick: this tick's player intent.
Latest-wins on the server (`ServerSimulation` keeps only the newest input
per peer), so dropping one is harmless. See docs/netcode.md#message-table
for the byte layout.

### SnapshotBlock

<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::SnapshotBlock -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::SnapshotBlock.X -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::SnapshotBlock.Y -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::SnapshotBlock.TypeId -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::SnapshotBlock.Mods -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::SnapshotBlock.Damage -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::SnapshotBlock.SnapshotBlock -->

One block as carried by `ShipSnapshot`: position (grid-local, fits an
sbyte per `BlockKey`'s -128..127 range), type, modifiers and accumulated
damage.

### ShipSnapshot

<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipSnapshot -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipSnapshot.Sequence -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipSnapshot.NetId -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipSnapshot.Blocks -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipSnapshot.Px -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipSnapshot.Py -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipSnapshot.Rot -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipSnapshot.Vx -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipSnapshot.Vy -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipSnapshot.Av -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipSnapshot.ShipSnapshot -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipSnapshot.MaxBlocks -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipSnapshot.ByteSize -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipSnapshot.Write -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipSnapshot.Read -->

Server -> client, RELIABLE, sent once on join or respawn: the full block
layout plus the ship's current pose/velocity. 5 bytes per block raw; block
grids deflate ~10:1 under a reliable transport's own compression, so a
10k-block ship is a few KB. Fine as a one-off; never sent per tick. See
docs/netcode.md#message-table for the byte layout.

`MaxBlocks` (4096) caps a snapshot: `Read` fails the reader (and returns
no blocks) when the wire count exceeds it or does not fit in the bytes
actually received, so a remote count never sizes an allocation, and
`Write` throws rather than letting the u16 count wrap.

### ShipRemoved

<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipRemoved -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipRemoved.Sequence -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipRemoved.NetId -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipRemoved.ShipRemoved -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipRemoved.Write -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipRemoved.Read -->

Server -> client, reliable ordered: the ship left (an explicit `Leave` or
an input timeout). Without it every replica kept a ghost ship forever.

### ShipState

<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipState -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipState.NetId -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipState.Px -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipState.Py -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipState.Rot -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipState.Vx -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipState.Vy -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipState.Av -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipState.ByteSize -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipState.ShipState -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipState.Write -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::ShipState.Read -->

Server -> client, UNRELIABLE, ~30-50 Hz: this tick's pose. Losing one is
harmless since the next one supersedes it. See docs/netcode.md#message-table
for the byte layout and quantization.

### BlockPlaced

<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockPlaced -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockPlaced.Sequence -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockPlaced.NetId -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockPlaced.X -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockPlaced.Y -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockPlaced.TypeId -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockPlaced.Mods -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockPlaced.BlockPlaced -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockPlaced.Write -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockPlaced.Read -->

Server -> client, RELIABLE ORDERED: a block was placed. Carries a
sequence number because placement order matters for which cell wins a
race, exactly like destruction order matters for `Connectivity.FindDetached`.

### BlockDestroyed

<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockDestroyed -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockDestroyed.Sequence -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockDestroyed.NetId -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockDestroyed.X -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockDestroyed.Y -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockDestroyed.BlockDestroyed -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockDestroyed.Write -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockDestroyed.Read -->

Server -> client, RELIABLE ORDERED: a block died. This is the whole point
of the design: both sides run `Connectivity.FindDetached`/
`SplitIntoComponents` after applying this, and derive the identical set of
fragments without a block list ever crossing the wire. See
docs/netcode.md#the-governing-rule-send-causes-not-effects.

### FragmentSpawned

<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::FragmentSpawned -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::FragmentSpawned.Sequence -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::FragmentSpawned.ParentId -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::FragmentSpawned.NewId -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::FragmentSpawned.Px -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::FragmentSpawned.Py -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::FragmentSpawned.Rot -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::FragmentSpawned.Vx -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::FragmentSpawned.Vy -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::FragmentSpawned.Av -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::FragmentSpawned.FragmentSpawned -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::FragmentSpawned.Write -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::FragmentSpawned.Read -->

Server -> client, RELIABLE ORDERED: a detached component (from a
`FindDetached` split, on the tick's `BlockDestroyed`/buckling events) is
spawned as its own body. Carries NO block list: both sides already know
which blocks left, because they ran the same flood fill after applying
the same ordered destruction events first.

### BlockDamaged

<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockDamaged -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockDamaged.Sequence -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockDamaged.NetId -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockDamaged.X -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockDamaged.Y -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockDamaged.Damage -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockDamaged.BlockDamaged -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockDamaged.Write -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::BlockDamaged.Read -->

Server -> client, RELIABLE ORDERED: a block took damage but did not die.
Damage is a CAUSE the client cannot derive on its own (the FE solve that
computed it is not bit-identical across machines), so unlike destruction
it must be sent explicitly rather than recomputed locally.

### PowerupApplied

<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::PowerupApplied -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::PowerupApplied.Sequence -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::PowerupApplied.NetId -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::PowerupApplied.X -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::PowerupApplied.Y -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::PowerupApplied.Variant -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::PowerupApplied.Seconds10 -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::PowerupApplied.PowerupApplied -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::PowerupApplied.Seconds -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::PowerupApplied.Write -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::PowerupApplied.Read -->

Server -> client, RELIABLE ORDERED: a temporary variant transform
(`ShipBody.ApplyPowerup`) landed on a block. `Seconds10` is tenths of a
second in a u16 (`seconds*10`) so a multi-minute buff still fits without a
float on the wire.

### GravityWellSpawned

<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::GravityWellSpawned -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::GravityWellSpawned.Px -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::GravityWellSpawned.Py -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::GravityWellSpawned.Mu -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::GravityWellSpawned.Radius -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::GravityWellSpawned.Seconds10 -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::GravityWellSpawned.MuScale -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::GravityWellSpawned.GravityWellSpawned -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::GravityWellSpawned.FromFloats -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::GravityWellSpawned.MuFloat -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::GravityWellSpawned.RadiusFloat -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::GravityWellSpawned.SecondsFloat -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::GravityWellSpawned.PxFloat -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::GravityWellSpawned.PyFloat -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::GravityWellSpawned.Write -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/NetMessages.cs::GravityWellSpawned.Read -->

Server -> client, RELIABLE ORDERED: a temporary gravity well/anti-well
(`IWorldSink.AddTemporaryGravity`) was dropped into the world field. `Mu`
is quantized as a signed 16-bit fixed point at 1/16 per unit via
`MuScale`, wide enough to carry the anti-gravity gun's negative `Mu`, and
`Radius` is a single unsigned byte since well radii never need sub-unit
precision.

### ReplicaEventKind

<!-- frob:describes Assets/Scripts/Hullbreach.Net/ClientReplica.cs::ReplicaEventKind -->

Effect-worthy events a renderer/audio layer wants to react to, drained via
`ClientReplica.TryDequeueEvent` once applied.

### ReplicaEvent

<!-- frob:describes Assets/Scripts/Hullbreach.Net/ClientReplica.cs::ReplicaEvent -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ClientReplica.cs::ReplicaEvent.Kind -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ClientReplica.cs::ReplicaEvent.NetId -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ClientReplica.cs::ReplicaEvent.X -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ClientReplica.cs::ReplicaEvent.Y -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ClientReplica.cs::ReplicaEvent.WorldPosition -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ClientReplica.cs::ReplicaEvent.ReplicaEvent -->

One applied event, boxed just enough for a renderer to know what happened
and to which ship, without re-parsing wire bytes.

### ClientReplica

<!-- frob:describes Assets/Scripts/Hullbreach.Net/ClientReplica.cs::ClientReplica -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ClientReplica.cs::ClientReplica.Ships -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ClientReplica.cs::ClientReplica.ApplySnapshot -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ClientReplica.cs::ClientReplica.ApplyState -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ClientReplica.cs::ClientReplica.TryInterpolate -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ClientReplica.cs::ClientReplica.ApplyReceived -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ClientReplica.cs::ClientReplica.ApplyReliable -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ClientReplica.cs::ClientReplica.MaxReliableWindow -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ClientReplica.cs::ClientReplica.TryDequeueEvent -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ClientReplica.cs::ClientReplica.BuildInput -->

The non-authoritative twin of `ServerSimulation`: applies `ShipSnapshot`/
`ShipState`/reliable events received over an `ITransport` (or fed directly
in a test) to build and keep replica `ShipBody` instances in sync with the
server, WITHOUT running a `StructuralSolver` (clients never decide a block
breaks; see `StructuralSolver.BuckledBlocks`).

Reliable events are buffered by sequence number and applied only when the
run is contiguous from the last applied sequence (see `ApplyReliable`).
This is what makes `ClientReplica` correct even against a transport that
reorders reliable messages (see `LoopbackTransport`'s jitter and
`ITransport`'s ordering contract, docs/netcode.md#ordering...).

Baselines are per ship. `ApplySnapshot` installs a ship only if no newer
snapshot or removal already covers its `NetId` (it returns false
otherwise), and records the snapshot's sequence as that ship's baseline:
later-arriving events for that ship at or below it are skipped (already
folded in), other ships are untouched. The first snapshot a replica ever
sees also sets the stream cursor; after that a snapshot reserves its own
slot in the ordered stream so the cursor can pass it. An event for a ship
whose snapshot has not arrived yet is held (bounded) and replayed when it
does. `ShipRemoved` drops the ship, raises `ReplicaEventKind.ShipRemoved`
and keeps the baseline so a late duplicate cannot resurrect it.

Wire data never throws. `ApplyReceived` and `ApplyReliable` return a
`ReplicaApplyResult`: `Applied`, `Buffered` (waiting for a gap),
`Stale` (duplicate or covered), `OutOfWindow` (more than
`MaxReliableWindow` = 512 past the last applied sequence, INV-002;
buffered and deferred counts are capped by the same constant) or
`Malformed` (unknown kind, truncated header). Only the first `length`
bytes are read. A reliable message with a readable header but a short or
undecodable body still consumes its sequence slot, so one bad message
cannot stall the stream. Sequence comparison is wrap-safe.

### ReplicaApplyResult

<!-- frob:describes Assets/Scripts/Hullbreach.Net/ClientReplica.cs::ReplicaApplyResult -->

What the replica did with one payload: `Applied`, `Buffered`, `Stale`,
`OutOfWindow` or `Malformed`; see `ClientReplica`.

`ApplyFragmentSpawned` (private) only spawns the replica body the
already-derived detached blocks belong in, at the pose the server reports;
it creates an empty-grid placeholder for renderer bookkeeping rather than
carrying the detached block set over from `RunLocalDetach`'s own
`Connectivity.SplitIntoComponents` call, kept minimal since the wire
contract is what deliverable 6 tests.

`RunLocalDetach` (private) locally derives the same detached-component
split the server derived, removing every stranded block from the replica
grid: this is the entire reason `FragmentSpawned` never needs a block list
on the wire, since both sides ran the identical integer flood fill after
applying the identical ordered `BlockDestroyed` events.

`ApplyState` is unreliable and unordered by design: an out-of-order or
duplicate `ShipState` simply becomes the new "newer" interpolation sample
regardless of tick, since a missed/duplicated pose update is harmless.
`ApplyReceived` is the convenience entry point a transport pump should call
for every payload received: it dispatches `ShipSnapshot`/`ShipState`
straight through, applies `GravityWellSpawned` immediately (it carries no
sequence number), and routes every other reliable event through
`ApplyReliable`, which applies a message immediately if it is exactly the
next expected sequence and then drains whatever the gap closing unblocks.

### ServerSimulation

<!-- frob:describes Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::ServerSimulation -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::ServerSimulation.TickRate -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::ServerSimulation.TimeoutSeconds -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::ServerSimulation.Gravity -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::ServerSimulation.ServerSimulation -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::ServerSimulation.Ships -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::ServerSimulation.MaxPeerId -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::ServerSimulation.FirstFragmentId -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::ServerSimulation.Join -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::ServerSimulation.Leave -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::ServerSimulation.TryDequeueDroppedPeer -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::ServerSimulation.SetInput -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::ServerSimulation.Tick -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::ServerSimulation.DebugDestroyBlock -->

The authoritative, plain-C# server loop: one `ShipBody` + `StructuralSolver`
per connected peer, a shared `GravityField`, and a minimal point-body
projectile simulation (its own `IWorldSink`), all driven at a fixed tick
rate. Never touches `ITransport` directly: every `Tick` call fills the
`IServerOutbox` handed to the constructor, and whatever owns the real
transport (a `MonoBehaviour`, a headless loop) drains that outbox and
forwards it. That separation is deliverable 4's whole point: this class,
and therefore the entire server simulation, compiles and tests without
knowing sockets exist.

`Join` re-broadcasts the new ship's snapshot (reliable) to every OTHER
connected peer so they can build a replica for it; the joining peer
already has its own design locally and does not need it echoed back. It
catches the newcomer up on every ship that already exists BEFORE adding
theirs to the peer table, so the later loop that broadcasts the
newcomer's own snapshot to everyone else never doubles back and resends
an existing ship to itself; that later loop includes the joiner itself,
since it needs a snapshot of its own ship exactly like everyone else
does. The snapshots sent to the joiner for existing ships are stamped
with the current sequence rather than a fresh one (only the joiner gets
them, so a fresh number would leave a gap in everyone else's stream).

`Join` returns a `JoinResult` and treats the design as untrusted (INV-003):
`InvalidPeerId` unless `1 <= peer <= MaxPeerId` (the peer id is the ship's
`NetId`), `AlreadyJoined` if the peer has a ship (a join never replaces
one; call `Leave` first), `InvalidDesign` unless
`DesignValidator.ValidateDesign` passes. Damage in the design is ignored
(a new ship is intact) and the claimed velocity is clamped to
`DesignValidator.MaxSpawnSpeed`; position and angle are bounded by the
wire quantization. Fragment `NetId`s cycle through
`FirstFragmentId..65535`, disjoint from peer ids, so a fragment can never
overwrite a peer's replica. Each peer's `StructuralSolver` gets the shared
calibrated `StructuralSolver.DefaultLoadScale` /
`DefaultMaterialStiffnessScale`, the same values `ShipStructure` uses.

`Leave` and the input timeout both broadcast `ShipRemoved` to the remaining
peers and orphan the leaver's in-flight projectiles; timed-out peers are
queued for `TryDequeueDroppedPeer` so the host can close their connection.

`SetInput` keeps the latest axes (clamped to -1..1; -128 decodes to -1) but
latches a fire press until the next `Tick` consumes it, so a release that
arrives before the tick cannot erase the press edge, and ignores a message
whose tick is older than the last accepted one. Peer identity is always the
`peer` argument, never `InputMessage.NetId`. Projectile hits test a circle
of `ProjectileSpec.Radius` against the block cells (`ProjectileHitTest`)
and skip the owner's ship.

### JoinResult

<!-- frob:describes Assets/Scripts/Hullbreach.Net/ServerSimulation.cs::JoinResult -->

Outcome of `ServerSimulation.Join`: `Joined`, `InvalidPeerId`,
`AlreadyJoined`, `InvalidDesign`. A non-`Joined` result changes nothing.

### DesignValidator

<!-- frob:describes Assets/Scripts/Hullbreach.Net/DesignValidator.cs::DesignVerdict -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/DesignValidator.cs::DesignValidator -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/DesignValidator.cs::DesignValidator.MaxSpawnSpeed -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/DesignValidator.cs::DesignValidator.ValidateDesign -->

Server-side check of a client-supplied `ShipSnapshot` before a ship is
built from it: at least one and at most `ShipSnapshot.MaxBlocks` blocks,
known block types, no duplicate cells, exactly one core, and every block
connected to the core. Returns a `DesignVerdict` naming the first rule
broken.

### ProjectileHitTest

<!-- frob:describes Assets/Scripts/Hullbreach.Net/ProjectileHitTest.cs::ProjectileHitTest -->
<!-- frob:describes Assets/Scripts/Hullbreach.Net/ProjectileHitTest.cs::ProjectileHitTest.TryHitShipCell -->

Engine-free circle-versus-block-cell test the server uses for projectile
impacts: finds the occupied cell nearest the circle centre that the circle
overlaps, in the ship's local frame. A zero radius is a point test.

`Tick` advances every ship by one fixed tick (apply latest input, step
the body, tick its structural solver, resolve damage/detachment/
buckling, then emit poses and events) in a fixed, deterministic order
(peer id, then event kind) so sequence numbers are reproducible given
the same inputs. `DebugDestroyBlock` is a test/debug hook that goes
through the exact same broadcast + detach-resolution path a real
projectile impact would, letting a test force a split without simulating
ballistics.

`ResolveDetachAfterDestruction` (private) runs
`Connectivity.FindDetached`/`SplitIntoComponents` once for the whole batch
(never per block), removes every stranded block from the authoritative
grid without individually announcing them (clients derive the same set
from the `BlockDestroyed` events already broadcast), and broadcasts one
`FragmentSpawned` per resulting component so both sides spawn matching
debris bodies. `BroadcastReliable` (private) sends to every connected
peer, not just a ship's own owner, since everyone needs to know every
ship's destruction/damage/fragment events to keep their replicas in
sync.

`ServerWorldSink` (private nested) is the server's own `IWorldSink`:
spawns cannon shots as simple point bodies (no rigid body, no scene
object) and routes gravity-well drops into the shared field while
broadcasting the cause. `BlockGridExtensions.CoreKeyOrDefault` (a small
file-private helper) returns an out-of-range sentinel when the grid has
no core key, so a comparison against a candidate key never needs a
separate `HasValue` branch.

### NetDemo

<!-- frob:describes Assets/Scripts/Hullbreach.Game/NetDemo.cs::NetDemo -->

Runs a whole `ServerSimulation` + two `ClientReplica` instances in-process
over a jittery `LoopbackTransport`, purely so a handoff engineer can see
the netcode pipeline moving in the Editor without standing up a real
transport. Deliberately tiny: hardcodes two ship designs, drives one with
WASD-style axes and fires the other's cannon on a timer, and renders each
replica as one colored quad per block using runtime-generated
`GameObject`s (no prefabs, independent of `ShipRenderer` since
`ShipRenderer` requires a `ShipController`/`Rigidbody2D` this demo's
replica ships deliberately do not have). Does not touch `DemoScene`; drop
this on an empty `GameObject` in any scene to see it run.

`RefreshVisuals` (private) creates a quad per block lazily, keyed by
(replica, netId, block) so the two clients do not share quads, moves it with
its ship, and destroys any quad whose block or ship the replica no longer
has; `OnDestroy` removes the rest.
