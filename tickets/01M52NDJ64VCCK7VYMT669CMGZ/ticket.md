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
updated = "2026-10-16T15:31:17Z"
labels = ["owner:mcnairrobotics", "game", "netcode", "milestone:0.3.0"]
scope = ["Assets/Editor/Build/**", "docs/build.md"]

[[acceptance]]
text = "HullbreachBuild.BuildFromCommandLine builds the Windows client from -executeMethod with LobbyScene first and MultiplayerGame in the scene list, and exits non-zero on failure."
bound = false

[[acceptance]]
text = "The same entry point builds a dedicated server for an installed Dedicated Server module and explains which Hub module is missing otherwise."
bound = false

[[acceptance]]
text = "play build wraps it so nobody opens Build Profiles."
bound = false
+++

Build scripts for the beginner demo flow: one editor entry point callable headless (Unity -batchmode -executeMethod) that builds the client and the dedicated server with the right scene list, falling back to Mono when IL2CPP is not installed, and writes build-info.json (commit, kind, development flag, scenes) that the doctor reads to judge freshness.
