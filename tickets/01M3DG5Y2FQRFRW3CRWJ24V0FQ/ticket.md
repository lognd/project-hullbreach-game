+++
id = "01M3DG5Y2FQRFRW3CRWJ24V0FQ"
title = "S38-1: StressCriteria and DamageModel detaching a block past its failure threshold"
type = "task"
category = "todo"
priority = "critical"
points = 3
parent = "01M3DG5Y0V6ZWSJZQW0H5VM80Z"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-10-09T04:17:59Z"
aliases = ["T-0079"]
labels = ["jira:SCRUM-158", "owner:GingerVHS", "game", "physics", "milestone:0.2.0"]
scope = ["Assets/Scripts/Hullbreach.Structure/Failure/**", "Assets/Tests/EditMode/Hullbreach.Structure.Tests/**"]

[[acceptance]]
text = "Given a stress tensor, when StressCriteria reduces it, then ductile (von Mises) and brittle (principal) ratios are correct and compression is asymmetric for armor (StressCriteriaTests, DamageModelTests)"
bound = true

[[acceptance]]
text = "Given a block past its failure ratio, when DamageModel decides, then it detaches with hysteresis, and a brittle failure detaches immediately (DamageModelTests; S38 criterion 1)"
bound = true
+++

StressCriteria and DamageModel detaching a block past its failure threshold
Parent story: S38 Break apart under load
