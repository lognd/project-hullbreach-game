+++
id = "01M3DG5Y107GDBD1ZZQK0H4AKX"
title = "S43: Bend the field with a Gravity Gun and an Anti-Gravity Gun"
type = "story"
category = "todo"
priority = "high"
parent = "01M3DG5Y06HCEC2NK39DR1347N"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-09-26T00:00:00Z"
aliases = ["T-0032"]
labels = ["jira:SCRUM-64", "owner:GingerVHS", "game", "physics", "milestone:0.2.0"]

[[acceptance]]
text = "A Gravity Gun shot creates a well at its impact point that pulls both ships and projectiles for its lifetime."
bound = false

[[acceptance]]
text = "An Anti-Gravity Gun shot creates a repulsive field with the same rules."
bound = false

[[acceptance]]
text = "The field's area and remaining lifetime are visible to both players."
bound = false
+++

https://aliens-against-humanity.atlassian.net/browse/SCRUM-64

**Card**
As a player, I want a shot that plants a temporary attractive well where it lands, and one that plants a repulsive one, so that I can pull an opponent into a planetoid or push them off their line.

**Conversation**
- Well strength, radius, and lifetime? Does a well affect the shooter too?
- Do wells stack?

Code already exists: Done, landed early. roadmap.md: GravityGunBehaviour/AntiGravityGunBehaviour, IWorldSink.AddTemporaryGravity, powerup pickups in DemoScene.unity.
