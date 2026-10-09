+++
id = "01M4FE7F83RFR48TNQJCXH4A19"
title = "ShipState is sent only to the ship's owner, so opponents never get pose updates"
type = "bug"
category = "todo"
priority = "high"
reporter = "mcnairrobotics"
created = "2026-10-09T04:20:03Z"
updated = "2026-10-09T04:20:03Z"
labels = ["owner:mcnairrobotics", "game", "netcode"]
scope = ["Assets/Scripts/Hullbreach.Net/ServerSimulation.cs"]
+++

found while working T-0110. ServerSimulation.Tick calls EmitUnreliable(peer, BuildState(ship)) with the ship's own peer as destination, so each client only receives its own ship's poses; docs/netcode.md and S48 interpolation of the opponent (T-0109) assume every ship's state reaches every peer. ServerHostTests.Advance_ReturnsSnapshotsThenPoseUpdatesOverTheTransport documents the current behaviour.
