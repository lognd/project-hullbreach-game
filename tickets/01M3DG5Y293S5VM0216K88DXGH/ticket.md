+++
id = "01M3DG5Y293S5VM0216K88DXGH"
title = "S36-2: Conjugate-gradient solver with preconditioning and a per-tick iteration budget"
type = "task"
category = "done"
outcome = "done"
priority = "critical"
points = 5
parent = "01M3DG5Y0SP70ASKR1VP80X976"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-10-09T04:20:18Z"
aliases = ["T-0073"]
labels = ["jira:SCRUM-152", "owner:GingerVHS", "game", "physics", "milestone:0.2.0"]
scope = ["Assets/Scripts/Hullbreach.Structure/Fem/**", "Assets/Tests/EditMode/Hullbreach.Structure.Tests/**"]

[[acceptance]]
text = "Given a steady load, when CG runs under a per-tick iteration budget, then it continues across ticks to the same displacement as one unbudgeted solve and restarts on a changed load or topology (CgContinuationTests)"
bound = true

[[acceptance]]
text = "Given the coarse preconditioner, when applied on a slender arm, then it is symmetric, annihilates rigid modes and cuts iterations (CoarsePreconditionerTests)"
bound = true
+++

Conjugate-gradient solver with preconditioning and a per-tick iteration budget
Parent story: S36 Compute stress in every block
