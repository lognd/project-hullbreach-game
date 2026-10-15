+++
id = "01M5033TEY427K4B3TRCXZEJE8"
title = "S47-5: Load MultiplayerGame in direct mode and on a dedicated server"
type = "task"
category = "todo"
priority = "high"
parent = "01M3DG5Y14KAMJ8NTGHC8BW9YV"
reporter = "mcnairrobotics"
created = "2026-10-15T15:32:55Z"
updated = "2026-10-15T16:50:00Z"
labels = ["owner:mcnairrobotics", "game", "netcode", "milestone:0.3.0"]
scope = ["Assets/Scripts/Hullbreach.NetCode.Entities/**"]

[[acceptance]]
text = "A direct host started with -hullbreachDirect loads MultiplayerGame and admits players without reordering the build scene list."
bound = true

[[acceptance]]
text = "A direct client started with -hullbreachConnect loads MultiplayerGame and reaches Replicated ships: 2 against a direct host without the build-order workaround."
bound = true

[[acceptance]]
text = "A Linux dedicated server build loads MultiplayerGame and admits direct clients without the build-order workaround."
bound = false
+++

Found while writing the beginner README (PR #52). On main nothing loads MultiplayerGame when the game runs with -hullbreachDirect/-hullbreachConnect/-hullbreachPort or as a dedicated server: only the lobby's START GAME (HullbreachLobbyService) loads it. HullbreachNetCodeServerSystem only puts peers in game once the HullbreachGhostPrefab singleton from the MultiplayerGame subscene exists, so a build that opens on LobbyScene never admits direct peers. README Guides 3 and 5 currently tell people to build with MultiplayerGame as the first scene; remove that workaround from the README when this lands.
