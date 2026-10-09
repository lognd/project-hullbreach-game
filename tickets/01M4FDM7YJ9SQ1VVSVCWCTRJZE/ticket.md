+++
id = "01M4FDM7YJ9SQ1VVSVCWCTRJZE"
title = "S53-2: Pulsating green-to-red outline around blocks at risk of failure"
type = "task"
category = "todo"
priority = "medium"
points = 3
parent = "01M4FDM7MX49CFNWPN41HEBC3F"
reporter = "lognd"
created = "2026-10-09T04:09:33Z"
updated = "2026-10-09T04:09:33Z"
labels = ["owner:a-carten", "game", "milestone:0.3.0"]
scope = ["Assets/Scripts/Hullbreach.Game/ShipRenderer.cs", "Assets/Art/**", "docs/**"]

[[links]]
kind = "blocked-by"
target = "01M4FDM7SMC5PB0AW3T1RS9TFD"

[[acceptance]]
text = "Given a block above the warning ratio, when it is drawn in any overlay mode including None, then an outline around the block is visible and shifts from green toward red as the ratio approaches failure"
bound = false

[[acceptance]]
text = "Given a failing block, when its TimeToFailure shrinks, then the outline pulse rate increases"
bound = false

[[acceptance]]
text = "Given the colorblind palette setting (S37-2), when it is on, then the outline uses the colorblind-safe ramp"
bound = false

[[acceptance]]
text = "Given a block below the warning ratio, when it is drawn, then no outline is shown"
bound = false
+++

Replaces or complements ApplyCriticalFlash (body tint). Needs Unity editor verification.
