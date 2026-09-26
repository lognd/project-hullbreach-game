---
id: T-0023
title: 'S34: Build during a fight'
state: queued
kind: feature
origin: human
created: '2026-09-26'
priority: high
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
- text: During a match a player can enter build mode and place blocks using the same
    two-click flow.
  evidence: []
- text: The opponent sees the new blocks within one network round trip.
  evidence: []
- text: Blocks placed mid-match are subject to the same validity and structural rules
    as pre-match blocks.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-55
- owner:stevendangkhoi
- game
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-55

**Card**
As a player, I want to add blocks to my ship while the match is running, so that repairing and adapting mid-fight is part of the strategy.

**Conversation**
- Is there a build cooldown or resource cost mid-match so building is a trade-off, not free?
- Is the ship frozen while building, or does it keep drifting?

Code already exists: Not started. roadmap.md: DemoMode treats Build and Fly as mutually exclusive states, not simultaneous.
