+++
id = "01M3DG5Y2SN7TH11BDYFCY4SVZ"
title = "S41-1: Arena bounds with soft push-back and no structural damage"
type = "task"
category = "todo"
priority = "medium"
points = 2
parent = "01M3DG5Y0Y1MZXATXQF051SPA2"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-10-09T04:14:47Z"
aliases = ["T-0089"]
labels = ["jira:SCRUM-177", "owner:GingerVHS", "game", "milestone:0.2.0"]
scope = ["Assets/Scripts/Hullbreach.World/ArenaBounds.cs*", "Assets/Scripts/Hullbreach.Ship/ShipBody.cs", "Assets/Scripts/Hullbreach.Net/ServerSimulation.cs", "Assets/Tests/EditMode/Hullbreach.World.Tests/ArenaBoundsTests.cs*", "Assets/Tests/EditMode/Hullbreach.Ship.Tests/ShipArenaTests.cs*", "Assets/Tests/EditMode/Hullbreach.Net.Tests/ServerArenaTests.cs*", "docs/reference/hullbreach-world.md", "docs/reference/hullbreach-ship.md", "docs/reference/hullbreach-net.md", "docs/architecture.md", "changelog.d/01M3DG5Y2SN7TH11BDYFCY4SVZ.changed.md"]

[[acceptance]]
text = "Given a ship whose center of mass is outside the arena radius and moving outward, when the shared ShipBody step runs for a few simulated seconds, then it is back inside the radius and no block has taken damage"
bound = true

[[acceptance]]
text = "Given a ship outside the arena radius on the authoritative server, when ServerSimulation ticks, then it is pushed back by the same ArenaBounds rule as the client ShipBody and the structural solver sees no extra load"
bound = true
+++

Arena bounds with soft push-back and no structural damage
Parent story: S41 Stay inside the arena
