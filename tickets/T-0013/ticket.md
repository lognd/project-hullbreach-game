---
id: T-0013
title: 'S12: Equip a skin'
state: queued
kind: feature
origin: human
created: '2026-09-26'
priority: medium
parent: null
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
- text: A player can select any owned skin as active and cannot select one they do
    not own.
  evidence: []
- text: The active skin is visible on the player's ship in the next match for both
    players.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-33
- owner:a-carten
- game
- platform
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-33

**Card**
As a player, I want to choose which owned skin my ship uses, so that my ship looks the way I want in every match.

**Conversation**
- Is a skin per-ship, per-block-type, or one ship-wide palette?
- Where is it chosen: website only, game only, or both?
