+++
id = "01M3DG5Y0NYCG6S3SA36MNKBZD"
title = "S32: Build around a core"
type = "story"
category = "todo"
priority = "critical"
parent = "01M3DG5Y03DJ72WRTEGD6EBC3C"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-09-26T00:00:00Z"
aliases = ["T-0021"]
labels = ["jira:SCRUM-53", "owner:GingerVHS", "game", "milestone:0.1.0"]

[[acceptance]]
text = "A new ship consists of exactly one core block; it cannot be removed."
bound = false

[[acceptance]]
text = "Destroying the core ends the match with the other player as winner."
bound = false
+++

https://aliens-against-humanity.atlassian.net/browse/SCRUM-53

**Card**
As a player, I want every ship to start from one core block that I must protect, so that there is a clear objective for my opponent and a clear thing for me to defend.

**Conversation**
- Can the core be moved or must the ship grow around it?
- Does the core have any function beyond being the objective (power, control)?

Code already exists: Done. roadmap.md: PlacementRules.CanPlace's core-seeding rule (empty grid accepts only a core).
