+++
id = "01M4FE7ESKCXE1C2NBSFS0APDE"
title = "Fragment ids collide with peer net ids: a second detach overwrites a player's replica ship"
type = "bug"
category = "todo"
priority = "high"
reporter = "mcnairrobotics"
created = "2026-10-09T04:20:03Z"
updated = "2026-10-09T04:20:03Z"
labels = ["owner:mcnairrobotics", "game", "netcode"]
scope = ["Assets/Scripts/Hullbreach.Net/ServerSimulation.cs"]
+++

found while working T-0110. ServerSimulation._nextFragmentId starts at 1, but peer NetIds are transport peer ids (1, 2, ...). ClientReplica.ApplyFragmentSpawned does _ships[NewId] = new body, so the second FragmentSpawned (NewId == a player's id) replaces that player's replica ship with an empty fragment body. Repro: ThrottledConnectionTests.FragmentIds_DoNotCollideWithPeerIds (currently [Ignore]d). Fix: allocate fragment ids from a range disjoint from peer ids.
