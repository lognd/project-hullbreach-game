+++
id = "01M3DG5Y3FB5N2BVNEYDCTK8WC"
title = "S48-4: Transport behind an interface so the C# socket layer can be replaced"
type = "task"
category = "in-progress"
priority = "critical"
points = 2
parent = "01M3DG5Y157XVFXF2844EP0ZKH"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-10-09T04:22:53Z"
aliases = ["T-0111"]
labels = ["jira:SCRUM-167", "owner:mcnairrobotics", "game", "netcode", "milestone:0.2.0"]
scope = ["Assets/Scripts/Hullbreach.Net/ITransport.cs"]

[[acceptance]]
text = "Given the netcode assemblies, when a component other than a demo composition root sends or receives, then it does so only through ITransport (ServerHost, ClientReplica pumps, the server outbox forwarding), so another transport can replace LoopbackTransport without touching game code"
bound = false

[[acceptance]]
text = "Given the full server-to-clients pipeline run with the transport typed only as ITransport, when the edit-mode suite runs, then the end-to-end, host and throttled-link tests pass"
bound = false
+++

Transport behind an interface so the C# socket layer can be replaced
Parent story: S48 Keep the game fluid over the internet
