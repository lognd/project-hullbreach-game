---
id: T-0014
title: 'S16: Record a finished match'
state: queued
kind: feature
origin: human
created: '2026-09-26'
priority: critical
parent: null
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
- text: An authenticated game server can submit a match result once; a duplicate submission
    is recognized and not double-counted.
  evidence: []
- text: A result submitted by an unauthenticated caller is rejected.
  evidence: []
- text: Both players' histories show the match within seconds of the report.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-37
- owner:lognd
- game
- platform
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-37

**Card**
As a game server, I want to report a completed match with both players, the winner, duration, and per-player stats, so that the platform is the single source of truth for what happened.

**Conversation**
- How does the game server authenticate to the API: a server API key, or the players' own tokens?
- Which stats are worth recording in v1: damage dealt, blocks destroyed, blocks placed mid-match, time alive?
- Idempotency: what if the server retries a report after a timeout?
