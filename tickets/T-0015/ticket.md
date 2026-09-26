---
id: T-0015
title: 'S26: Start from a title screen'
state: queued
kind: feature
origin: human
created: '2026-09-26'
priority: high
parent: T-0002
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
- text: Launching the game shows a title screen with the listed options, each of which
    leads somewhere.
  evidence: []
- text: The signed-in username is visible on the title screen when signed in.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-47
- owner:stevendangkhoi
- game
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-47

**Card**
As a player, I want a title screen with Play, Build, Settings, Sign in, and Quit, so that I can get where I want without guessing.

**Conversation**
- Does Play go straight to a lobby, or to a mode select (LAN / online / co-op later)?
- Controller support in v1, or keyboard and mouse only?

Code already exists: Not started. roadmap.md: needs a UI scene; nothing under Assets/Scenes but RocketScene.unity/DemoScene.unity.
