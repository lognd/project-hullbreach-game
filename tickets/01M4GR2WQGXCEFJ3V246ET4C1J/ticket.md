+++
id = "01M4GR2WQGXCEFJ3V246ET4C1J"
title = "Bounds-check wire decoding and cap remote-sized allocations"
type = "security"
category = "todo"
priority = "high"
reporter = "lognd"
created = "2026-10-09T16:31:34Z"
updated = "2026-10-09T16:31:34Z"
labels = ["origin:auditor", "security"]
scope = ["Assets/Scripts/Hullbreach.Net/Wire.cs", "Assets/Scripts/Hullbreach.Net/NetMessages.cs", "Assets/Scripts/Hullbreach.Net/ClientReplica.cs"]

[[acceptance]]
text = "ByteReader rejects reads past the received length, ApplyReceived honors length and rejects unknown kinds, ShipSnapshot.Read caps count; scripts/check_unity_tree.sh reports no INV-001 policy failure"
bound = false
+++

origin: auditor. Invariant INV-001; policy rules POL-net-no-remote-sized-alloc and POL-net-reader-bounds-checked (scripts/check_unity_tree.sh check 8). Findings: ByteReader (Wire.cs) has no bounds check; ClientReplica.ApplyReceived ignores length and treats any unknown kind byte as reliable; ShipSnapshot.Read allocates new SnapshotBlock[count] from remote u16. Fix direction: Result-returning TryRead with remaining check, validate length/kind up front, cap block count to the grid limit. Leave frob:invariant INV-001 anchors at the sites.
