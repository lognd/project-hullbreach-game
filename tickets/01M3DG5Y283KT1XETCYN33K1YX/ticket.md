+++
id = "01M3DG5Y283KT1XETCYN33K1YX"
title = "S36-1: Finite-element core: Q8Element, NodeLattice, StiffnessAssembly, LoadVector"
type = "task"
category = "done"
outcome = "done"
priority = "critical"
points = 8
parent = "01M3DG5Y0SP70ASKR1VP80X976"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-10-09T04:20:18Z"
aliases = ["T-0072"]
labels = ["jira:SCRUM-151", "owner:GingerVHS", "game", "physics", "milestone:0.2.0"]
scope = ["Assets/Scripts/Hullbreach.Structure/Fem/**", "Assets/Tests/EditMode/Hullbreach.Structure.Tests/**"]

[[acceptance]]
text = "Given Q8 elements, the node lattice and the assembled stiffness, when unit stiffness is built, then it is symmetric, positive semi-definite and annihilates rigid-body modes, and adjacent blocks share nodes (Q8ElementTests, NodeLatticeTests, FemTests)"
bound = true

[[acceptance]]
text = "Given an arm loaded at its tip, when the FE core solves, then blocks near the load are more stressed than the free end and a brace row lowers the peak (StressDistributionTests, S36 criteria 1 and 2)"
bound = true
+++

Finite-element core: Q8Element, NodeLattice, StiffnessAssembly, LoadVector
Parent story: S36 Compute stress in every block
