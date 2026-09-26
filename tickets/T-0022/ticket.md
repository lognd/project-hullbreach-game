---
id: T-0022
title: 'S33: Choose from a block palette'
state: queued
kind: feature
origin: human
created: '2026-09-26'
priority: high
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
- text: The builder shows every available block type with its mass and any cost.
  evidence: []
- text: The ship's total mass and block count are visible and update as blocks are
    added or removed.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-54
- owner:stevendangkhoi
- game
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-54

**Card**
As a player, I want a palette of block types (hull, armor, thruster, control fin, weapon mounts) with their mass and cost, so that I can weigh armor against mobility as I build.

**Conversation**
- Is there a build budget (mass cap, point cap, block cap) or is size self-limiting through sluggishness?
- Which block types ship in Sprint 1 versus later?

Code already exists: Done. roadmap.md: Hullbreach.Builder.BlockPalette, Hullbreach.Game.BuilderHud (uGUI over BuilderHudModel); tests in BlockPaletteTests.cs.
