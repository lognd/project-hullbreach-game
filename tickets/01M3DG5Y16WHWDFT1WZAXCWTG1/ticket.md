+++
id = "01M3DG5Y16WHWDFT1WZAXCWTG1"
title = "S49: Favor the defender and forgive honest lag"
type = "story"
category = "todo"
priority = "high"
parent = "01M3DG5Y07Q4APXVF7R5P60T9Y"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-09-26T00:00:00Z"
aliases = ["T-0038"]
labels = ["jira:SCRUM-70", "owner:mcnairrobotics", "game", "netcode", "milestone:0.3.0"]

[[acceptance]]
text = "When client and server disagree about a hit, the resolution favors the defender within a documented tolerance."
bound = false

[[acceptance]]
text = "Repeated implausible claims lower a client's trust and tighten its tolerance rather than disconnecting it outright."
bound = false

[[acceptance]]
text = "Trust events are logged and visible to administrators on the platform."
bound = false
+++

https://aliens-against-humanity.atlassian.net/browse/SCRUM-70

**Card**
As a player, I want hit resolution to favor the player being shot at when clients disagree, and the server to tolerate small inconsistencies before treating me as a cheater, so that lag never makes me lose a fight I won on my screen, and I am not kicked for a bad wifi moment.

**Conversation**
- Per-client trust level: what raises and lowers it, and what tolerances tighten as it drops?
- What does the server do at minimum trust: reject claims, or end the match?
- Are trust events reported to the platform for admins to see?

Code already exists: Not started, needs-platform. roadmap.md Sprint 3 table.
