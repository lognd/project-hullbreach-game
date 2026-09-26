---
id: T-draft-7239c6b3
title: 'S32: Build around a core'
state: queued
kind: feature
origin: human
created: '2026-09-26'
priority: critical
parent: T-draft-35bc243e
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
- text: A new ship consists of exactly one core block; it cannot be removed.
  evidence: []
- text: Destroying the core ends the match with the other player as winner.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-53
- owner:GingerVHS
- game
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-53

**Card**
As a player, I want every ship to start from one core block that I must protect, so that there is a clear objective for my opponent and a clear thing for me to defend.

**Conversation**
- Can the core be moved or must the ship grow around it?
- Does the core have any function beyond being the objective (power, control)?

Code already exists: Done. roadmap.md: PlacementRules.CanPlace's core-seeding rule (empty grid accepts only a core).
