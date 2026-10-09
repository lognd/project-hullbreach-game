+++
id = "01M4GR2KS7KV38NGT1YA6FDXKJ"
title = "ClientReplica.ApplyReceived/ApplyReliable: malformed or unknown reliable payload throws and permanently stalls sequence apply"
type = "bug"
category = "todo"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:31:24Z"
updated = "2026-10-09T17:09:25Z"
labels = ["origin:auditor", "audit:hullbreach-net"]
scope = ["Assets/Scripts/Hullbreach.Net/ClientReplica.cs"]

[[acceptance]]
text = "A malformed or unknown reliable payload never throws and does not stall later sequences"
bound = false
+++

ClientReplica.cs:171-204: ApplyReceived indexes into[0..4] without checking length (>=5 for reliable) and any non-snapshot/state/well kind (including Input=1, or garbage) goes to the reliable default branch, which parses a sequence from garbage and buffers it. ApplyReliable (ClientReplica.cs:214-219) removes the payload from _pendingReliable BEFORE ApplyOne, and ApplyOne throws InvalidOperationException for an unexpected kind (ClientReplica.cs:273) or a reader overrun, before _lastAppliedSequence++ runs: every later reliable message is then blocked forever (sequence last+1 is gone) and the exception escapes the transport pump. Also a far-future sequence is buffered with no cap (ClientReplica.cs:212) so a peer can grow _pendingReliable without bound. Also sequence arithmetic wraps at uint.MaxValue (_lastAppliedSequence + 1). Fix direction: ApplyReceived returns a Result/enum (Applied, Dropped(reason), Malformed) and never throws on wire data; validate length and kind before buffering; advance _lastAppliedSequence even if a message is malformed (or resync by requesting a snapshot); cap the pending window (e.g. drop sequence > last + N); document the failure returns on the method.
