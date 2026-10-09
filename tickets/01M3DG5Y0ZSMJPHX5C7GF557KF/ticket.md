+++
id = "01M3DG5Y0ZSMJPHX5C7GF557KF"
title = "S42: Shoot a basic cannon"
type = "story"
category = "todo"
priority = "critical"
parent = "01M3DG5Y06HCEC2NK39DR1347N"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-09-26T00:00:00Z"
aliases = ["T-0031"]
labels = ["jira:SCRUM-63", "owner:GingerVHS", "game", "milestone:0.1.0"]

[[acceptance]]
text = "A cannon fires in the direction it is mounted with a cooldown."
bound = false

[[acceptance]]
text = "A projectile that hits a block damages that block and applies an impulse to the ship."
bound = false

[[acceptance]]
text = "Hits are resolved the same way on both players' screens."
bound = false
+++

https://aliens-against-humanity.atlassian.net/browse/SCRUM-63

**Card**
As a player, I want a cannon block that fires projectiles which damage the blocks they hit, so that I can start breaching hulls from the first playable build.

**Conversation**
- Damage as a direct hit-point subtraction, or as an impulse that feeds the stress model?
- Ammunition, heat, or unlimited fire with a cooldown?

Code already exists: Done (TODO.md is stale on this one). roadmap.md: CannonBehaviour/CannonBehaviourBase, ProjectileSpawner/Projectile, wired into DemoScene.unity.
