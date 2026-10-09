+++
id = "01M4GR2JNYB77501DAJFF4Q694"
title = "Wire decode has no length/kind validation: ShipSnapshot.Read trusts count, readers ignore payload length"
type = "security"
category = "todo"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:31:23Z"
updated = "2026-10-09T16:31:23Z"
labels = ["origin:auditor", "audit:hullbreach-net"]
scope = ["Assets/Scripts/Hullbreach.Net/Wire.cs", "Assets/Scripts/Hullbreach.Net/NetMessages.cs"]
+++

ByteReader (Wire.cs:58-106) is bound to the whole buffer, not the received length, and Read* throws raw IndexOutOfRange on overrun; callers pass pooled buffers (Hullbreach.Game/NetDemo.cs:96,126) so a truncated datagram silently decodes stale bytes from the previous message instead of failing. ShipSnapshot.Read (NetMessages.cs:219-220) allocates new SnapshotBlock[count] from an attacker/peer controlled u16 before checking remaining bytes. InputMessage.Read/ShipSnapshot.Read etc. discard the kind byte (NetMessages.cs:98,216) without verifying it. Separately ShipSnapshot.Write (NetMessages.cs:195) casts Blocks.Length to ushort: a full 256x256 grid (65536 blocks, legal per BlockKey) writes count 0 then 65536 records, corrupting the stream. Contract violated: decoders of untrusted bytes must fail explicitly. Fix direction: give ByteReader a limit (length) and a TryRead/Result path (bool Ok, or a sticky Failed flag), have each Message.TryRead verify kind and that count*5 <= remaining before allocating, and reject Blocks.Length > ushort.MaxValue in Write/ByteSize. Add malformed-input tests (truncated, wrong kind, huge count).
