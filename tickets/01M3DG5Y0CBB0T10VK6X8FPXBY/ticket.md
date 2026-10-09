+++
id = "01M3DG5Y0CBB0T10VK6X8FPXBY"
title = "S07: Sign in from inside the game"
type = "story"
category = "todo"
priority = "critical"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-09-26T00:00:00Z"
aliases = ["T-0012"]
labels = ["jira:SCRUM-28", "owner:stevendangkhoi", "game", "platform", "milestone:0.1.0"]

[[acceptance]]
text = "A player can sign in from the game client with the same credentials as the website."
bound = false

[[acceptance]]
text = "The game client presents a valid session to the game server when joining a match."
bound = false

[[acceptance]]
text = "A player who is not signed in can still build ships offline but cannot join ranked matches."
bound = false
+++

https://aliens-against-humanity.atlassian.net/browse/SCRUM-28

**Card**
As a player, I want to sign into my platform account from the game client, so that my matches count toward my rating and my skins appear on my ship.

**Conversation**
- Does the game show a username/password form, or open the website in a browser and receive a token (device-code style)?
- How does the game client store the token between launches?
