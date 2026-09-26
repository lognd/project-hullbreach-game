---
id: T-draft-fd1cf6a8
title: 'S47: Run matches on an authoritative server'
state: queued
kind: feature
origin: human
created: '2026-09-26'
priority: critical
parent: T-0007
tier: story
sprint: sprint-1
runs_last: false
milestone: 0.1.0
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
- text: Both clients send inputs over UDP and receive authoritative state from the
    server.
  evidence: []
- text: A client that stops sending is disconnected after a timeout and the match
    resolves per the rules.
  evidence: []
- text: The same server binary hosts LAN and internet matches.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-68
- owner:mcnairrobotics
- game
- netcode
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-68

**Card**
As a player, I want the match to be simulated on a server that both clients send inputs to, so that my opponent's client cannot decide that I lost.

**Conversation**
- Headless Unity build as the server versus a separate .NET process sharing the simulation code. Which is easier to keep in sync with the client?
- Tick rate? Snapshot format and size budget per tick?

Code already exists: Partially done. roadmap.md: NetMessages wire protocol design and Quantization exist; no transport, headless server loop, or serializer written yet.
