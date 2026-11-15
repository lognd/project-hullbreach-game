+++
id = "01M3DG5Y3HHB6S1Y0V50GMV7S1"
title = "S49-2: Per-client trust level that tightens tolerances instead of disconnecting"
type = "task"
category = "todo"
priority = "high"
points = 3
parent = "01M3DG5Y16WHWDFT1WZAXCWTG1"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-11-15T05:24:12Z"
aliases = ["T-0113"]
labels = ["jira:SCRUM-203", "owner:mcnairrobotics", "game", "netcode", "milestone:0.3.0"]

[[acceptance]]
text = "Given a peer that repeatedly makes implausible claims (refused builds, reserved input bits), when its trust drops, then the hit margin and late-input hold shrink and build claims are refused at the floor, and the peer is never disconnected for it (TrustLevelTests)"
bound = false
+++

Per-client trust level that tightens tolerances instead of disconnecting
Parent story: S49 Favor the defender and forgive honest lag
Routing: game (per-client trust tolerance: game-server logic)
