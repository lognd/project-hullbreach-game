+++
id = "01M3DG5Y3G01P51X156ANTM71X"
title = "S49-1: Defender-favored hit resolution within a documented tolerance"
type = "task"
category = "done"
outcome = "fixed"
priority = "high"
points = 3
parent = "01M3DG5Y16WHWDFT1WZAXCWTG1"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-11-15T05:24:11Z"
aliases = ["T-0112"]
labels = ["jira:SCRUM-202", "owner:mcnairrobotics", "game", "netcode", "milestone:0.3.0"]

[[acceptance]]
text = "Given a client and server that disagree about a hit, when the shot grazes within the documented speed*grace tolerance of a block edge, then the server resolves it as a miss, while a centre hit still hits (HitToleranceTests)"
bound = true
+++

Defender-favored hit resolution within a documented tolerance
Parent story: S49 Favor the defender and forgive honest lag
Routing: game (hit-resolution tolerance: authoritative game-server logic)
