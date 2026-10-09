+++
id = "01M3DG5Y3EWDFMPEXFWR9YHWQY"
title = "S48-3: Throttled-connection test harness at 100 ms round trip and 2 percent loss"
type = "task"
category = "done"
outcome = "done"
priority = "critical"
points = 3
parent = "01M3DG5Y157XVFXF2844EP0ZKH"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-10-09T04:22:39Z"
aliases = ["T-0110"]
labels = ["jira:SCRUM-166", "owner:mcnairrobotics", "game", "netcode", "milestone:0.2.0", "creates:Assets/Tests/EditMode/Hullbreach.Net.Tests/NetRig.cs*", "creates:Assets/Tests/EditMode/Hullbreach.Net.Tests/ThrottledConnectionTests.cs*"]
scope = ["Assets/Tests/EditMode/Hullbreach.Net.Tests/ServerHostTests.cs", "Assets/Tests/EditMode/Hullbreach.Net.Tests/BuildReplicationTests.cs", "docs/testing.md", "docs/netcode.md", "Assets/Tests/EditMode/Hullbreach.Net.Tests/NetRig.cs*", "Assets/Tests/EditMode/Hullbreach.Net.Tests/ThrottledConnectionTests.cs*"]

[[acceptance]]
text = "Given a LoopbackTransport configured by the throttled profile, when pings are echoed and unreliable messages are sent in bulk, then the measured round trip is 100 ms within one tick and the unreliable loss rate is about 2 percent"
bound = true

[[acceptance]]
text = "Given reliable build, damage and detach events under the throttled link, when the link settles, then every replica's block grid for every ship equals the server's"
bound = true

[[acceptance]]
text = "Given a server and two clients on the throttled link flying a scripted course, when it runs for ten seconds, then each client's own-ship pose always matches a recent server pose within a bounded number of ticks and never jumps by more than a bounded multiple of one tick's motion"
bound = true
+++

Throttled-connection test harness at 100 ms round trip and 2 percent loss
Parent story: S48 Keep the game fluid over the internet
