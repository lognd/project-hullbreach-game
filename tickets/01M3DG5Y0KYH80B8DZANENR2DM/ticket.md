+++
id = "01M3DG5Y0KYH80B8DZANENR2DM"
title = "S30: Place a block with two clicks"
type = "story"
category = "todo"
priority = "critical"
parent = "01M3DG5Y03DJ72WRTEGD6EBC3C"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-09-26T00:00:00Z"
aliases = ["T-0019"]
labels = ["jira:SCRUM-51", "owner:stevendangkhoi", "game", "milestone:0.1.0"]

[[acceptance]]
text = "Hovering shows whether a location is valid before the first click; an invalid location cannot be selected."
bound = false

[[acceptance]]
text = "After the first click, moving the cursor previews the orientation and the second click commits it."
bound = false

[[acceptance]]
text = "The placed block is attached to the hull and moves with the ship."
bound = false
+++

https://aliens-against-humanity.atlassian.net/browse/SCRUM-51

**Card**
As a player, I want to click a valid attachment point to place a block and click again to set its orientation, so that building is fast and every placement is deliberate.

**Conversation**
- Grid-locked placement, or free placement with snapping?
- What defines 'valid': at least one shared edge with an existing block, and no overlap? Anything about the core?
- Orientation for symmetric blocks: skip the second click?

Code already exists: Done. roadmap.md: Hullbreach.Builder.PlacementRules/BuilderSession, Hullbreach.Game.BuilderController, with edit-mode tests.
