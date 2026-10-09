+++
id = "01M4GREG1Z463PSEZHTRFXDEYB"
title = "S56: Read my ship's damage, status and stress at a glance in every match"
type = "story"
category = "todo"
priority = "medium"
parent = "01M3DG5Y04X6WZ1G61CVD7TX9S"
reporter = "lognd"
created = "2026-10-09T16:37:54Z"
updated = "2026-10-09T16:37:54Z"
persona = "player"
capability = "to see damage, active powerups and continuous stress/buckling feedback on every block during normal play"
outcome_text = "I can react before my ship breaks without toggling a debug view"
labels = ["owner:a-carten", "game", "physics", "milestone:0.3.0"]

[[acceptance]]
text = "Block rendering is layered: cosmetic skin base, then damage, then status, then stress/buckling, then critical flash; no layer replaces another."
bound = false

[[acceptance]]
text = "Damage, status and stress feedback are on in every match and readable on every skin."
bound = false

[[acceptance]]
text = "The existing one-at-a-time overlay modes stay available as a developer toggle."
bound = false
+++

Builds on S37 (T-0026) and its S37-1 overlay code already in ShipRenderer.UpdateOverlay; S37-2 colorblind palette and S37-3 shared stress-to-color mapping apply to the always-on layers.
