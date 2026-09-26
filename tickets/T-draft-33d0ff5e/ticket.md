---
id: T-draft-33d0ff5e
title: 'S38: Break apart under load'
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
- text: A block whose stress passes its threshold detaches from the hull.
  evidence: []
- text: Any group of blocks no longer connected to the core detaches as one piece.
  evidence: []
- text: Detached pieces stop contributing thrust, mass, and weapons to the ship.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-59
- owner:GingerVHS
- game
- physics
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-59

**Card**
As a player, I want blocks that exceed their failure threshold to detach, and any blocks that lose their connection to the core to fall away, so that damage and bad engineering have visible, physical consequences.

**Conversation**
- Do detached fragments keep flying as debris with their own physics, or vanish after a delay?
- Buckling: a separate compressive criterion, or folded into the stress threshold?

Code already exists: Done, landed early. roadmap.md: Hullbreach.Game.ShipStructure detaching blocks once a ratio exceeds 1 or in a sub-critical buckling mode.
