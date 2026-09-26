---
id: T-0034
title: 'S45: Dodge telegraphed hazards'
state: queued
kind: feature
origin: human
created: '2026-09-26'
priority: high
parent: T-0006
tier: story
sprint: sprint-3
runs_last: false
milestone: 0.3.0
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
- text: Every hazard appears on the radar at least a few seconds before it can hit
    a ship.
  evidence: []
- text: A hazard that hits a ship applies impact loads through the stress model.
  evidence: []
- text: Hazard frequency is configurable per map.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-66
- owner:GingerVHS
- game
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-66

**Card**
As a player, I want asteroids and solar flares that are announced on a radar before they arrive, so that the arena is dangerous but never unfair.

**Conversation**
- Warning lead time? Radar as a minimap or an edge-of-screen indicator?
- Do hazards hit both players equally, or are they positioned to break stalemates?

Code already exists: Not started. roadmap.md: no hazard system exists.
