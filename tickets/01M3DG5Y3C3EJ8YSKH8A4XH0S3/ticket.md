+++
id = "01M3DG5Y3C3EJ8YSKH8A4XH0S3"
title = "S48-1: Client-side prediction with server reconciliation for the local ship"
type = "task"
category = "done"
outcome = "fixed"
priority = "critical"
points = 5
parent = "01M3DG5Y157XVFXF2844EP0ZKH"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-10-26T08:30:01Z"
aliases = ["T-0108"]
labels = ["jira:SCRUM-164", "owner:mcnairrobotics", "game", "netcode", "milestone:0.2.0"]

[[acceptance]]
text = "Local inputs move the predicted ship immediately, before any server reply"
bound = true

[[acceptance]]
text = "ShipState carries the server-acked input tick (19-byte wire, round-trips)"
bound = true

[[acceptance]]
text = "Reconcile drops acked inputs, replays the rest and corrects divergence"
bound = true

[[acceptance]]
text = "On a throttled lossy link the predicted pose tracks the server far closer than the lagging replica"
bound = true
+++

Client-side prediction with server reconciliation for the local ship
Parent story: S48 Keep the game fluid over the internet
