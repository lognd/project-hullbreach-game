+++
id = "01M4GR2HF8JVX7TF2PHJF5948N"
title = "ServerSimulation: fragment ids and peer NetIds share one ushort id space and collide"
type = "bug"
category = "done"
outcome = "fixed"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:31:22Z"
updated = "2026-10-09T17:09:25Z"
labels = ["origin:auditor", "audit:hullbreach-net"]
scope = ["Assets/Scripts/Hullbreach.Net/ServerSimulation.cs"]

[[acceptance]]
text = "Fragment ids never collide with peer NetIds; a split with two peers joined leaves both peer ships intact"
bound = true
+++

ServerSimulation.cs:112 sets NetId = (ushort)peer (peer is an int transport id, silently truncated; LoopbackTransport hands out 1,2,3...). ServerSimulation.cs:51/361 allocates fragment ids from _nextFragmentId starting at 1 (wraps to 0 after 65535). Both populate ClientReplica._ships keyed by ushort (ClientReplica.cs:120, 317). The first fragment (NewId=1) overwrites peer 1's replica ship (ClientReplica.cs:317) and BlockDestroyed/ShipState for that NetId then hit the wrong body. Contract violated: netId must uniquely identify one body (docs/netcode.md message table). Fix direction: allocate NetIds from one server-owned counter for both ships and fragments (skip live ids on wrap), map peer->NetId explicitly, and return an error/reject Join when peer is outside ushort or the id space is exhausted. Add an end-to-end test where a split happens with peers 1 and 2 joined.
