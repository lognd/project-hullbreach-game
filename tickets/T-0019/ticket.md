---
id: T-0019
title: 'S30: Place a block with two clicks'
state: queued
kind: feature
origin: human
created: '2026-09-26'
priority: critical
parent: T-0003
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
- text: Hovering shows whether a location is valid before the first click; an invalid
    location cannot be selected.
  evidence: []
- text: After the first click, moving the cursor previews the orientation and the
    second click commits it.
  evidence: []
- text: The placed block is attached to the hull and moves with the ship.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-51
- owner:stevendangkhoi
- game
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-51

**Card**
As a player, I want to click a valid attachment point to place a block and click again to set its orientation, so that building is fast and every placement is deliberate.

**Conversation**
- Grid-locked placement, or free placement with snapping?
- What defines 'valid': at least one shared edge with an existing block, and no overlap? Anything about the core?
- Orientation for symmetric blocks: skip the second click?

Code already exists: Done. roadmap.md: Hullbreach.Builder.PlacementRules/BuilderSession, Hullbreach.Game.BuilderController, with edit-mode tests.
