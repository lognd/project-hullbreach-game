+++
id = "01M4GR1TAGP8W7Q6HVDHSCMVEK"
title = "Damage changes block stiffness but never triggers K rebuild, so stress uses softened E against an un-softened K"
type = "bug"
category = "todo"
priority = "high"
reporter = "lognd"
created = "2026-10-09T16:30:58Z"
updated = "2026-10-09T17:00:41Z"
labels = ["origin:auditor", "audit:hullbreach-structure"]
scope = ["Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs"]

[[acceptance]]
text = "Given a block whose damage changes, when the solver ticks, then K is rebuilt and the solve restarts, with one shared softening floor"
bound = true
+++

Contract: StiffnessAssembly.Rebuild docstring (Fem/StiffnessAssembly.cs:35-37) says it rebuilds 'when topology is dirty or damage changed stiffness', and BlockTypes.EffectiveStiffness folds damage softening into E. Violation: StructuralSolver.Tick needsRebuild (StructuralSolver.cs:131) only checks _forceRebuild, grid.TopologyDirty and grid.Count. BlockGrid.TrySet (damage write by ShipStructure.cs:114 and ServerSimulation.cs:303) does not set TopologyDirty, so K is assembled with stale undamaged E while ComputeBlockStress (:244) multiplies strain by the CURRENT softened E. Result is a wrong-but-not-erroring hybrid: damaged blocks neither shed load (K unchanged) nor report consistent stress, defeating the 'damaged block is structurally weaker' contract. Also DamageModel.SofteningFactor (Failure/DamageModel.cs:499-503) duplicates the 0.05 floor in BlockTypes.EffectiveStiffness and is not used by it, so the two can drift. Fix: have Tick detect damage changes (e.g. a grid damage/stiffness version counter, or callers call MarkTopologyChanged after TrySet damage; prefer a counter in BlockGrid bumped by TrySet) and rebuild; make EffectiveStiffness call DamageModel.SofteningFactor (or vice versa) so there is one floor. Add a test: damage a block, Tick, assert its displacement/stress changes vs undamaged.
