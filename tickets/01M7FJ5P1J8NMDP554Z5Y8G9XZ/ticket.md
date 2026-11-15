+++
id = "01M7FJ5P1J8NMDP554Z5Y8G9XZ"
title = "Server tests for mid-match placement refusals (Disconnected, WouldBuckle)"
type = "task"
category = "todo"
priority = "medium"
points = 2
parent = "01M3DG5Y0QWT3XQ9HHJGV4H9H0"
reporter = "lognd"
created = "2026-11-15T12:16:05Z"
updated = "2026-11-15T12:16:05Z"
labels = ["game"]

[[acceptance]]
text = "A mid-match PlaceBlock of a disconnected cell is refused with Disconnected and not distrusted"
bound = false

[[acceptance]]
text = "A mid-match PlaceBlock that would fail immediately under load is refused with WouldBuckle"
bound = false
+++
