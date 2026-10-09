+++
id = "01M4GRDNTKY5Q9603W26Q4GPPH"
title = "S12-4: Cosmetic loadout model (per-block-type skins plus trail, banner and projectile-effect slots)"
type = "task"
category = "todo"
priority = "medium"
points = 2
parent = "01M3DG5Y0DCWYQ5H93E6DQMXG2"
reporter = "lognd"
created = "2026-10-09T16:37:27Z"
updated = "2026-10-09T16:37:27Z"
labels = ["owner:a-carten", "game", "milestone:0.3.0"]

[[acceptance]]
text = "A CosmeticLoadout value maps each block type id to an optional skin id and holds optional trail, banner and projectile-effect ids."
bound = false

[[acceptance]]
text = "An empty loadout renders exactly like today's default per-type colors."
bound = false

[[acceptance]]
text = "Unknown or out-of-range block type ids and skin ids are rejected with a Result, not an exception."
bound = false
+++

Pure data in a non-engine assembly so Net and tools/plaincs can use it. No gameplay field may read it.
