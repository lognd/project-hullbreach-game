+++
id = "01M3DG5Y0V6ZWSJZQW0H5VM80Z"
title = "S38: Break apart under load"
type = "story"
category = "todo"
priority = "critical"
parent = "01M3DG5Y04X6WZ1G61CVD7TX9S"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-09-26T00:00:00Z"
aliases = ["T-0027"]
labels = ["jira:SCRUM-59", "owner:GingerVHS", "game", "physics", "milestone:0.2.0"]

[[acceptance]]
text = "A block whose stress passes its threshold detaches from the hull."
bound = false

[[acceptance]]
text = "Any group of blocks no longer connected to the core detaches as one piece."
bound = false

[[acceptance]]
text = "Detached pieces stop contributing thrust, mass, and weapons to the ship."
bound = false
+++

https://aliens-against-humanity.atlassian.net/browse/SCRUM-59

**Card**
As a player, I want blocks that exceed their failure threshold to detach, and any blocks that lose their connection to the core to fall away, so that damage and bad engineering have visible, physical consequences.

**Conversation**
- Do detached fragments keep flying as debris with their own physics, or vanish after a delay?
- Buckling: a separate compressive criterion, or folded into the stress threshold?

Code already exists: Done, landed early. roadmap.md: Hullbreach.Game.ShipStructure detaching blocks once a ratio exceeds 1 or in a sub-critical buckling mode.
