+++
id = "01M4GR1S7JC1SDFN6PFQXEB44B"
title = "Hud models format with current culture, so HUD text varies by OS locale"
type = "bug"
category = "todo"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:30:57Z"
updated = "2026-10-09T16:56:42Z"
labels = ["origin:auditor"]
scope = ["Assets/Scripts/Hullbreach.Hud/*Model.cs"]

[[acceptance]]
text = "Given a de-DE thread culture, when Hud models build text, then output uses an invariant decimal point"
bound = false
+++

Contract: Hud models are documented as pure functions producing the exact old IMGUI text (D3). Violated: every numeric interpolation uses the thread's CurrentCulture: FlightTelemetryModel.cs:133 ({speed:0.0}, {absAngular:0.00}), :94 and :101 (percent, +0;-0;0 sections), BuilderHudModel.cs:45,55, HullWarningModel.cs:255, StatusPanelModel.cs:388,396. On a comma-decimal locale the HUD shows '12,3' and unit tests asserting '12.3' fail by machine. Fix: format with CultureInfo.InvariantCulture (FormattableString.Invariant or string.Create) in all Hud models and add an EditMode test that sets CurrentCulture to de-DE and asserts identical output.
