+++
id = "01M4GR2NA6VM4EM22MMXXKT7YH"
title = "ServerSimulation.Join trusts client ShipSnapshot: ignores Grid.TryAdd failures, accepts arbitrary variants/state and re-Join overwrite"
type = "security"
category = "done"
outcome = "duplicate"
priority = "high"
reporter = "lognd"
created = "2026-10-09T16:31:26Z"
updated = "2026-10-09T16:36:17Z"
labels = ["origin:auditor", "audit:ship"]
scope = ["Assets/Scripts/Hullbreach.Net/ServerSimulation.cs"]
+++

Symbol: ServerSimulation.Join (ServerSimulation.cs:87-124) consuming BlockGrid.TryAdd and ShipBody. (1) TryAdd result at line 102 is discarded: duplicate/invalid/overlapping cells are silently dropped and the ship the server simulates differs from what was requested, with no error to the peer. (2) Block count, core presence, and connectivity are unvalidated, and Mods/Damage come straight from the client, so a client can join with permanent variant bits (gravity gun, seeking thruster, max ramp upgrades from ThrusterUpgrades.Mask) that Ship intends to be powerup-timed (ShipBody.ApplyPowerup), or Damage=0 / unbounded block count. (3) Px/Py/Vx/Vy/Av are applied verbatim: a client picks spawn position and velocity. (4) Join for an existing peer id silently replaces its PeerState (and ushort cast of peer can alias NetIds). Fix: make Join return Result/bool with an error set (DuplicatePeer, InvalidDesign, TooManyBlocks, NoCore, Disconnected), validate against BlockPalette/limits, spawn at a server-chosen pose, whitelist allowed Mods for build-time, and reject on any TryAdd failure. Add negative tests.
