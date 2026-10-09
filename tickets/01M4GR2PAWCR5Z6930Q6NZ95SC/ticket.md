+++
id = "01M4GR2PAWCR5Z6930Q6NZ95SC"
title = "ClientReplica.ApplySnapshot replaces the ship before the sequence check and resets one global baseline for all ships"
type = "bug"
category = "todo"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:31:27Z"
updated = "2026-10-09T17:09:51Z"
labels = ["origin:auditor", "audit:hullbreach-net"]
scope = ["Assets/Scripts/Hullbreach.Net/ClientReplica.cs"]

[[acceptance]]
text = "Stale snapshots do not rewind a ship and one ship's snapshot does not discard another ship's pending events"
bound = false
+++

ClientReplica.cs:96-128: _ships[snapshot.NetId] = replica (line 120) happens unconditionally, then the sequence guard (line 122) only decides the baseline. A reordered/duplicated older snapshot (reliable is exactly-once but unordered per ITransport.cs:7) therefore overwrites a newer replica and rewinds blocks that later BlockDestroyed events (already applied, now stale) will never re-apply. In addition the baseline is one global _lastAppliedSequence across ALL ships (server sequence is global, ServerSimulation.cs:416), so a new player's join snapshot with sequence N makes DiscardStaleBuffered (line 126) drop pending BlockDestroyed/BlockDamaged for other ships with sequence < N that have not been applied yet; those clients diverge permanently. Fix direction: ignore (return false) snapshots with sequence <= the ship's last-applied snapshot sequence, track baselines per netId (apply buffered events with seq > that ship's snapshot seq to it only), and have ApplySnapshot return a bool/enum so the caller sees why it was dropped.
