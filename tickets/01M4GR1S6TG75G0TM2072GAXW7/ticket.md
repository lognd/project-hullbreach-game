+++
id = "01M4GR1S6TG75G0TM2072GAXW7"
title = "Authoritative ServerSimulation never configures LoadScale/MaterialStiffnessScale, so server stress and buckling disagree with client"
type = "bug"
category = "done"
outcome = "fixed"
priority = "high"
reporter = "lognd"
created = "2026-10-09T16:30:57Z"
updated = "2026-10-09T17:12:11Z"
labels = ["origin:auditor", "audit:hullbreach-structure"]
scope = ["Assets/Scripts/Hullbreach.Net/ServerSimulation.cs"]

[[acceptance]]
text = "The server solver uses the shared calibrated LoadScale and MaterialStiffnessScale"
bound = true
+++

Contract: docs/reference/hullbreach-structure.md (LoadScale, MaterialStiffnessScale) says gameplay forces must be scaled into normalized material units (calibrated 0.06 / 40) or ships disintegrate. Violation: Assets/Scripts/Hullbreach.Net/ServerSimulation.cs:114 constructs 'new StructuralSolver()' with defaults LoadScale=1, MaterialStiffnessScale=1 and never sets them; only Hullbreach.Game/ShipStructure.cs:68-69 does. The authoritative server (which alone decides detachment, ServerSimulation.cs:278-311) therefore sees ~16x higher stress than the client tint and a 40x lower buckling load factor, so ships break on the server at thrust the client shows as 0.3 of yield. Fix: set Solver.LoadScale = ShipStructure.DefaultLoadScale and MaterialStiffnessScale = ShipStructure.DefaultMaterialStiffnessScale (better: move the defaults into Hullbreach.Structure as shared constants since Net cannot reference Game, and make StructuralSolver default to them). Add a Net integration test ticking a demo ship at full thrust with a real StructuralSolver and asserting no block fails.
