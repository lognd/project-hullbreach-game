+++
id = "01M3DG5Y2PYFJ27TMZTE14413F"
title = "S40-1: GravityField, GravityBody, and OrbitHelper with the attractive-then-repulsive force law"
type = "task"
category = "todo"
priority = "high"
points = 3
parent = "01M3DG5Y0X02V7SGJ0Y3J7550F"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-10-09T04:18:44Z"
aliases = ["T-0086"]
labels = ["jira:SCRUM-161", "owner:GingerVHS", "game", "physics", "milestone:0.2.0"]
scope = ["Assets/Scripts/Hullbreach.World/**", "Assets/Tests/EditMode/Hullbreach.World.Tests/**"]

[[acceptance]]
text = "Given a GravityField, when acceleration is sampled, then it is attractive inverse-square outside the soft radius, softens to zero at the center, is continuous, and is clamped (GravityFieldTests)"
bound = true

[[acceptance]]
text = "Given a ship falling onto a planetoid, when ShipBody steps, then it curves toward it and ends outside the surface (ShipGravityTests, OrbitHelperTests; S40 criterion 1)"
bound = false
+++

GravityField, GravityBody, and OrbitHelper with the attractive-then-repulsive force law
Parent story: S40 Fight inside a gravity field
