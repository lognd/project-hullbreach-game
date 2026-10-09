+++
id = "01M3DG5Y14KAMJ8NTGHC8BW9YV"
title = "S47: Run matches on an authoritative server"
type = "story"
category = "todo"
priority = "critical"
parent = "01M3DG5Y07Q4APXVF7R5P60T9Y"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-09-26T00:00:00Z"
aliases = ["T-0036"]
labels = ["jira:SCRUM-68", "owner:mcnairrobotics", "game", "netcode", "milestone:0.1.0"]

[[acceptance]]
text = "Both clients send inputs over UDP and receive authoritative state from the server."
bound = false

[[acceptance]]
text = "A client that stops sending is disconnected after a timeout and the match resolves per the rules."
bound = false

[[acceptance]]
text = "The same server binary hosts LAN and internet matches."
bound = false
+++

https://aliens-against-humanity.atlassian.net/browse/SCRUM-68

**Card**
As a player, I want the match to be simulated on a server that both clients send inputs to, so that my opponent's client cannot decide that I lost.

**Conversation**
- Headless Unity build as the server versus a separate .NET process sharing the simulation code. Which is easier to keep in sync with the client?
- Tick rate? Snapshot format and size budget per tick?

Code already exists: Partially done. roadmap.md: NetMessages wire protocol design and Quantization exist; no transport, headless server loop, or serializer written yet.
