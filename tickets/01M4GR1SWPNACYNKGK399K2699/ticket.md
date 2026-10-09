+++
id = "01M4GR1SWPNACYNKGK399K2699"
title = "StructuralSolver.BuckledBlocks/BucklingModes stay stale after topology rebuild, forcing a K rebuild every tick on the server"
type = "bug"
category = "todo"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:30:58Z"
updated = "2026-10-09T17:00:06Z"
labels = ["origin:auditor", "audit:hullbreach-structure"]
scope = ["Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs"]

[[acceptance]]
text = "Given a buckled block that is removed, when the solver ticks, then BuckledBlocks and mode participation list only live blocks"
bound = true
+++

Contract: BuckledBlocks is documented as the current set of blocks that fold (StructuralSolver.cs:317-328). Violation: Tick() rebuilds K at :131-158 but never clears _bucklingModes/_buckledBlocks/CriticalLoadFactor; RunBuckling only refreshes every BucklingEveryNTicks (:343) and only when Converged (:198). After a detach, the old list keeps naming already-removed keys (and a non-converged solve keeps it indefinitely). Consumer effect: Hullbreach.Net/ServerSimulation.cs:310-311 sees Count>0 each tick, calls DestroyAndDetach, whose unconditional MarkTopologyChanged (:327) forces a full stiffness+coarse rebuild and CG restart every tick until the next buckling pass; in Game/ShipStructure.cs:138-147 stale keys re-seed _buckledFor timers. Fix: on needsRebuild (and when the solve is not Converged) drop modes whose keys are absent from the grid or clear _bucklingModes/_buckledBlocks/ratios and reset CriticalLoadFactor to +inf, so BuckledBlocks only ever lists live blocks from the current topology. Add a test: detach a buckled block, assert BuckledBlocks contains no absent keys on the next Tick.
