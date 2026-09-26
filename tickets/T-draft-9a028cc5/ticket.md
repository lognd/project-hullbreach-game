---
id: T-draft-9a028cc5
title: 'S49: Favor the defender and forgive honest lag'
state: queued
kind: feature
origin: human
created: '2026-09-26'
priority: high
parent: T-0007
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
- text: When client and server disagree about a hit, the resolution favors the defender
    within a documented tolerance.
  evidence: []
- text: Repeated implausible claims lower a client's trust and tighten its tolerance
    rather than disconnecting it outright.
  evidence: []
- text: Trust events are logged and visible to administrators on the platform.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-70
- owner:mcnairrobotics
- game
- netcode
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-70

**Card**
As a player, I want hit resolution to favor the player being shot at when clients disagree, and the server to tolerate small inconsistencies before treating me as a cheater, so that lag never makes me lose a fight I won on my screen, and I am not kicked for a bad wifi moment.

**Conversation**
- Per-client trust level: what raises and lowers it, and what tolerances tighten as it drops?
- What does the server do at minimum trust: reject claims, or end the match?
- Are trust events reported to the platform for admins to see?

Code already exists: Not started, needs-platform. roadmap.md Sprint 3 table.
