---
id: T-draft-8487ff95
title: 'S36: Compute stress in every block'
state: queued
kind: feature
origin: human
created: '2026-09-26'
priority: critical
parent: T-draft-2a99d859
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
- text: Under thrust, blocks near the thruster show higher stress than blocks far
    from it.
  evidence: []
- text: Adding a brace between two stressed blocks measurably lowers their stress.
  evidence: []
- text: A 60-block ship maintains the agreed frame budget on the reference laptop.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-57
- owner:GingerVHS
- game
- physics
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-57

**Card**
As a player, I want each block to carry real stress under thrust, gravity, and impacts, so that a poorly braced ship actually fails where it was built badly.

**Conversation**
- Each block as one Q8 element with shared nodes, assembled once per hull change; solve on a fixed sub-step. Is the linear elastic model enough, or do we need geometric nonlinearity for buckling?
- Material properties per block type: one stiffness for hull, another for armor?
- Performance budget: what block count on a mid-range laptop must stay above 60 fps?

Code already exists: Done, landed early. roadmap.md: the entire Hullbreach.Structure assembly (Q8Element/NodeLattice/StiffnessAssembly/CgSolver/LoadVector).
