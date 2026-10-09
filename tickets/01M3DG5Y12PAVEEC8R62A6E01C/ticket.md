+++
id = "01M3DG5Y12PAVEEC8R62A6E01C"
title = "S45: Dodge telegraphed hazards"
type = "story"
category = "todo"
priority = "high"
parent = "01M3DG5Y06HCEC2NK39DR1347N"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-09-26T00:00:00Z"
aliases = ["T-0034"]
labels = ["jira:SCRUM-66", "owner:GingerVHS", "game", "milestone:0.3.0"]

[[acceptance]]
text = "Every hazard appears on the radar at least a few seconds before it can hit a ship."
bound = false

[[acceptance]]
text = "A hazard that hits a ship applies impact loads through the stress model."
bound = false

[[acceptance]]
text = "Hazard frequency is configurable per map."
bound = false
+++

https://aliens-against-humanity.atlassian.net/browse/SCRUM-66

**Card**
As a player, I want asteroids and solar flares that are announced on a radar before they arrive, so that the arena is dangerous but never unfair.

**Conversation**
- Warning lead time? Radar as a minimap or an edge-of-screen indicator?
- Do hazards hit both players equally, or are they positioned to break stalemates?

Code already exists: Not started. roadmap.md: no hazard system exists.
