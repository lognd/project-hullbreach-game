+++
id = "01M4GR14N86R205ZMEZE85M949"
title = "BlockGrid.TryAdd/TrySet crash and corrupt state on unvalidated TypeId from network"
type = "security"
category = "todo"
priority = "high"
reporter = "lognd"
created = "2026-10-09T16:30:36Z"
updated = "2026-10-09T16:30:36Z"
labels = ["origin:auditor", "audit:hullbreach-core"]
scope = ["Assets/Scripts/Hullbreach.Core/Grid/BlockGrid.cs"]
+++

Contract: TryAdd/TrySet return bool and never throw (BlockGrid.cs:113,174), but BlockTypes.Get(byte) is Table[typeId] (BlockType.cs ~Get) and the table has 7 entries. Net feeds wire bytes straight in: ClientReplica.cs:102,301 and ServerSimulation.cs:102 (new Block(b.TypeId,...)). TypeId>=7 -> IndexOutOfRangeException at BlockGrid.cs:130 AFTER _blocks.Add (l.125) and _structureVersion++ (l.126): block present with no mass accounted, CoreKey possibly set, exception escapes the net handler (remote DoS on client/server). Fix: validate block.TypeId < BlockTypes.Count at top of TryAdd and TrySet and return false (before any mutation); add BlockTypes.IsValid(byte); add Core test with TypeId=255 asserting false and grid unchanged. Callers in Net should then treat false as a protocol error.
