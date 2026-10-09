+++
id = "01M4GTPTANABV9JDBKR0C72YDC"
title = "BlockStress.BucklingRatio drops to 0 on ticks between buckling passes"
type = "bug"
category = "todo"
priority = "medium"
reporter = "GingerVHS"
created = "2026-10-09T17:17:24Z"
updated = "2026-10-09T17:17:24Z"
labels = ["owner:GingerVHS"]
scope = ["Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs"]
+++

found while fixing ~99K2699: StructuralSolver.ComputeBlockStress rebuilds BlockStresses every Tick with BucklingRatio = 0, and RunBuckling only recomputes ratios on ticks where it publishes (every BucklingEveryNTicks and converged), so the Stress/Buckling overlay ratio reads 0 on the ticks in between although published modes still stand. Fix: re-apply RecomputeBucklingRatios after ComputeBlockStress whenever modes are held. Add a test asserting BucklingRatio stays non-zero across consecutive ticks.
