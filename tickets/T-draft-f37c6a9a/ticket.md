---
id: T-draft-f37c6a9a
title: 'S31: Remove and undo blocks'
state: queued
kind: feature
origin: human
created: '2026-09-26'
priority: high
parent: T-draft-35bc243e
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
- text: A player can remove any block except the core; blocks that would be left unattached
    are handled per the agreed rule.
  evidence: []
- text: Undo reverses the last placement or removal, at least ten deep.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-52
- owner:stevendangkhoi
- game
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-52

**Card**
As a player, I want to remove a block I placed and undo my last few actions, so that a misclick does not cost me the whole design.

**Conversation**
- Can removing a block leave others detached? If so, remove them too, or refuse?
- Undo depth?

Code already exists: Done. roadmap.md: PlacementRules.Detach + Hullbreach.Builder.UndoStack; tests in UndoStackTests.cs.
