+++
id = "01M3DG5Y2GB5TTGSV0X6FE2XGE"
title = "S38-2: BucklingAnalysis and GeometricStiffness for compressive failure"
type = "task"
category = "todo"
priority = "critical"
points = 5
parent = "01M3DG5Y0V6ZWSJZQW0H5VM80Z"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-10-09T04:14:28Z"
aliases = ["T-0080"]
labels = ["jira:SCRUM-159", "owner:GingerVHS", "game", "physics", "milestone:0.2.0"]
scope = ["Assets/Scripts/Hullbreach.Structure/Fem/**", "Assets/Tests/EditMode/Hullbreach.Structure.Tests/**"]

[[acceptance]]
text = "Given a column under compression, when BucklingAnalysis runs, then the critical load factor matches a dense oracle, scales with inverse length squared, and tension has no modes (BucklingTests)"
bound = false

[[acceptance]]
text = "Given the solver hook, when load is high, then buckled blocks are reported and the analysis is bit-deterministic (BucklingTests)"
bound = false
+++

BucklingAnalysis and GeometricStiffness for compressive failure
Parent story: S38 Break apart under load
