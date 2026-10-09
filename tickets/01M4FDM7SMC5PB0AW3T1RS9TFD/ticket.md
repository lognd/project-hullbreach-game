+++
id = "01M4FDM7SMC5PB0AW3T1RS9TFD"
title = "S53-1: Time-to-failure fuse for load-driven yield and buckling"
type = "task"
category = "todo"
priority = "medium"
points = 3
parent = "01M4FDM7MX49CFNWPN41HEBC3F"
reporter = "lognd"
created = "2026-10-09T04:09:33Z"
updated = "2026-10-09T04:09:33Z"
labels = ["owner:GingerVHS", "game", "physics", "milestone:0.3.0"]
scope = ["Assets/Scripts/Hullbreach.Structure/**", "Assets/Scripts/Hullbreach.Net/ServerSimulation.cs", "Assets/Tests/**", "docs/**"]

[[acceptance]]
text = "Given a block whose ductile ratio exceeds 1.0 from sustained load, when the solver ticks, then the block enters a failing state with a finite TimeToFailure in seconds and detaches only when that reaches zero"
bound = false

[[acceptance]]
text = "Given a failing block whose load drops below the recover ratio, when the solver ticks, then its countdown stops and the failing state clears"
bound = false

[[acceptance]]
text = "Given a block buckled by sustained compression, when the solver ticks, then it detaches only after the documented buckling grace period"
bound = false

[[acceptance]]
text = "Given a weapon impact, when damage is applied, then the existing immediate behaviour is unchanged"
bound = false

[[acceptance]]
text = "Given the same inputs on server and client, when both run, then TimeToFailure is identical (deterministic) and the server is authoritative"
bound = false
+++

Expose per-block TimeToFailure (or none) on StructuralSolver; tunables documented in docs/reference/hullbreach-structure.md#damagemodel.
