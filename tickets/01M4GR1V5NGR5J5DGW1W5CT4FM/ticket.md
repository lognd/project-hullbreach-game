+++
id = "01M4GR1V5NGR5J5DGW1W5CT4FM"
title = "LoadVector.AddPointForce silently drops or partially loses force for points outside the grid and allocates per call"
type = "bug"
category = "done"
outcome = "fixed"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:30:59Z"
updated = "2026-10-09T17:03:02Z"
labels = ["origin:auditor", "audit:hullbreach-structure"]
scope = ["Assets/Scripts/Hullbreach.Structure/Fem/LoadVector.cs"]

[[acceptance]]
text = "Given a point force on an absent block or non-finite point, when AddPointForce runs, then force is conserved over present nodes or rejected with false"
bound = true
+++

Contract: AddPointForce (Fem/LoadVector.cs:34-57) says it scatters a force over the containing element by shape-function weight, with no failure path. Violation: nodes absent from _assembly.NodeMap are skipped with 'continue' (:53), so a force applied at a ship-local point on a destroyed/absent block (ShipBody.AppliedForcesThisStep can hold such points after a detach until rebuilt) or outside the hull loses all or part of its magnitude (weights over the present nodes sum to <1), and inertia relief then balances only the remaining load, so the loss is invisible. NaN/huge points cast via (int)math.floor are undefined. Also allocates two arrays per call (:45,:48), contradicting StructuralSolver's 'never allocates in hot path' comment (StructuralSolver.cs:57-58). Fix: return bool (or typani Result with an OutsideStructure error) and have StructuralSolver.Tick log/skip or snap to the nearest present element and renormalize weights; reject non-finite points; reuse cached scratch arrays. Add a test with a point on an absent block asserting the documented outcome.
