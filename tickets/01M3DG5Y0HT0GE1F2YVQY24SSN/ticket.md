+++
id = "01M3DG5Y0HT0GE1F2YVQY24SSN"
title = "S28: Host or join a LAN match"
type = "story"
category = "todo"
priority = "critical"
parent = "01M3DG5Y02BXW6QBAG8X1JFSW2"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-09-26T00:00:00Z"
aliases = ["T-0017"]
labels = ["jira:SCRUM-49", "owner:mcnairrobotics", "game", "milestone:0.1.0"]

[[acceptance]]
text = "One player can host and another on the same network can join by entering the host's address."
bound = false

[[acceptance]]
text = "Both players see each other's ships and the match starts when both are ready."
bound = false

[[acceptance]]
text = "A LAN match works with no platform connection, and is unranked."
bound = false
+++

https://aliens-against-humanity.atlassian.net/browse/SCRUM-49

**Card**
As a player, I want to host a match on my network and have a friend join by address, so that we can play at a LAN party without any internet.

**Conversation**
- Host is also a player (listen server) or a separate headless process on the same machine?
- LAN discovery / broadcast, or type an IP?

Code already exists: Partially done. roadmap.md: Hullbreach.Net.NetMessages/Quantization exist as wire formats and quantization helpers; no transport is wired to anything.
