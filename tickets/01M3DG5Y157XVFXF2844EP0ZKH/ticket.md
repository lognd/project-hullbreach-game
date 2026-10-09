+++
id = "01M3DG5Y157XVFXF2844EP0ZKH"
title = "S48: Keep the game fluid over the internet"
type = "story"
category = "todo"
priority = "critical"
points = 13
parent = "01M3DG5Y07Q4APXVF7R5P60T9Y"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-10-09T04:03:28Z"
aliases = ["T-0037"]
labels = ["jira:SCRUM-69", "owner:mcnairrobotics", "game", "netcode", "milestone:0.2.0"]

[[acceptance]]
text = "On a throttled connection at 100 ms round trip, a player's own ship responds within one frame of input."
bound = false

[[acceptance]]
text = "Server corrections are applied without visible teleporting under normal packet loss (under 2 percent)."
bound = false

[[acceptance]]
text = "The transport is behind an interface so it can be replaced without touching game code."
bound = false
+++

https://aliens-against-humanity.atlassian.net/browse/SCRUM-69

**Card**
As a player, I want my ship to respond instantly to my input and corrections from the server to be small and rare, so that a 100 ms connection still feels like a local game.

**Conversation**
- Client-side prediction with server reconciliation, interpolation for the opponent. What is the measurable target: playable at 100 ms RTT with at most two frames of visible correction?
- C# sockets first; if the target is missed, migrate the transport to a C++ native plugin with raw UDP sockets behind the same interface. What test tells us we have to?

Code already exists: Not started. roadmap.md: depends on S47's transport landing first; Quantization's helpers are the piece already in place.
