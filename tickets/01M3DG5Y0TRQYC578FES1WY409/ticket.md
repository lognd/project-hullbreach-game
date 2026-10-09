+++
id = "01M3DG5Y0TRQYC578FES1WY409"
title = "S37: See where my ship is about to fail"
type = "story"
category = "todo"
priority = "critical"
points = 5
parent = "01M3DG5Y04X6WZ1G61CVD7TX9S"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-10-09T04:03:27Z"
aliases = ["T-0026"]
labels = ["jira:SCRUM-58", "owner:a-carten", "game", "physics", "milestone:0.2.0"]

[[acceptance]]
text = "Each block's color reflects its current stress relative to its failure threshold, updating live."
bound = false

[[acceptance]]
text = "The mapping is documented and consistent between build mode and combat."
bound = false

[[acceptance]]
text = "An alternative palette is available for red-green colorblind players."
bound = false
+++

https://aliens-against-humanity.atlassian.net/browse/SCRUM-58

**Card**
As a player, I want every block tinted from green to red by how close it is to failure, so that I can fix a weak design before it breaks.

**Conversation**
- Continuous gradient or three bands? Is the display always on, on a key, or only in build mode?
- Colorblind-safe alternative to green/red?

Code already exists: Done, landed early. roadmap.md: Stress/LoadBearing/Buckling overlays in ShipRenderer plus HullWarningBanner.
