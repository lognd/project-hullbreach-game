+++
id = "01M7EQX7V98QT1Y5ETTY4NM8BP"
title = "Fix main CI: POL-net-no-remote-sized-alloc false positive and tracked .grimble state"
type = "bug"
category = "in-progress"
priority = "high"
points = 1
reporter = "lognd"
created = "2026-11-15T04:37:05Z"
updated = "2026-11-15T04:38:25Z"
scope = ["scripts/check_unity_tree.sh", "Assets/Scripts/Hullbreach.Net/NetMessages.cs", ".gitignore"]

[[acceptance]]
text = "check_unity_tree.sh passes and no .grimble state is tracked"
bound = true
+++

Base CI red since 2026-11-08: INV-001 grep matches the bounded 'new ProjectileEntry[count]' in ProjectileState.Read, and frob check refuses tracked .grimble/precision-history.json and telemetry.jsonl. Blocks every frob land.
