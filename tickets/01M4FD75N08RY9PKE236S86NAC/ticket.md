+++
id = "01M4FD75N08RY9PKE236S86NAC"
title = "Q8Element rigid-mode test via symmetric eigensolver"
type = "task"
category = "todo"
priority = "medium"
reporter = "mcnairrobotics"
created = "2026-10-09T04:02:25Z"
updated = "2026-10-09T04:02:25Z"
labels = ["owner:GingerVHS", "game", "physics"]
scope = ["Assets/Tests/EditMode/Hullbreach.Structure.Tests/Q8ElementTests.cs"]

[[acceptance]]
text = "Q8ElementTests asserts exactly three zero eigenvalues of the element stiffness using a symmetric eigensolver, replacing the two rigid-mode tests it supersedes"
bound = false
+++

Optional test hardening left as a TODO [C2] comment in Q8ElementTests.cs; DenseJacobiEigen already exists in Structure/Fem.
