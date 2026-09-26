---
id: T-draft-66f6a7f6
title: 'S27: Adjust settings'
state: queued
kind: feature
origin: human
created: '2026-09-26'
priority: medium
parent: T-draft-cf4f464a
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
- text: Volume, display, and control settings can be changed from a settings screen
    and take effect immediately.
  evidence: []
- text: Settings persist across launches on the same machine.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-48
- owner:stevendangkhoi
- game
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-48

**Card**
As a player, I want to change master, music, and effects volume, resolution, and key bindings, so that the game is comfortable on my machine.

**Conversation**
- Rebindable keys in v1, or fixed with a displayed map?
- Where are settings persisted: a local file only?

Code already exists: Not started. roadmap.md: same UI-scene dependency as S26; also the point to migrate off the legacy Input class.
