+++
id = "01M3DG5Y0R3XBGSCEYQ0383EDK"
title = "S35: Save and load ship designs"
type = "story"
category = "todo"
priority = "medium"
parent = "01M3DG5Y03DJ72WRTEGD6EBC3C"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-09-26T00:00:00Z"
aliases = ["T-0024"]
labels = ["jira:SCRUM-56", "owner:stevendangkhoi", "game", "platform", "milestone:0.2.0"]

[[acceptance]]
text = "A player can save the current design under a name and load it later."
bound = false

[[acceptance]]
text = "A loaded design that violates current rules is reported rather than silently altered."
bound = false
+++

https://aliens-against-humanity.atlassian.net/browse/SCRUM-56

**Card**
As a player, I want to save a ship design and load it before a match, so that I do not rebuild from scratch every game.

**Conversation**
- Local files, or stored on the platform under the account?
- Is a design validated on load against the current block rules?

Code already exists: Not started, needs-platform. roadmap.md: no serialization of a BlockGrid to/from a design format exists yet.
