+++
id = "01M4FE7F00VEC36B7PBFGNGKV2"
title = "Reliable event that overtakes its snapshot stalls until the next reliable message"
type = "bug"
category = "todo"
priority = "high"
reporter = "mcnairrobotics"
created = "2026-10-09T04:20:03Z"
updated = "2026-10-09T04:20:03Z"
labels = ["owner:mcnairrobotics", "game", "netcode"]
scope = ["Assets/Scripts/Hullbreach.Net/ClientReplica.cs"]
+++

found while working T-0110. ClientReplica.ApplySnapshot raises the baseline sequence and discards stale buffered events, but never drains the pending buffer, so a BlockPlaced/BlockDestroyed buffered at baseline+1 is stuck until another reliable message arrives. Reorder on a jittery link makes this reachable at join time. Repro: ThrottledConnectionTests.EventThatOvertakesItsSnapshot_IsStillApplied (currently [Ignore]d).
