+++
id = "01M3DG5Y25YGZ3DV7JSDB4BNMF"
title = "S34-3: Build cooldown or cost so mid-match building is a trade-off"
type = "task"
category = "in-progress"
priority = "high"
points = 2
parent = "01M3DG5Y0QWT3XQ9HHJGV4H9H0"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-10-09T04:15:03Z"
aliases = ["T-0069"]
labels = ["jira:SCRUM-173", "owner:stevendangkhoi", "game", "milestone:0.2.0", "creates:Assets/Tests/EditMode/Hullbreach.Builder.Tests/BuildBudgetTests.cs*"]
scope = ["Assets/Scripts/Hullbreach.Builder/BuildBudget.cs", "Assets/Scripts/Hullbreach.Builder/BuilderSession.cs", "Assets/Scripts/Hullbreach.Builder/BlockPalette.cs", "docs/reference/hullbreach-builder.md", "Assets/Tests/EditMode/Hullbreach.Builder.Tests/BuildBudgetTests.cs*"]

[[acceptance]]
text = "Given a BuilderSession with a mid-match BuildBudget, when a block is placed, then its BlockPalette cost is charged, a cooldown starts, and further placements are refused (with the reason exposed) until credits and cooldown allow"
bound = true

[[acceptance]]
text = "Given a refused placement (rules or budget), when it is refused, then the grid and the credits are unchanged"
bound = true

[[acceptance]]
text = "Given two BuildBudget instances fed identical charges and ticks, when compared, then credits and cooldown are identical, with no engine or clock dependency so a server can enforce it"
bound = true

[[acceptance]]
text = "Given the shipped BuildTuning.MidMatch, when read, then the starting credits, cap, refill rate and cooldown are documented tunables"
bound = true
+++

Build cooldown or cost so mid-match building is a trade-off
Parent story: S34 Build during a fight
