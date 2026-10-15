+++
id = "01M500FR763WZNFTPXE6T58VK7"
title = "S57-3: Minimap HUD view and prefab in the single-player scene"
type = "task"
category = "todo"
priority = "medium"
points = 3
parent = "01M500EVPWWECD3A2C08TJ1K5Q"
reporter = "a-carten"
created = "2026-10-15T14:47:00Z"
updated = "2026-10-15T14:47:00Z"
labels = ["owner:a-carten", "game", "milestone:0.3.0", "creates:Assets/Scripts/Hullbreach.Game/Hud/MinimapView.cs", "creates:Assets/Scripts/Hullbreach.Game/Hud/MinimapView.cs.meta"]
scope = ["Assets/Editor/Hullbreach.Editor/HudPrefabBuilder.cs", "Assets/Scripts/Hullbreach.Game/DemoMode.cs", "Assets/Scripts/Hullbreach.Game/GravityWorld.cs", "Assets/Scenes/DemoScene.unity", "docs/design/ui-port.md", "Assets/Scripts/Hullbreach.Game/Hud/MinimapView.cs", "Assets/Scripts/Hullbreach.Game/Hud/MinimapView.cs.meta"]

[[links]]
kind = "blocked-by"
target = "01M500FAJ4Z1WG0BNMAGTFBF99"

[[acceptance]]
text = "HudPrefabBuilder generates a Minimap prefab (uGUI) with a MinimapView anchored to a screen corner that does not overlap the StatusPanel or HullWarningBanner at 1280x720 and 1920x1080."
bound = false

[[acceptance]]
text = "In DemoScene the minimap shows the own ship (with heading), the enemy ship(s), every GravityWorld planet and the arena bounds when configured, all positioned from MinimapModel output and refreshed every frame."
bound = false

[[acceptance]]
text = "A destroyed enemy's blip disappears within one frame; planets and bounds do not flicker or reallocate per frame (blips are pooled)."
bound = false

[[acceptance]]
text = "MinimapView contains no projection maths beyond copying MinimapModel output to RectTransforms; ui-port.md documents the view and prefab layout."
bound = false
+++

Unity side of S57-1. GravityWorld may need a read-only accessor for its planets; enemy ships come from the same source WorldSink.TryNearestEnemy uses.
