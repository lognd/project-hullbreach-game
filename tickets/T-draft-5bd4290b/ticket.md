---
id: T-draft-5bd4290b
title: 'S39: Fly a ship that handles like it was built'
state: queued
kind: feature
origin: human
created: '2026-09-26'
priority: critical
parent: T-draft-5989e530
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
- text: A thruster mounted off the center of mass produces rotation as well as translation.
  evidence: []
- text: Doubling a ship's mass with no added thrust roughly halves its acceleration.
  evidence: []
- text: Control fins change turning behavior in a way a player can feel.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-60
- owner:GingerVHS
- game
- physics
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-60

**Card**
As a player, I want thrusters and control fins to apply force and torque at the point where they are mounted, so that where I put a thruster changes how the ship turns, and a heavy ship feels heavy.

**Conversation**
- Do we use Unity's 2D rigid body with per-block force application, or our own integrator to share with the game server?
- Control scheme: direct thruster toggles, or a flight-assist layer that maps WASD to the thrusters it finds?

Code already exists: Done. roadmap.md: Hullbreach.Ship.ShipBody/SteeringModel/BlockFacing, Hullbreach.Game.ShipController; tests in ShipBodyTests.cs, ThrusterUpgradesTests.cs.
