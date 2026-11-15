+++
id = "01M5XACEYZRD4KJYBEJYTVQZRX"
title = "S29-3b: Unity connect flow consumes ConnectRequest (queue screen to scene load)"
type = "task"
category = "todo"
priority = "high"
parent = "01M3DG5Y0J805WP05DZV8GWAWA"
reporter = "mcnairrobotics"
created = "2026-10-26T23:57:57Z"
updated = "2026-11-15T13:17:22Z"
labels = ["game", "owner:mcnairrobotics"]

[[acceptance]]
text = "A found match becomes a resolvable connect target (ConnectRequest/ConnectTarget, tests)"
bound = true

[[acceptance]]
text = "HullbreachQueueConnect dials on Matched and loads the match scene (editor-verified against a local platform with two accounts; no automated test)"
bound = false
+++

found while working ~RHV8HRV. Needs the editor: queue screen calls ConnectRequest.FromHandoff on MatchmakingMachine.Handoff, dials Host:Port (name resolution in the transport) and loads the game scene. Engine-free part landed in ConnectRequest.cs.
