+++
id = "01M500FR8NDVVBRAY9ZD2EDHC4"
title = "S57-4: Edge-of-screen enemy arrow HUD view and prefab in the single-player scene"
type = "task"
category = "todo"
priority = "medium"
points = 2
parent = "01M500EVPWWECD3A2C08TJ1K5Q"
reporter = "a-carten"
created = "2026-10-15T14:47:00Z"
updated = "2026-10-15T14:47:00Z"
labels = ["owner:a-carten", "game", "milestone:0.3.0", "creates:Assets/Scripts/Hullbreach.Game/Hud/EnemyIndicatorView.cs", "creates:Assets/Scripts/Hullbreach.Game/Hud/EnemyIndicatorView.cs.meta"]
scope = ["Assets/Editor/Hullbreach.Editor/HudPrefabBuilder.cs", "Assets/Scripts/Hullbreach.Game/DemoMode.cs", "Assets/Scenes/DemoScene.unity", "docs/design/ui-port.md", "Assets/Scripts/Hullbreach.Game/Hud/EnemyIndicatorView.cs", "Assets/Scripts/Hullbreach.Game/Hud/EnemyIndicatorView.cs.meta"]

[[links]]
kind = "blocked-by"
target = "01M500FAKKW8CVR00D6BN0C07B"

[[acceptance]]
text = "HudPrefabBuilder generates an EnemyIndicator prefab (arrow image plus optional distance label) driven by EnemyIndicatorView."
bound = false

[[acceptance]]
text = "In DemoScene, with the enemy off-screen the arrow sits on the screen edge inset and points within 2 degrees of the bearing to the nearest enemy; with the enemy on screen the arrow is hidden and an on-screen marker is shown over the enemy."
bound = false

[[acceptance]]
text = "With no living enemy the indicator is fully hidden; the indicator never overlaps the minimap rect from S57-3."
bound = false

[[acceptance]]
text = "EnemyIndicatorView contains no bearing or clamping maths beyond applying EnemyIndicatorModel output; ui-port.md documents the view and prefab."
bound = false
+++

Unity side of S57-2. Uses the main camera's orthographic size and aspect to build the viewport half-extents passed to the model.
