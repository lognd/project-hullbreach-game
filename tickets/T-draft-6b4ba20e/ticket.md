---
id: T-draft-6b4ba20e
title: 'S35: Save and load ship designs'
state: queued
kind: feature
origin: human
created: '2026-09-26'
priority: medium
parent: T-0003
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
- text: A player can save the current design under a name and load it later.
  evidence: []
- text: A loaded design that violates current rules is reported rather than silently
    altered.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-56
- owner:stevendangkhoi
- game
- platform
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-56

**Card**
As a player, I want to save a ship design and load it before a match, so that I do not rebuild from scratch every game.

**Conversation**
- Local files, or stored on the platform under the account?
- Is a design validated on load against the current block rules?

Code already exists: Not started, needs-platform. roadmap.md: no serialization of a BlockGrid to/from a design format exists yet.
