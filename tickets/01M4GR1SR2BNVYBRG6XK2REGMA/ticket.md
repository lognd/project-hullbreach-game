+++
id = "01M4GR1SR2BNVYBRG6XK2REGMA"
title = "ChannelBarValue clamp lets NaN through; Build accepts non-finite floats without contract"
type = "bug"
category = "todo"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:30:57Z"
updated = "2026-10-09T16:30:57Z"
labels = ["origin:auditor"]
scope = ["Assets/Scripts/Hullbreach.Hud/FlightTelemetryModel.cs"]
+++

FlightTelemetryModel.cs:93 and :100: 'value < 0 ? 0 : value > 1 ? 1 : value' returns NaN unchanged (both comparisons false), so Fill is NaN and Label reads 'NaN%'; the doc promises Fill is clamped 0..1 / -1..1. ChannelBar.cs (Game/Hud) copies Fill straight into RectTransform anchors, so NaN would poison the layout. Also FlightTelemetryModel.Build:131 yields NaN/Infinity speed for non-finite velocity. Fix: treat NaN as 0 (float.IsNaN check) in both clamps, document non-finite handling in the Build docstring and docs/design/ui-port.md#hullbreachhud-module-reference, add tests for NaN and +/-Infinity inputs.
