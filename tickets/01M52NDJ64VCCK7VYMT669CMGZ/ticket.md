+++
id = "01M52NDJ64VCCK7VYMT669CMGZ"
title = "S47-6: Headless build entry point for the client and dedicated server"
type = "task"
category = "todo"
priority = "high"
points = 3
parent = "01M3DG5Y14KAMJ8NTGHC8BW9YV"
reporter = "mcnairrobotics"
created = "2026-10-16T15:31:17Z"
updated = "2026-10-16T15:31:41Z"
labels = ["owner:mcnairrobotics", "game", "netcode", "milestone:0.3.0"]
scope = ["Assets/Editor/Hullbreach.Editor/HullbreachBuild.cs", "docs/reference/hullbreach-editor.md"]

[[acceptance]]
text = "HullbreachBuild.BuildFromCommandLine builds the Windows client headless from -executeMethod with LobbyScene first and MultiplayerGame in the scene list, falling back to Mono when IL2CPP is missing."
bound = false

[[acceptance]]
text = "Asked for a dedicated server without the Dedicated Server module, it names the missing Unity Hub module and exits non-zero."
bound = false

[[acceptance]]
text = "With the Dedicated Server module installed it builds a server player that opens MultiplayerGame."
bound = false

[[acceptance]]
text = "play build wraps the entry point so a beginner never opens Build Profiles."
bound = false
+++

Build scripts for the beginner demo flow: one editor entry point callable headless (Unity -batchmode -executeMethod Hullbreach.Editor.HullbreachBuild.BuildFromCommandLine) that builds the client and the dedicated server with the right scene list, falls back to Mono when IL2CPP is not installed, and writes hullbreach-build.json (commit, kind, development flag, backend, scenes) that the doctor reads to judge freshness.
