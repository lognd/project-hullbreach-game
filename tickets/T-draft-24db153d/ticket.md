---
id: T-draft-24db153d
title: 'S42: Shoot a basic cannon'
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
- text: A cannon fires in the direction it is mounted with a cooldown.
  evidence: []
- text: A projectile that hits a block damages that block and applies an impulse to
    the ship.
  evidence: []
- text: Hits are resolved the same way on both players' screens.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-63
- owner:GingerVHS
- game
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-63

**Card**
As a player, I want a cannon block that fires projectiles which damage the blocks they hit, so that I can start breaching hulls from the first playable build.

**Conversation**
- Damage as a direct hit-point subtraction, or as an impulse that feeds the stress model?
- Ammunition, heat, or unlimited fire with a cooldown?

Code already exists: Done (TODO.md is stale on this one). roadmap.md: CannonBehaviour/CannonBehaviourBase, ProjectileSpawner/Projectile, wired into DemoScene.unity.
