---
id: T-0017
title: 'S28: Host or join a LAN match'
state: queued
kind: feature
origin: human
created: '2026-09-26'
priority: critical
parent: T-0002
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
- text: One player can host and another on the same network can join by entering the
    host's address.
  evidence: []
- text: Both players see each other's ships and the match starts when both are ready.
  evidence: []
- text: A LAN match works with no platform connection, and is unranked.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-49
- owner:mcnairrobotics
- game
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-49

**Card**
As a player, I want to host a match on my network and have a friend join by address, so that we can play at a LAN party without any internet.

**Conversation**
- Host is also a player (listen server) or a separate headless process on the same machine?
- LAN discovery / broadcast, or type an IP?

Code already exists: Partially done. roadmap.md: Hullbreach.Net.NetMessages/Quantization exist as wire formats and quantization helpers; no transport is wired to anything.
