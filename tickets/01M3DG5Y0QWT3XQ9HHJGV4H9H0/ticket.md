+++
id = "01M3DG5Y0QWT3XQ9HHJGV4H9H0"
title = "S34: Build during a fight"
type = "story"
category = "todo"
priority = "high"
parent = "01M3DG5Y03DJ72WRTEGD6EBC3C"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-09-26T00:00:00Z"
aliases = ["T-0023"]
labels = ["jira:SCRUM-55", "owner:stevendangkhoi", "game", "milestone:0.2.0"]

[[acceptance]]
text = "During a match a player can enter build mode and place blocks using the same two-click flow."
bound = false

[[acceptance]]
text = "The opponent sees the new blocks within one network round trip."
bound = false

[[acceptance]]
text = "Blocks placed mid-match are subject to the same validity and structural rules as pre-match blocks."
bound = false
+++

https://aliens-against-humanity.atlassian.net/browse/SCRUM-55

**Card**
As a player, I want to add blocks to my ship while the match is running, so that repairing and adapting mid-fight is part of the strategy.

**Conversation**
- Is there a build cooldown or resource cost mid-match so building is a trade-off, not free?
- Is the ship frozen while building, or does it keep drifting?

Code already exists: Not started. roadmap.md: DemoMode treats Build and Fly as mutually exclusive states, not simultaneous.
