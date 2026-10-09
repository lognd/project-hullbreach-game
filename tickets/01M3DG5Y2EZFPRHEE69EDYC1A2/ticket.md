+++
id = "01M3DG5Y2EZFPRHEE69EDYC1A2"
title = "S37-3: Document the stress-to-color mapping shared by build mode and combat"
type = "docs"
category = "in-progress"
priority = "critical"
points = 1
parent = "01M3DG5Y0TRQYC578FES1WY409"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-10-09T04:09:20Z"
aliases = ["T-0078"]
labels = ["jira:SCRUM-157", "owner:GingerVHS", "game", "physics", "milestone:0.2.0"]
scope = ["docs/stress-colors.md", "docs/README.md", "docs/architecture.md", "changelog.d/**"]

[[acceptance]]
text = "Given the stress overlay and the hull warning banner, when docs/stress-colors.md is read, then it states the ratio each uses, the ratio-to-color function, the band thresholds and where each lives in code"
bound = false

[[acceptance]]
text = "Given build mode and fly mode, when docs/stress-colors.md is read, then it states that both go through the same ShipRenderer tint function and what differs between them"
bound = false
+++

Document the stress-to-color mapping shared by build mode and combat
Parent story: S37 See where my ship is about to fail
