---
id: T-0035
title: 'S46: Win by breaching the core'
state: queued
kind: feature
origin: human
created: '2026-09-26'
priority: critical
parent: T-0006
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
- text: Destroying the opposing core ends the match and shows winner, duration, and
    both players' stats.
  evidence: []
- text: If the time limit is reached the documented tiebreak decides the winner.
  evidence: []
- text: Both players are returned to the lobby or offered a rematch.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-67
- owner:mcnairrobotics
- game
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-67

**Card**
As a player, I want the match to end the moment an opponent's core is destroyed, with a results screen, so that every fight has a clear finish.

**Conversation**
- Time limit and tiebreak (most core damage? most mass remaining?)
- Rematch from the results screen?

Code already exists: Not started. roadmap.md: no match state machine exists; BlockGrid.CoreKey/Connectivity/Articulation already detect core loss.
