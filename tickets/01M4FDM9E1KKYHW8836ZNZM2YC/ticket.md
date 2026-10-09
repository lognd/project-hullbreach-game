+++
id = "01M4FDM9E1KKYHW8836ZNZM2YC"
title = "S55-2: Flight-feel audit: thrust, torque, damping and turn rate"
type = "task"
category = "todo"
priority = "medium"
points = 2
parent = "01M4FDM93PQE69WH2J77HTH96V"
reporter = "lognd"
created = "2026-10-09T04:09:35Z"
updated = "2026-10-09T04:09:35Z"
labels = ["owner:GingerVHS", "game", "physics", "milestone:0.3.0"]
scope = ["docs/**", "Assets/Tests/**"]

[[acceptance]]
text = "Given the reference ships (starter, 20, 50 and 100 blocks), when measured in edit-mode tests, then docs record time to 90-degree turn, 0-to-cruise time and stopping distance"
bound = false

[[acceptance]]
text = "Given the measurements, when the audit closes, then proposed target numbers and follow-up tickets for each tuning change exist"
bound = false
+++

Plain-C# measurements via ShipBody so they run under tools/plaincs.
