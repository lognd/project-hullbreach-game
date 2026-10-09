+++
id = "01M3DG5Y134BJMWZH7S1QV7EY9"
title = "S46: Win by breaching the core"
type = "story"
category = "todo"
priority = "critical"
parent = "01M3DG5Y06HCEC2NK39DR1347N"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-09-26T00:00:00Z"
aliases = ["T-0035"]
labels = ["jira:SCRUM-67", "owner:mcnairrobotics", "game", "milestone:0.1.0"]

[[acceptance]]
text = "Destroying the opposing core ends the match and shows winner, duration, and both players' stats."
bound = false

[[acceptance]]
text = "If the time limit is reached the documented tiebreak decides the winner."
bound = false

[[acceptance]]
text = "Both players are returned to the lobby or offered a rematch."
bound = false
+++

https://aliens-against-humanity.atlassian.net/browse/SCRUM-67

**Card**
As a player, I want the match to end the moment an opponent's core is destroyed, with a results screen, so that every fight has a clear finish.

**Conversation**
- Time limit and tiebreak (most core damage? most mass remaining?)
- Rematch from the results screen?

Code already exists: Not started. roadmap.md: no match state machine exists; BlockGrid.CoreKey/Connectivity/Articulation already detect core loss.
