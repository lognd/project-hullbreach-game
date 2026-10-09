+++
id = "01M4GREGMV7QP3H8V47P0C165M"
title = "S56-1: Layered block compositing in ShipRenderer (skin, damage, status, stress, critical flash)"
type = "task"
category = "todo"
priority = "medium"
points = 3
parent = "01M4GREG1Z463PSEZHTRFXDEYB"
reporter = "lognd"
created = "2026-10-09T16:37:54Z"
updated = "2026-10-09T16:37:54Z"
labels = ["owner:a-carten", "game", "milestone:0.3.0"]

[[acceptance]]
text = "UpdateOverlay's exclusive switch is replaced by ordered layers; the dev overlay toggle still isolates one channel."
bound = false

[[acceptance]]
text = "PlayMode test: a damaged, powered-up, highly stressed block shows all three signals at once."
bound = false
+++
