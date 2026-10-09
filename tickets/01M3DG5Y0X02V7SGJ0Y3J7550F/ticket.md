+++
id = "01M3DG5Y0X02V7SGJ0Y3J7550F"
title = "S40: Fight inside a gravity field"
type = "story"
category = "todo"
priority = "high"
parent = "01M3DG5Y05HP0YSRB6819FBK2S"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-09-26T00:00:00Z"
aliases = ["T-0029"]
labels = ["jira:SCRUM-61", "owner:GingerVHS", "game", "physics", "milestone:0.2.0"]

[[acceptance]]
text = "A drifting ship curves toward a planetoid; a ship very close to one is pushed away rather than crushed."
bound = false

[[acceptance]]
text = "Projectiles follow curved paths near planetoids."
bound = false

[[acceptance]]
text = "Gravity constants are tunable from configuration without a code change."
bound = false
+++

https://aliens-against-humanity.atlassian.net/browse/SCRUM-61

**Card**
As a player, I want planetoids that pull my ship and my shots toward them, so that positioning and orbits matter as much as aim.

**Conversation**
- Non-physical force law: attractive at range, repulsive very close, so nobody gets trapped. What are the constants and are they per-map?
- Do planetoids move, or are they fixed for v1?
- Do ships attract each other?

Code already exists: Done. roadmap.md: Hullbreach.World.GravityField/GravityBody/OrbitHelper, Hullbreach.Game.GravityWorld, ShipBody.ApplyGravityForces.
