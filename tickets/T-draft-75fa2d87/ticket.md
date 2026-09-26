---
id: T-draft-75fa2d87
title: 'S29: Find an online opponent'
state: queued
kind: feature
origin: human
created: '2026-09-26'
priority: high
parent: T-draft-cf4f464a
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
- text: A signed-in player can enter a queue, see they are queued, and cancel.
  evidence: []
- text: Two queued players within the rating window are placed into the same match
    on a game server without either typing an address.
  evidence: []
- text: The match is recorded as ranked when it ends.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-50
- owner:lognd
- game
- platform
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-50

**Card**
As a player, I want to queue for a ranked match and be paired with an opponent of similar rating, so that matches are fair and I do not have to arrange them myself.

**Conversation**
- Who runs matchmaking: the platform API, or a lobby service on the game server host?
- Widening rating window over time in queue? Maximum wait before matching anyone?
- How many game servers do we run, and where?

Code already exists: Not started, needs-platform. roadmap.md: depends on the platform repo's matchmaking API.
