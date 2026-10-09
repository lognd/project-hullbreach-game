+++
id = "01M4GR2M447TPZABZ0RS11BHM5"
title = "ServerSimulation never resolves ship-ship contact; authoritative server and Game diverge"
type = "bug"
category = "todo"
priority = "high"
reporter = "lognd"
created = "2026-10-09T16:31:25Z"
updated = "2026-10-09T17:05:35Z"
labels = ["origin:auditor", "audit:ship"]
scope = ["Assets/Scripts/Hullbreach.Net/ServerSimulation.cs"]
+++

Symbol: ServerSimulation.Tick (ServerSimulation.cs:140-176) vs ShipContacts.Resolve. docs/reference/hullbreach-ship.md#shipcontacts says contact is 'resolved in plain C# by the same authority that integrates the ships', and Game calls it from ShipContactsRunner.cs:39, but the headless server (the actual authority, S47) never calls ShipContacts.Resolve. Ships therefore pass through each other on the server and no contact damage/impulses are broadcast, while any local Game sim shows collisions. Fix: after all ships Step in Tick, call ShipContacts.Resolve for each peer pair in sorted peer order (deterministic), broadcast resulting BlockDamaged events, and add an integration test with two real ServerSimulation ships on a collision course.
