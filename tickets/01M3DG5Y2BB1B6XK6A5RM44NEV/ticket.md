+++
id = "01M3DG5Y2BB1B6XK6A5RM44NEV"
title = "S36-4: Solver benchmarks and the frame budget at 100 blocks on the reference laptop"
type = "task"
category = "todo"
priority = "critical"
points = 3
parent = "01M3DG5Y0SP70ASKR1VP80X976"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-10-09T04:06:48Z"
aliases = ["T-0075"]
labels = ["jira:SCRUM-154", "owner:GingerVHS", "game", "physics", "milestone:0.2.0"]
scope = ["Assets/Tests/EditMode/Hullbreach.Structure.Tests/SolverBenchmarks.cs", "docs/**", "changelog.d/**", "TODO.md"]

[[acceptance]]
text = "Given a 100-block ship under thrust, when the structural solver runs 20 Release ticks, then the median steady-state tick is within the documented 5 ms solver budget (25 percent of the 20 ms 50 Hz frame)"
bound = false

[[acceptance]]
text = "Given the budget is asserted by a test that runs in the default plain-C# suite, when the budget or its margin is documented, then docs/testing.md states the budget, why the median is asserted, and how the 500 and 2000 block cases and the buckling spike are treated"
bound = false
+++

Solver benchmarks and the frame budget at 100 blocks on the reference laptop
Parent story: S36 Compute stress in every block
