+++
id = "01M3DG5Y2YG0KDP12PR69RS4TM"
title = "S43-1: GravityGunBehaviour and AntiGravityGunBehaviour placing temporary gravity wells"
type = "task"
category = "done"
outcome = "done"
priority = "high"
points = 3
parent = "01M3DG5Y107GDBD1ZZQK0H4AKX"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-10-09T04:20:20Z"
aliases = ["T-0094"]
labels = ["jira:SCRUM-179", "owner:GingerVHS", "game", "physics", "milestone:0.2.0"]
scope = ["Assets/Scripts/Hullbreach.Ship/Behaviours/**", "Assets/Scripts/Hullbreach.Net/ServerSimulation.cs", "Assets/Tests/EditMode/**"]

[[acceptance]]
text = "Given a gravity-gun cannon, when it fires, then the shot carries a positive well spec and an anti-gravity gun a negative one (BlockBehaviourTests)"
bound = true

[[acceptance]]
text = "Given a gravity or anti-gravity shot that lands on a ship, when the server steps, then one attractive or repulsive well is planted in the shared field and peers are told (ServerGravityWellTests; S43 criteria 1 and 2)"
bound = true
+++

GravityGunBehaviour and AntiGravityGunBehaviour placing temporary gravity wells
Parent story: S43 Bend the field with a Gravity Gun and an Anti-Gravity Gun
