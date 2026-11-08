+++
id = "01M6WVRTC724W6RQYAA9WCEK5F"
title = "Client: projectile replication and interpolation; prediction-correction smoothing"
type = "task"
category = "todo"
priority = "medium"
reporter = "mcnairrobotics"
created = "2026-11-08T05:58:15Z"
updated = "2026-11-08T07:17:22Z"

[[acceptance]]
text = "Projectile state replicates to clients and renders interpolated, with a test"
bound = false

[[acceptance]]
text = "Prediction corrections are smoothed over several ticks, with a test"
bound = false
+++

Found while closing out the backlog. ClientReplica has no projectile state (server projectiles are never sent to clients), so a projectile interpolation test has nothing to exercise; S48-2 only covers ship poses. ClientReplica.LastCorrection measures prediction error but nothing smooths the correction, so there is no behavior to test. Needs a protocol message for projectile state and a smoothing design first; tests follow the design. Not a test-only gap.
