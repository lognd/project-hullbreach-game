---
id: T-draft-df4d6b80
title: 'S37: See where my ship is about to fail'
state: queued
kind: feature
origin: human
created: '2026-09-26'
priority: critical
parent: T-0004
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
- text: Each block's color reflects its current stress relative to its failure threshold,
    updating live.
  evidence: []
- text: The mapping is documented and consistent between build mode and combat.
  evidence: []
- text: An alternative palette is available for red-green colorblind players.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-58
- owner:a-carten
- game
- physics
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-58

**Card**
As a player, I want every block tinted from green to red by how close it is to failure, so that I can fix a weak design before it breaks.

**Conversation**
- Continuous gradient or three bands? Is the display always on, on a key, or only in build mode?
- Colorblind-safe alternative to green/red?

Code already exists: Done, landed early. roadmap.md: Stress/LoadBearing/Buckling overlays in ShipRenderer plus HullWarningBanner.
