---
id: T-draft-b27a240f
title: 'S48: Keep the game fluid over the internet'
state: queued
kind: feature
origin: human
created: '2026-09-26'
priority: critical
parent: T-0007
tier: story
sprint: sprint-2
runs_last: false
milestone: 0.2.0
flavour: null
due: null
rank: null
points: null
unsized_ack: false
unsized_ack_reason: null
tokens_in: null
tokens_out: null
tokens_cache_read: null
usage: null
runs_last_parallel_safe: false
runs_last_parallel_safe_reason: null
worktree: null
branch: null
scope_breadth_ack: false
scope_breadth_ack_reason: null
no_scope_declared: false
no_scope_declared_reason: null
designated_repro_test: null
acceptance:
- text: On a throttled connection at 100 ms round trip, a player's own ship responds
    within one frame of input.
  evidence: []
- text: Server corrections are applied without visible teleporting under normal packet
    loss (under 2 percent).
  evidence: []
- text: The transport is behind an interface so it can be replaced without touching
    game code.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-69
- owner:mcnairrobotics
- game
- netcode
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-69

**Card**
As a player, I want my ship to respond instantly to my input and corrections from the server to be small and rare, so that a 100 ms connection still feels like a local game.

**Conversation**
- Client-side prediction with server reconciliation, interpolation for the opponent. What is the measurable target: playable at 100 ms RTT with at most two frames of visible correction?
- C# sockets first; if the target is missed, migrate the transport to a C++ native plugin with raw UDP sockets behind the same interface. What test tells us we have to?

Code already exists: Not started. roadmap.md: depends on S47's transport landing first; Quantization's helpers are the piece already in place.
