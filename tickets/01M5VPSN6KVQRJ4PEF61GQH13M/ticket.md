+++
id = "01M5VPSN6KVQRJ4PEF61GQH13M"
title = "ClientReplica accepts older-server-tick poses as the newer interpolation sample"
type = "bug"
category = "in-progress"
priority = "medium"
reporter = "mcnairrobotics"
created = "2026-10-26T08:56:23Z"
updated = "2026-10-26T08:56:53Z"
labels = ["game", "creates:changelog.d/*.fixed.md"]
scope = ["Assets/Scripts/Hullbreach.Net/ClientReplica.cs", "Assets/Tests/EditMode/Hullbreach.Net.Tests/ClientInterpolationTests.cs", "docs/reference/hullbreach-net.md", "changelog.d/*.fixed.md"]

[[acceptance]]
text = "A pose with a server tick older than the newest is dropped, so Older.Tick <= Newer.Tick always holds and interpolation stays monotonic; ApplyGhostPose shares the guard; run_tests.sh passes"
bound = false
+++

Follow-up to PR #81. ApplyState/ApplyGhostPose unconditionally shift Newer to Older, so an out-of-order unreliable pose gives a negative span and snaps.
