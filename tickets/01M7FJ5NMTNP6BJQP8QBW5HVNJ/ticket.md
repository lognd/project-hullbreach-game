+++
id = "01M7FJ5NMTNP6BJQP8QBW5HVNJ"
title = "Opponent cannot shoot during the build-phase grace period after READY"
type = "bug"
category = "done"
outcome = "fixed"
priority = "medium"
points = 2
reporter = "lognd"
created = "2026-11-15T12:16:04Z"
updated = "2026-11-15T12:54:35Z"
labels = ["game"]

[[acceptance]]
text = "A ship that pressed READY cannot be damaged until the other side is ready or a short grace period ends, bots included"
bound = true

[[acceptance]]
text = "A server test covers the grace period"
bound = true
+++
