+++
id = "01M7FJ5PEX6RNRC5ZPR648EQ3D"
title = "Forward server TrustEvents to POST /api/v1/trust-events"
type = "task"
category = "todo"
priority = "high"
points = 3
parent = "01M3DG5Y16WHWDFT1WZAXCWTG1"
reporter = "lognd"
created = "2026-11-15T12:16:05Z"
updated = "2026-11-15T12:16:05Z"
labels = ["game"]

[[acceptance]]
text = "Events from ServerSimulation.TryDequeueTrustEvent are posted to the platform with the server key, best effort and retried, for authenticated peers only"
bound = false

[[acceptance]]
text = "A test with a fake HTTP seam shows the request body matches the platform TrustEventIn contract"
bound = false
+++
