+++
id = "01M3DG5Y0FMGFR0WY5PJG1C3WZ"
title = "S26: Start from a title screen"
type = "story"
category = "todo"
priority = "high"
parent = "01M3DG5Y02BXW6QBAG8X1JFSW2"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-09-26T00:00:00Z"
aliases = ["T-0015"]
labels = ["jira:SCRUM-47", "owner:stevendangkhoi", "game", "milestone:0.1.0"]

[[acceptance]]
text = "Launching the game shows a title screen with the listed options, each of which leads somewhere."
bound = false

[[acceptance]]
text = "The signed-in username is visible on the title screen when signed in."
bound = false
+++

https://aliens-against-humanity.atlassian.net/browse/SCRUM-47

**Card**
As a player, I want a title screen with Play, Build, Settings, Sign in, and Quit, so that I can get where I want without guessing.

**Conversation**
- Does Play go straight to a lobby, or to a mode select (LAN / online / co-op later)?
- Controller support in v1, or keyboard and mouse only?

Code already exists: Not started. roadmap.md: needs a UI scene; nothing under Assets/Scenes but RocketScene.unity/DemoScene.unity.
