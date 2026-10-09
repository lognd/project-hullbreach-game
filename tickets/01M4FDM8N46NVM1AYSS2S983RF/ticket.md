+++
id = "01M4FDM8N46NVM1AYSS2S983RF"
title = "S54-2: No-instant-death safety rules and tests for fragment hazards"
type = "task"
category = "todo"
priority = "medium"
points = 3
parent = "01M4FDM8C8GH3HM55Z3JDCRYWQ"
reporter = "lognd"
created = "2026-10-09T04:09:34Z"
updated = "2026-10-09T04:09:34Z"
labels = ["owner:GingerVHS", "game", "physics", "milestone:0.3.0"]
scope = ["Assets/Scripts/Hullbreach.World/**", "Assets/Scripts/Hullbreach.Net/ServerSimulation.cs", "Assets/Tests/**", "docs/**"]

[[links]]
kind = "blocked-by"
target = "01M4FDM8GHAEHJ25DWX6GJSJ8E"

[[acceptance]]
text = "Given a fragment that spawns overlapping or touching any ship, when it would arm, then arming is deferred until it is clear of every ship"
bound = false

[[acceptance]]
text = "Given a fragment and its parent ship, when the parent grace window is active, then the fragment cannot damage the parent"
bound = false

[[acceptance]]
text = "Given a fragment detaching from a fast-moving ship, when the ship decelerates into it during the grace window, then no damage is dealt"
bound = false

[[acceptance]]
text = "Given many fragments contacting one ship in the same tick, when damage is applied, then total fragment damage per ship per tick is capped and no single fragment hit can destroy a core from full health"
bound = false

[[acceptance]]
text = "Given each edge case listed on S54, when edit-mode tests run, then each has a named test"
bound = false
+++

Edge-case list lives on the S54 story body.
