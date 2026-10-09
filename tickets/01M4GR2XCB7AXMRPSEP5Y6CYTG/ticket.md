+++
id = "01M4GR2XCB7AXMRPSEP5Y6CYTG"
title = "ServerSimulation.Join accepts an unvalidated client-supplied ShipSnapshot and an existing peer id"
type = "security"
category = "done"
outcome = "duplicate"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:31:34Z"
updated = "2026-10-09T16:36:18Z"
labels = ["origin:auditor", "audit:hullbreach-net"]
scope = ["Assets/Scripts/Hullbreach.Net/ServerSimulation.cs"]
+++

ServerSimulation.cs:87-122: Join builds an authoritative ShipBody directly from initialDesign. Nothing validates TypeId against BlockTypes, Mods/Damage (a design can arrive pre-damaged or with free powerup modifiers), presence of exactly one Core, connectivity, block count/budget, or the initial Px/Py/Vx/Vy/Av (a client can spawn at speed or inside another ship). Duplicate keys are silently dropped by TryAdd (line 102). Joining a peer id already in _peers silently replaces its ship (line 117) without a Leave notification, and the signature returns void so rejection is impossible. Contract violated: server-authoritative state must not trust the join payload (docs/netcode.md). Fix direction: change Join to return a Result/enum (Accepted, DuplicatePeer, InvalidDesign(reason)) and validate against Core block catalog and platform-approved design limits before inserting; zero velocity and clamp spawn to server-chosen position; reject empty or coreless grids (ticks skip empty grids at line 164/173 so a coreless ship never emits state).
