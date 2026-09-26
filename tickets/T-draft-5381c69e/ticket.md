---
id: T-draft-5381c69e
title: 'S40: Fight inside a gravity field'
state: queued
kind: feature
origin: human
created: '2026-09-26'
priority: high
parent: T-draft-5989e530
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
- text: A drifting ship curves toward a planetoid; a ship very close to one is pushed
    away rather than crushed.
  evidence: []
- text: Projectiles follow curved paths near planetoids.
  evidence: []
- text: Gravity constants are tunable from configuration without a code change.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-61
- owner:GingerVHS
- game
- physics
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-61

**Card**
As a player, I want planetoids that pull my ship and my shots toward them, so that positioning and orbits matter as much as aim.

**Conversation**
- Non-physical force law: attractive at range, repulsive very close, so nobody gets trapped. What are the constants and are they per-map?
- Do planetoids move, or are they fixed for v1?
- Do ships attract each other?

Code already exists: Done. roadmap.md: Hullbreach.World.GravityField/GravityBody/OrbitHelper, Hullbreach.Game.GravityWorld, ShipBody.ApplyGravityForces.
