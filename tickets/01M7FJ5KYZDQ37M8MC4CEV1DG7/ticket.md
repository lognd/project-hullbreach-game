+++
id = "01M7FJ5KYZDQ37M8MC4CEV1DG7"
title = "Auto-rejoin after a dropped connection (offline and online)"
type = "task"
category = "todo"
priority = "medium"
points = 5
reporter = "lognd"
created = "2026-11-15T12:16:02Z"
updated = "2026-11-16T01:37:47Z"
labels = ["game"]

[[acceptance]]
text = "After a connection loss the client retries and rejoins the same match within a timeout without returning to the lobby"
bound = false

[[acceptance]]
text = "A rejoin test covers the server keeping the ship for a grace period"
bound = true
+++
