+++
id = "01M6WVK2JM5FA72DD850NKNZP7"
title = "Server stats: detach-cascade blocks not counted in BlocksDestroyed"
type = "bug"
category = "done"
outcome = "fixed"
priority = "medium"
reporter = "mcnairrobotics"
created = "2026-11-08T05:55:06Z"
updated = "2026-11-08T05:58:00Z"

[[acceptance]]
text = "Blocks stranded by a projectile kill are credited to the shooter's BlocksDestroyed"
bound = true
+++

ServerSimulation.ApplyProjectileImpact tallied only the directly destroyed block; stranded blocks became a fragment uncounted.
