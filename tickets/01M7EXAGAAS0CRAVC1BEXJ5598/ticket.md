+++
id = "01M7EXAGAAS0CRAVC1BEXJ5598"
title = "S41: shrinking storm zone (headless simulation: StormZone schedule, damage, HUD queries)"
type = "story"
category = "done"
outcome = "fixed"
priority = "medium"
reporter = "lognd"
created = "2026-11-15T06:11:42Z"
updated = "2026-11-15T12:11:19Z"

[[acceptance]]
text = "The storm schedule shrinks the safe radius on a fixed timetable and damages blocks outside it (StormZone, headless tests)"
bound = true

[[acceptance]]
text = "The server builds the storm at match start, applies it each tick and tells both peers (StormStarted), covered by StormWiringTests"
bound = true

[[acceptance]]
text = "HUD and minimap queries expose the storm state to the player (MinimapAndStormHudTests)"
bound = true
+++
