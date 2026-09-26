---
id: T-draft-5942609c
title: 'S44: Fire the Inconvenient Thruster'
state: queued
kind: feature
origin: human
created: '2026-09-26'
priority: medium
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
- text: The shot homes toward the opponent and attaches a thruster block where it
    hits.
  evidence: []
- text: The victim cannot remove the block in build mode.
  evidence: []
- text: The block applies thrust at its mount point and counts toward the victim's
    mass and stress model.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-65
- owner:GingerVHS
- game
- physics
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-65

**Card**
As a player, I want a homing shot that welds an unremovable firing thruster onto the enemy hull, so that I can wreck their handling instead of their armor.

**Conversation**
- Does the welded thruster fire constantly, on the victim's own thrust input, or randomly?
- Can it be destroyed by shooting it, if not removed?

Code already exists: Done, landed early. roadmap.md: Hullbreach.Ship.Behaviours.SeekingThrusterBehaviour, the third Powerups preset.
