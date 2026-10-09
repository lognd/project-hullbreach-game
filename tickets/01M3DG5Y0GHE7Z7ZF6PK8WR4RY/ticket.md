+++
id = "01M3DG5Y0GHE7Z7ZF6PK8WR4RY"
title = "S27: Adjust settings"
type = "story"
category = "todo"
priority = "medium"
parent = "01M3DG5Y02BXW6QBAG8X1JFSW2"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-09-26T00:00:00Z"
aliases = ["T-0016"]
labels = ["jira:SCRUM-48", "owner:stevendangkhoi", "game", "milestone:0.1.0"]

[[acceptance]]
text = "Volume, display, and control settings can be changed from a settings screen and take effect immediately."
bound = false

[[acceptance]]
text = "Settings persist across launches on the same machine."
bound = false
+++

https://aliens-against-humanity.atlassian.net/browse/SCRUM-48

**Card**
As a player, I want to change master, music, and effects volume, resolution, and key bindings, so that the game is comfortable on my machine.

**Conversation**
- Rebindable keys in v1, or fixed with a displayed map?
- Where are settings persisted: a local file only?

Code already exists: Not started. roadmap.md: same UI-scene dependency as S26; also the point to migrate off the legacy Input class.
