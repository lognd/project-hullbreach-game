+++
id = "01M3DG5Y0J805WP05DZV8GWAWA"
title = "S29: Find an online opponent"
type = "story"
category = "todo"
priority = "high"
parent = "01M3DG5Y02BXW6QBAG8X1JFSW2"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-09-26T00:00:00Z"
aliases = ["T-0018"]
labels = ["jira:SCRUM-50", "owner:lognd", "game", "platform", "milestone:0.2.0"]

[[acceptance]]
text = "A signed-in player can enter a queue, see they are queued, and cancel."
bound = false

[[acceptance]]
text = "Two queued players within the rating window are placed into the same match on a game server without either typing an address."
bound = false

[[acceptance]]
text = "The match is recorded as ranked when it ends."
bound = false
+++

https://aliens-against-humanity.atlassian.net/browse/SCRUM-50

**Card**
As a player, I want to queue for a ranked match and be paired with an opponent of similar rating, so that matches are fair and I do not have to arrange them myself.

**Conversation**
- Who runs matchmaking: the platform API, or a lobby service on the game server host?
- Widening rating window over time in queue? Maximum wait before matching anyone?
- How many game servers do we run, and where?

Code already exists: Not started, needs-platform. roadmap.md: depends on the platform repo's matchmaking API.
