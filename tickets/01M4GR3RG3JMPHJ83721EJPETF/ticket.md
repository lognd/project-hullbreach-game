+++
id = "01M4GR3RG3JMPHJ83721EJPETF"
title = "ShipStructure.FixedUpdate ignores ShipController.SimulationEnabled; blocks take damage and detach while in Build mode"
type = "bug"
category = "done"
outcome = "fixed"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:32:02Z"
updated = "2026-10-09T17:14:49Z"
idempotency_key = "audit-game-sim-gate"
labels = ["origin:auditor", "audit:hullbreach-game"]
scope = ["Assets/Scripts/Hullbreach.Game/ShipStructure.cs"]

[[acceptance]]
text = "Given a paused ship in Build mode under stale high load, when FixedUpdate runs, then no block takes damage or detaches"
bound = false
+++

ShipStructure.cs:79-130 (FixedUpdate). Contract (ShipController.cs:352-355, DemoMode.ApplyState:259-260): SimulationEnabled=false means the ship is frozen in Build mode and nothing integrates. ShipStructure only checks controller/Ship null and grid.Count, so it keeps calling Solver.Tick with the stale ship.AppliedForcesThisStep from the last un-paused step, accumulating DamageModel.Accumulate damage, detaching blocks and running the Authoritative buckling-hold timers while the player is merely building. Fix: early-return (and clear _buckledFor/_failing hold timers) when !controller.SimulationEnabled, or clear AppliedForcesThisStep when pausing; add a play-mode test that holds a high-load ship in Build for several seconds and asserts no block loses health.
