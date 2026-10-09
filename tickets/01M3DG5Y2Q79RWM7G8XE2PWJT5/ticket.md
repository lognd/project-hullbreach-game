+++
id = "01M3DG5Y2Q79RWM7G8XE2PWJT5"
title = "S40-2: Projectiles follow gravity"
type = "task"
category = "todo"
priority = "high"
points = 1
parent = "01M3DG5Y0X02V7SGJ0Y3J7550F"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-10-09T04:11:04Z"
aliases = ["T-0087"]
labels = ["jira:SCRUM-162", "owner:GingerVHS", "game", "physics", "milestone:0.2.0"]
scope = ["Assets/Scripts/Hullbreach.Net/ServerSimulation.cs", "Assets/Tests/EditMode/**", "docs/**", "changelog.d/**"]

[[acceptance]]
text = "Given a planetoid beside a shot's path, when the authoritative server steps a fired projectile, then its path bends toward the planetoid by about half the pull times elapsed time squared and does not bend without the planetoid"
bound = false

[[acceptance]]
text = "Given the client projectile and the server projectile, when both integrate, then both add GravityField.AccelerationAt to the projectile velocity each step (client wiring needs Unity verification)"
bound = false
+++

Projectiles follow gravity
Parent story: S40 Fight inside a gravity field
