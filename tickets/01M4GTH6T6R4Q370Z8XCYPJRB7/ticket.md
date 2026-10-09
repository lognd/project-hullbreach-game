+++
id = "01M4GTH6T6R4Q370Z8XCYPJRB7"
title = "NetCode server system ignores timed-out peers (ghost and connection stay)"
type = "bug"
category = "todo"
priority = "medium"
reporter = "mcnairrobotics"
created = "2026-10-09T17:14:20Z"
updated = "2026-10-09T17:14:20Z"
labels = ["owner:mcnairrobotics"]
scope = ["Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachNetCodeServerSystem.cs"]

[[acceptance]]
text = "Timed-out peers are drained from ServerSimulation, their ghost destroyed and connection closed"
bound = false
+++

found while fixing ShipRemoved. HullbreachNetCodeServerSystem only calls Leave on a NetworkId disconnect; a peer dropped by input timeout (ServerSimulation.TryDequeueDroppedPeer) keeps its ghost entity and connection. Drain TryDequeueDroppedPeer each frame, destroy the ghost and disconnect the connection. Unity-only code, not runnable in the plain-C# harness.
