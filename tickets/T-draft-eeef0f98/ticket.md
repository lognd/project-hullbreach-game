---
id: T-draft-eeef0f98
title: 'S43: Bend the field with a Gravity Gun and an Anti-Gravity Gun'
state: queued
kind: feature
origin: human
created: '2026-09-26'
priority: high
parent: T-draft-c93bc4b5
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
- text: A Gravity Gun shot creates a well at its impact point that pulls both ships
    and projectiles for its lifetime.
  evidence: []
- text: An Anti-Gravity Gun shot creates a repulsive field with the same rules.
  evidence: []
- text: The field's area and remaining lifetime are visible to both players.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-64
- owner:GingerVHS
- game
- physics
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-64

**Card**
As a player, I want a shot that plants a temporary attractive well where it lands, and one that plants a repulsive one, so that I can pull an opponent into a planetoid or push them off their line.

**Conversation**
- Well strength, radius, and lifetime? Does a well affect the shooter too?
- Do wells stack?

Code already exists: Done, landed early. roadmap.md: GravityGunBehaviour/AntiGravityGunBehaviour, IWorldSink.AddTemporaryGravity, powerup pickups in DemoScene.unity.
