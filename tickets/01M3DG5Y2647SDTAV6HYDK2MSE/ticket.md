+++
id = "01M3DG5Y2647SDTAV6HYDK2MSE"
title = "S35-1: BlockGrid design serialization and validation on load"
type = "task"
category = "in-progress"
priority = "medium"
points = 2
parent = "01M3DG5Y0R3XBGSCEYQ0383EDK"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-10-09T04:08:21Z"
aliases = ["T-0070"]
labels = ["jira:SCRUM-174", "owner:stevendangkhoi", "game", "platform", "milestone:0.2.0", "creates:Assets/Tests/EditMode/Hullbreach.Builder.Tests/ShipDesign*", "creates:docs/ship-design-format.md"]
scope = ["Assets/Scripts/Hullbreach.Builder/**", "docs/reference/hullbreach-builder.md", "Assets/Tests/EditMode/Hullbreach.Builder.Tests/ShipDesign*", "docs/ship-design-format.md"]

[[acceptance]]
text = "Given a BlockGrid built through BuilderSession, when it is written to the versioned design format and read back, then the loaded grid has the same blocks (key, type, modifiers) and the same core"
bound = false

[[acceptance]]
text = "Given a design file whose blocks break PlacementRules (floating block, overlapping clearance, second core, out-of-range cell, unknown type, damaged block), when it is loaded, then every problem is reported with its cell and reason and the blocks are not altered"
bound = false

[[acceptance]]
text = "Given a corrupt, truncated or newer-version design file, when it is loaded, then load returns an error result naming the line and never throws"
bound = false
+++

BlockGrid design serialization and validation on load
Parent story: S35 Save and load ship designs
Routing: game (BlockGrid serialization/validation: Unity client code)
