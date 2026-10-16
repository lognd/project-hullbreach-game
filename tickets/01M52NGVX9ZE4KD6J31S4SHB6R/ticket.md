+++
id = "01M52NGVX9ZE4KD6J31S4SHB6R"
title = "S47-7: Doctor and play scripts for the online, guest and platform demos"
type = "task"
category = "done"
outcome = "done"
priority = "high"
points = 8
parent = "01M3DG5Y14KAMJ8NTGHC8BW9YV"
reporter = "mcnairrobotics"
created = "2026-10-16T15:33:06Z"
updated = "2026-10-16T15:36:18Z"
labels = ["owner:mcnairrobotics", "game", "netcode", "milestone:0.3.0"]
scope = ["tools/hbtools/**", "doctor.cmd", "doctor.sh", "play.cmd", "play.sh", "docs/doctor.md", "README.md", "Assets/Scripts/Hullbreach.Net/OnlineSmoke.cs", "Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachLobbyService.cs"]

[[acceptance]]
text = "The doctor prints PASS, WARN or FAIL for every check with numbered fix steps and a README link, and after a blocking failure it still lists the checks that wait on it."
bound = true

[[acceptance]]
text = "doctor --demo online, guest or platform checks only what that demo needs and ends with the exact commands to run next."
bound = true

[[acceptance]]
text = "The platform checks tell a game server key mismatch apart from a bad token and never print or store the key, password or token."
bound = true

[[acceptance]]
text = "doctor --collect zips the logs and doctor output with keys, passwords and tokens redacted."
bound = true

[[acceptance]]
text = "play online, host, join, server, two-local, build and logs start the right player, take secrets only from the environment or a prompt, and report whether each player reached the match."
bound = true

[[acceptance]]
text = "A headless online smoke run signs in to Unity Services, creates a private Relay lobby and leaves it, and reports pass or fail from the lobby status."
bound = true
+++

So that someone who has never programmed can run the demos: one Python implementation (tools/hbtools, run through uv) behind tiny doctor.cmd/doctor.sh and play.cmd/play.sh wrappers that offer to install uv. The doctor walks every prerequisite top to bottom; play starts players headless or windowed and greps their logs for the match proof lines.
