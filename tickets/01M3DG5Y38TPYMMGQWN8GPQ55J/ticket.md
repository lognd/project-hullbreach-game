+++
id = "01M3DG5Y38TPYMMGQWN8GPQ55J"
title = "S47-1: Headless server loop ticking ShipBody and StructuralSolver without the Unity scene"
type = "task"
category = "in-progress"
priority = "critical"
points = 5
parent = "01M3DG5Y14KAMJ8NTGHC8BW9YV"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-10-09T04:14:41Z"
aliases = ["T-0104"]
labels = ["jira:SCRUM-126", "owner:mcnairrobotics", "game", "netcode", "milestone:0.1.0", "creates:Assets/Scripts/Hullbreach.Net/ServerHost.cs*", "creates:Assets/Scripts/Hullbreach.Net/NetLog.cs*", "creates:Assets/Tests/EditMode/Hullbreach.Net.Tests/ServerHostTests.cs*"]
scope = ["docs/netcode.md", "docs/reference/hullbreach-net.md", "docs/roadmap.md", "Assets/Scripts/Hullbreach.Net/ServerHost.cs*", "Assets/Scripts/Hullbreach.Net/NetLog.cs*", "Assets/Tests/EditMode/Hullbreach.Net.Tests/ServerHostTests.cs*", "Assets/Scripts/Hullbreach.Net/NetMessages.cs"]

[[acceptance]]
text = "Given a ServerHost over an ITransport with two peers joined and no Unity runtime, when it is advanced by N seconds of host time, then it runs floor(N * TickRate) fixed ticks, each stepping every peer's ShipBody and StructuralSolver"
bound = false

[[acceptance]]
text = "Given two clients sending InputMessage over the transport, when the host ticks, then the server applies each peer's latest input and returns the authoritative ShipSnapshot and ShipState traffic to the clients over the same transport"
bound = false

[[acceptance]]
text = "Given a long stall or a payload that is truncated or of unknown kind, when the host runs, then catch-up ticks are bounded and the payload is dropped and logged without stopping the loop"
bound = false
+++

Headless server loop ticking ShipBody and StructuralSolver without the Unity scene
Parent story: S47 Run matches on an authoritative server
