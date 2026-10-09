+++
id = "01M3DG5Y0SP70ASKR1VP80X976"
title = "S36: Compute stress in every block"
type = "story"
category = "todo"
priority = "critical"
parent = "01M3DG5Y04X6WZ1G61CVD7TX9S"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-09-26T00:00:00Z"
aliases = ["T-0025"]
labels = ["jira:SCRUM-57", "owner:GingerVHS", "game", "physics", "milestone:0.2.0"]

[[acceptance]]
text = "Under thrust, blocks near the thruster show higher stress than blocks far from it."
bound = false

[[acceptance]]
text = "Adding a brace between two stressed blocks measurably lowers their stress."
bound = false

[[acceptance]]
text = "A 60-block ship maintains the agreed frame budget on the reference laptop."
bound = false
+++

https://aliens-against-humanity.atlassian.net/browse/SCRUM-57

**Card**
As a player, I want each block to carry real stress under thrust, gravity, and impacts, so that a poorly braced ship actually fails where it was built badly.

**Conversation**
- Each block as one Q8 element with shared nodes, assembled once per hull change; solve on a fixed sub-step. Is the linear elastic model enough, or do we need geometric nonlinearity for buckling?
- Material properties per block type: one stiffness for hull, another for armor?
- Performance budget: what block count on a mid-range laptop must stay above 60 fps?

Code already exists: Done, landed early. roadmap.md: the entire Hullbreach.Structure assembly (Q8Element/NodeLattice/StiffnessAssembly/CgSolver/LoadVector).
