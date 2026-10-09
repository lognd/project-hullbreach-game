+++
id = "01M4FDM836P7CBJQBPFN4NVFA2"
title = "S53-3: Countdown readout for failing clusters and the hull warning banner"
type = "task"
category = "todo"
priority = "medium"
points = 2
parent = "01M4FDM7MX49CFNWPN41HEBC3F"
reporter = "lognd"
created = "2026-10-09T04:09:34Z"
updated = "2026-10-09T04:09:34Z"
labels = ["owner:a-carten", "game", "milestone:0.3.0"]
scope = ["Assets/Scripts/Hullbreach.Hud/**", "Assets/Scripts/Hullbreach.Game/**", "docs/**"]

[[links]]
kind = "blocked-by"
target = "01M4FDM7SMC5PB0AW3T1RS9TFD"

[[acceptance]]
text = "Given one or more adjacent failing blocks, when the ship is drawn, then a countdown in seconds is shown next to the cluster"
bound = false

[[acceptance]]
text = "Given any failing block, when the HUD updates, then HullWarningBanner shows the shortest TimeToFailure"
bound = false

[[acceptance]]
text = "Given the countdown model, when edit-mode tests run, then clustering and shortest-time selection are covered without Unity"
bound = false
+++

Clustering and formatting live in plain C# (Hullbreach.Hud) so they are testable with tools/plaincs.
