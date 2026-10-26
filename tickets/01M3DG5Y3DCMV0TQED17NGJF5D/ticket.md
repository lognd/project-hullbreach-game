+++
id = "01M3DG5Y3DCMV0TQED17NGJF5D"
title = "S48-2: Interpolation for the opponent ship and projectiles"
type = "task"
category = "todo"
priority = "critical"
points = 3
parent = "01M3DG5Y157XVFXF2844EP0ZKH"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-10-26T08:23:04Z"
aliases = ["T-0109"]
labels = ["jira:SCRUM-165", "owner:mcnairrobotics", "game", "netcode", "milestone:0.2.0"]

[[acceptance]]
text = "ClientReplica.TryInterpolateAt blends the last two server poses by client tick, clamps without extrapolating, and takes the shortest rotation arc"
bound = true

[[acceptance]]
text = "On the 100 ms / 2 percent loss throttled link the interpolated view has a smaller largest frame-to-frame jump than raw snapping"
bound = true

[[acceptance]]
text = "NetDemo draws replica ships through TryInterpolateAt"
bound = true
+++

Interpolation for the opponent ship and projectiles
Parent story: S48 Keep the game fluid over the internet
