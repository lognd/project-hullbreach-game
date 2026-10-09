+++
id = "01M4FDM8YJJ9QNXQWZE1QZZC7T"
title = "S54-4: Telegraph fragment arming on screen and on the radar"
type = "task"
category = "todo"
priority = "medium"
points = 2
parent = "01M4FDM8C8GH3HM55Z3JDCRYWQ"
reporter = "lognd"
created = "2026-10-09T04:09:34Z"
updated = "2026-10-09T04:09:34Z"
labels = ["owner:a-carten", "game", "milestone:0.3.0"]
scope = ["Assets/Scripts/Hullbreach.Game/**", "Assets/Scripts/Hullbreach.Hud/**", "Assets/Art/**", "docs/**"]

[[links]]
kind = "blocked-by"
target = "01M4FDM8GHAEHJ25DWX6GJSJ8E"

[[acceptance]]
text = "Given an unarmed fragment, when drawn, then it is visibly inert (dimmed) and flashes during the last second before arming"
bound = false

[[acceptance]]
text = "Given an armed fragment, when drawn, then it uses the hazard look shared with asteroids and appears on the radar (S45-2) when that exists"
bound = false
+++

Needs Unity editor verification.
