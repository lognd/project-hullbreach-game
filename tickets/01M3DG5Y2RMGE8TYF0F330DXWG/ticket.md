+++
id = "01M3DG5Y2RMGE8TYF0F330DXWG"
title = "S40-3: Gravity constants loaded from configuration"
type = "task"
category = "todo"
priority = "high"
points = 1
parent = "01M3DG5Y0X02V7SGJ0Y3J7550F"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-10-09T04:03:54Z"
aliases = ["T-0088"]
labels = ["jira:SCRUM-163", "owner:GingerVHS", "game", "physics", "milestone:0.2.0"]
scope = ["Assets/Scripts/Hullbreach.World/GravityConfig.cs*", "Assets/Scripts/Hullbreach.World/GravityField.cs", "Assets/Scripts/Hullbreach.Net/ServerSimulation.cs", "Assets/Scripts/Hullbreach.Game/GravityWorld.cs", "Assets/Tests/EditMode/**", "docs/**", "changelog.d/**"]

[[acceptance]]
text = "Given a gravity configuration text that sets MaxAcceleration, SurfaceRestitution and planets, when the shared GravityConfig parses it and builds a GravityField, then the field clamps, bounces and pulls with those values and no code change"
bound = false

[[acceptance]]
text = "Given no configuration, when a GravityField is built from the default GravityConfig, then its constants equal the previous hard-coded values (MaxAcceleration 40, surface restitution 0.2)"
bound = false

[[acceptance]]
text = "Given a malformed configuration text, when it is parsed, then parsing fails with a line-numbered error and no field is built"
bound = false

[[acceptance]]
text = "Given the same configuration, when ServerSimulation and the client GravityWorld build their fields, then both come from GravityConfig.BuildField and agree on every acceleration"
bound = false
+++

Gravity constants loaded from configuration
Parent story: S40 Fight inside a gravity field
