+++
id = "01M3DG5Y0PKYGBXMHTC9BQE40M"
title = "S33: Choose from a block palette"
type = "story"
category = "todo"
priority = "high"
parent = "01M3DG5Y03DJ72WRTEGD6EBC3C"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-09-26T00:00:00Z"
aliases = ["T-0022"]
labels = ["jira:SCRUM-54", "owner:stevendangkhoi", "game", "milestone:0.1.0"]

[[acceptance]]
text = "The builder shows every available block type with its mass and any cost."
bound = false

[[acceptance]]
text = "The ship's total mass and block count are visible and update as blocks are added or removed."
bound = false
+++

https://aliens-against-humanity.atlassian.net/browse/SCRUM-54

**Card**
As a player, I want a palette of block types (hull, armor, thruster, control fin, weapon mounts) with their mass and cost, so that I can weigh armor against mobility as I build.

**Conversation**
- Is there a build budget (mass cap, point cap, block cap) or is size self-limiting through sluggishness?
- Which block types ship in Sprint 1 versus later?

Code already exists: Done. roadmap.md: Hullbreach.Builder.BlockPalette, Hullbreach.Game.BuilderHud (uGUI over BuilderHudModel); tests in BlockPaletteTests.cs.
