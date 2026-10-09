+++
id = "01M3DG5Y0ET03V3C636YJ6MQYD"
title = "S16: Record a finished match"
type = "story"
category = "todo"
priority = "critical"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-09-26T00:00:00Z"
aliases = ["T-0014"]
labels = ["jira:SCRUM-37", "owner:lognd", "game", "platform", "milestone:0.2.0"]

[[acceptance]]
text = "An authenticated game server can submit a match result once; a duplicate submission is recognized and not double-counted."
bound = false

[[acceptance]]
text = "A result submitted by an unauthenticated caller is rejected."
bound = false

[[acceptance]]
text = "Both players' histories show the match within seconds of the report."
bound = false
+++

https://aliens-against-humanity.atlassian.net/browse/SCRUM-37

**Card**
As a game server, I want to report a completed match with both players, the winner, duration, and per-player stats, so that the platform is the single source of truth for what happened.

**Conversation**
- How does the game server authenticate to the API: a server API key, or the players' own tokens?
- Which stats are worth recording in v1: damage dealt, blocks destroyed, blocks placed mid-match, time alive?
- Idempotency: what if the server retries a report after a timeout?
