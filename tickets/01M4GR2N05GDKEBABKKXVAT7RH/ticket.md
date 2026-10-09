+++
id = "01M4GR2N05GDKEBABKKXVAT7RH"
title = "ServerSimulation.Leave/TimeoutStalePeers remove a ship without telling clients; remote replicas keep ghost ships"
type = "bug"
category = "todo"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:31:25Z"
updated = "2026-10-09T17:10:17Z"
labels = ["origin:auditor", "audit:hullbreach-net"]
scope = ["Assets/Scripts/Hullbreach.Net/ServerSimulation.cs", "Assets/Scripts/Hullbreach.Net/NetMessages.cs"]

[[acceptance]]
text = "A ship that leaves or times out is removed from every replica via ShipRemoved"
bound = true
+++

ServerSimulation.cs:126 (Leave) and :188 (timeout) just _peers.Remove(peer). There is no ShipRemoved/PeerLeft MessageKind (NetMessages.cs:14-25) and no broadcast, so every ClientReplica keeps the departed ship in _ships forever (ClientReplica.cs:75), still rendered (Hullbreach.Game/NetDemo.cs RefreshVisuals), and its NetId is later reused by a new peer or fragment. Also, a timed-out peer's transport connection is not closed, and Leave does not clear its DrainShots/projectile ownership. Contract (S47 criterion 2: ship removed on timeout) is only met server-side. Fix direction: add a reliable sequenced ShipRemoved(seq, netId) message (update docs/netcode.md table and wire tests), broadcast it from Leave and timeout, handle it in ClientReplica.ApplyOne by removing the ship and enqueuing a ReplicaEvent, and surface the dropped peer list to the host so it can disconnect the transport.
