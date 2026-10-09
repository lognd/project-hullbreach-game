+++
id = "01M4GR1995RXB1T2641B31BDNS"
title = "BlockKey.Neighbors aliases across grid edges, giving false adjacency to Connectivity/Articulation"
type = "bug"
category = "todo"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:30:40Z"
updated = "2026-10-09T16:53:26Z"
labels = ["origin:auditor", "audit:hullbreach-core"]
scope = ["Assets/Scripts/Hullbreach.Core/Grid/BlockKey.cs"]

[[acceptance]]
text = "Given a cell on the grid edge, when Neighbors is computed, then off-grid neighbors are -1 and no far-edge cell is treated as adjacent"
bound = true
+++

Contract: Neighbors (BlockKey.cs:250-257) is documented as the four 4-connected neighbors, but it calls Pack without range checks. At y==127, Pack(x,128): by=256 ORs into the x byte, yielding the key of (x+1,-128), a real cell on the far edge; at x==127 +x gives bx=256 (unpacks to x=-128); at y==-128, -y gives by=-1 -> key -1 (also Articulation's NoParent sentinel, Articulation.cs:42). Connectivity.ReachableFromCore/SplitIntoComponents/Articulation.Compute (Topology/*.cs) use it unfiltered, so blocks at (x,127) and (x+1,-128) are wrongly treated as connected (core attached when it should be debris). Pack itself is also unchecked (l.232). Fix: have Neighbors write -1 (or skip) for out-of-range neighbors and make callers skip negatives (Facing.Ahead already returns -1 this way), or add a TryNeighbor; guard Pack with a Debug assert for InRange; add edge tests in Assets/Tests/EditMode/Hullbreach.Core.Tests/BlockKeyTests.cs and ConnectivityTests.cs.
