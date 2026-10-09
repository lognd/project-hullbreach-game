+++
id = "01M3DG5Y114RNPPMJWHSPVHVQC"
title = "S44: Fire the Inconvenient Thruster"
type = "story"
category = "todo"
priority = "medium"
parent = "01M3DG5Y06HCEC2NK39DR1347N"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-09-26T00:00:00Z"
aliases = ["T-0033"]
labels = ["jira:SCRUM-65", "owner:GingerVHS", "game", "physics", "milestone:0.3.0"]

[[acceptance]]
text = "The shot homes toward the opponent and attaches a thruster block where it hits."
bound = false

[[acceptance]]
text = "The victim cannot remove the block in build mode."
bound = false

[[acceptance]]
text = "The block applies thrust at its mount point and counts toward the victim's mass and stress model."
bound = false
+++

https://aliens-against-humanity.atlassian.net/browse/SCRUM-65

**Card**
As a player, I want a homing shot that welds an unremovable firing thruster onto the enemy hull, so that I can wreck their handling instead of their armor.

**Conversation**
- Does the welded thruster fire constantly, on the victim's own thrust input, or randomly?
- Can it be destroyed by shooting it, if not removed?

Code already exists: Done, landed early. roadmap.md: Hullbreach.Ship.Behaviours.SeekingThrusterBehaviour, the third Powerups preset.
