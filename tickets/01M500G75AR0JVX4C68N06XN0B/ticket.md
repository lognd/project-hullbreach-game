+++
id = "01M500G75AR0JVX4C68N06XN0B"
title = "S57-5: Feed replicated ships and gravity wells to the minimap and enemy arrow in MultiplayerGame"
type = "task"
category = "todo"
priority = "medium"
points = 3
parent = "01M500EVPWWECD3A2C08TJ1K5Q"
reporter = "a-carten"
created = "2026-10-15T14:47:15Z"
updated = "2026-10-15T14:47:15Z"
labels = ["owner:mcnairrobotics", "game", "netcode", "milestone:0.3.0", "creates:Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachNetworkRadarFeed.cs", "creates:Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachNetworkRadarFeed.cs.meta"]
scope = ["Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachNetworkGameHud.cs", "Assets/Scripts/Hullbreach.Net/ClientReplica.cs", "Assets/Scenes/MultiplayerGame.unity", "docs/design/ui-port.md", "Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachNetworkRadarFeed.cs", "Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachNetworkRadarFeed.cs.meta"]

[[links]]
kind = "blocked-by"
target = "01M500FR763WZNFTPXE6T58VK7"

[[links]]
kind = "blocked-by"
target = "01M500FR8NDVVBRAY9ZD2EDHC4"

[[acceptance]]
text = "In MultiplayerGame each client's minimap marks its own ship (the ship owned by HullbreachNetCodeClient.LocalNetworkId) as own and every other replicated ship as enemy."
bound = false

[[acceptance]]
text = "Gravity wells replicated via GravityWellSpawned appear as planet blips on the client minimap; ClientReplica exposes them read-only if it does not already."
bound = false

[[acceptance]]
text = "The enemy arrow on each client points within 2 degrees of the bearing to the nearest other player's ship and switches to the on-screen marker when that ship is in view."
bound = false

[[acceptance]]
text = "Before the local ship is replicated, or with no opponent connected, the minimap shows only what is known and the enemy indicator is hidden, with no exceptions logged."
bound = false

[[acceptance]]
text = "No per-frame managed allocations in the feed (pooled lists); ui-port.md documents the multiplayer data source."
bound = false
+++

Netcode data plumbing so the S57-3/S57-4 views work in the multiplayer scene: the enemy is the other player's ship from ClientReplica, not a local AI target. Reuses MinimapModel and EnemyIndicatorModel unchanged.
