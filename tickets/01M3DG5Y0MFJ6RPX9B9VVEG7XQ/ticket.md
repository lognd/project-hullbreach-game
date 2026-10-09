+++
id = "01M3DG5Y0MFJ6RPX9B9VVEG7XQ"
title = "S31: Remove and undo blocks"
type = "story"
category = "todo"
priority = "high"
parent = "01M3DG5Y03DJ72WRTEGD6EBC3C"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-09-26T00:00:00Z"
aliases = ["T-0020"]
labels = ["jira:SCRUM-52", "owner:stevendangkhoi", "game", "milestone:0.1.0"]

[[acceptance]]
text = "A player can remove any block except the core; blocks that would be left unattached are handled per the agreed rule."
bound = false

[[acceptance]]
text = "Undo reverses the last placement or removal, at least ten deep."
bound = false
+++

https://aliens-against-humanity.atlassian.net/browse/SCRUM-52

**Card**
As a player, I want to remove a block I placed and undo my last few actions, so that a misclick does not cost me the whole design.

**Conversation**
- Can removing a block leave others detached? If so, remove them too, or refuse?
- Undo depth?

Code already exists: Done. roadmap.md: PlacementRules.Detach + Hullbreach.Builder.UndoStack; tests in UndoStackTests.cs.
