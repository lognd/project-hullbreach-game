+++
id = "01M500FAJ4Z1WG0BNMAGTFBF99"
title = "S57-1: Engine-free MinimapModel projecting ships, planets and arena bounds"
type = "task"
category = "todo"
priority = "medium"
points = 3
parent = "01M500EVPWWECD3A2C08TJ1K5Q"
reporter = "a-carten"
created = "2026-10-15T14:46:46Z"
updated = "2026-10-15T14:46:46Z"
labels = ["owner:a-carten", "game", "milestone:0.3.0", "creates:Assets/Scripts/Hullbreach.Hud/MinimapModel.cs", "creates:Assets/Scripts/Hullbreach.Hud/MinimapModel.cs.meta", "creates:Assets/Tests/EditMode/Hullbreach.Hud.Tests/MinimapModelTests.cs", "creates:Assets/Tests/EditMode/Hullbreach.Hud.Tests/MinimapModelTests.cs.meta"]
scope = ["Assets/Scripts/Hullbreach.Hud/HudColor.cs", "docs/design/ui-port.md", "Assets/Scripts/Hullbreach.Hud/MinimapModel.cs", "Assets/Scripts/Hullbreach.Hud/MinimapModel.cs.meta", "Assets/Tests/EditMode/Hullbreach.Hud.Tests/MinimapModelTests.cs", "Assets/Tests/EditMode/Hullbreach.Hud.Tests/MinimapModelTests.cs.meta"]

[[acceptance]]
text = "MinimapModel takes own ship (position, heading), enemy positions, planets (position, radius), optional arena bounds and a minimap size in px, and returns minimap-local blips; it references no UnityEngine type and compiles in tools/plaincs."
bound = false

[[acceptance]]
text = "Framing is arena-fixed with uniform scale: the arena bounds touch the minimap edge on the limiting axis and the arena centre maps to the minimap centre within 0.5 px; with no bounds configured the frame fits all ships and planets plus a 10 percent margin."
bound = false

[[acceptance]]
text = "Planet blip radius equals world radius times the scale, clamped to a 2 px minimum; own-ship blip heading equals ship heading within 0.5 degrees."
bound = false

[[acceptance]]
text = "A ship outside the arena bounds is clamped onto the minimap edge along the ray from the minimap centre and flagged OutOfBounds."
bound = false

[[acceptance]]
text = "Own, enemy, planet and bounds blips use distinct named HudColor constants (enemy colour distinct from own)."
bound = false

[[acceptance]]
text = "MinimapModelTests cover framing, scaling, clamping, heading and the no-bounds fallback and pass under tools/plaincs/run_tests.sh; ui-port.md module reference lists MinimapModel."
bound = false
+++

Pure geometry for the S57 minimap, kept engine-free like FlightTelemetryModel/StatusPanelModel so it is testable without Unity. Arena bounds are an input because S41 (arena boundary) is not implemented yet. S45-2 (T-0099) will later add hazard warning blips to this model's output.
