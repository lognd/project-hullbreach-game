+++
id = "01M3DG5Y2AQGV267Y3STQSWFF3"
title = "S36-3: Material properties per block type (hull, armor, thruster)"
type = "task"
category = "todo"
priority = "critical"
points = 2
parent = "01M3DG5Y0SP70ASKR1VP80X976"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-10-09T04:14:27Z"
aliases = ["T-0074"]
labels = ["jira:SCRUM-153", "owner:GingerVHS", "game", "physics", "milestone:0.2.0"]
scope = ["Assets/Scripts/Hullbreach.Core/Grid/BlockType.cs", "Assets/Tests/EditMode/Hullbreach.Core.Tests/**"]

[[acceptance]]
text = "Given the block type table, when materials are read, then hull is the yield reference, armor is heavier and more brittle, and effective stiffness falls monotonically with damage to a floor (BlockTypesTests)"
bound = false
+++

Material properties per block type (hull, armor, thruster)
Parent story: S36 Compute stress in every block
