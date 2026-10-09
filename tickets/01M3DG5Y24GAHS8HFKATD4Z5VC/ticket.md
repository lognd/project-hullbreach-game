+++
id = "01M3DG5Y24GAHS8HFKATD4Z5VC"
title = "S34-2: Replicate BlockPlaced mid-match to the opponent"
type = "task"
category = "in-progress"
priority = "high"
points = 2
parent = "01M3DG5Y0QWT3XQ9HHJGV4H9H0"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-10-09T04:15:11Z"
aliases = ["T-0068"]
labels = ["jira:SCRUM-172", "owner:mcnairrobotics", "game", "milestone:0.2.0", "creates:Assets/Tests/EditMode/Hullbreach.Net.Tests/BuildReplicationTests.cs*"]
scope = ["Assets/Scripts/Hullbreach.Net/NetMessages.cs", "Assets/Scripts/Hullbreach.Net/ServerSimulation.cs", "Assets/Scripts/Hullbreach.Net/ServerHost.cs", "Assets/Scripts/Hullbreach.Net/Hullbreach.Net.asmdef", "Assets/Tests/EditMode/Hullbreach.Net.Tests/Hullbreach.Net.Tests.asmdef", "docs/netcode.md", "docs/reference/hullbreach-net.md", "docs/architecture.md", "design/hullbreach_game.strata", "Assets/Tests/EditMode/Hullbreach.Net.Tests/BuildReplicationTests.cs*"]

[[acceptance]]
text = "Given a joined peer and a BuildRequest that PlacementRules accepts, when the server handles it, then the block is added to that peer's authoritative grid and a BlockPlaced is broadcast to every peer, so the opponent's replica shows the new block"
bound = false

[[acceptance]]
text = "Given a BuildRequest the placement rules refuse, or one with an unknown block type, non-facing modifier bits, an unknown peer or a destroyed ship, when the server handles it, then no grid changes and no BlockPlaced is sent"
bound = false

[[acceptance]]
text = "Given a ServerHost over a transport, when a client sends a BuildRequest, then it edits only the sending peer's ship, and a truncated BuildRequest is dropped and logged without stopping the loop"
bound = false
+++

Replicate BlockPlaced mid-match to the opponent
Parent story: S34 Build during a fight
