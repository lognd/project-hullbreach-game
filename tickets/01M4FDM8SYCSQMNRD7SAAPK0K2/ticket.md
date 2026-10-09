+++
id = "01M4FDM8SYCSQMNRD7SAAPK0K2"
title = "S54-3: Server-authoritative fragment hazard replication"
type = "task"
category = "todo"
priority = "medium"
points = 2
parent = "01M4FDM8C8GH3HM55Z3JDCRYWQ"
reporter = "lognd"
created = "2026-10-09T04:09:34Z"
updated = "2026-10-09T04:09:34Z"
labels = ["owner:mcnairrobotics", "game", "netcode", "milestone:0.3.0"]
scope = ["Assets/Scripts/Hullbreach.Net/**", "Assets/Tests/**", "docs/netcode.md", "docs/reference/hullbreach-net.md"]

[[links]]
kind = "blocked-by"
target = "01M4FDM8GHAEHJ25DWX6GJSJ8E"

[[acceptance]]
text = "Given a fragment on the server, when snapshots are sent, then clients receive its position, velocity and armed state, and hits are resolved only on the server"
bound = false

[[acceptance]]
text = "Given the live fragment cap, when a break would exceed it, then the oldest fragments despawn first"
bound = false
+++

Extends FragmentSpawned / snapshot state; documented in docs/reference/hullbreach-net.md.
