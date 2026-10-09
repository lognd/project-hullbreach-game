+++
id = "01M4FDM98T2KKH1NSHY2FCHMZ3"
title = "S55-1: Input pipeline audit: sampling, dropped presses and latency"
type = "task"
category = "todo"
priority = "medium"
points = 2
parent = "01M4FDM93PQE69WH2J77HTH96V"
reporter = "lognd"
created = "2026-10-09T04:09:35Z"
updated = "2026-10-09T04:09:35Z"
labels = ["owner:stevendangkhoi", "game", "milestone:0.3.0"]
scope = ["docs/**", "Assets/Tests/**"]

[[acceptance]]
text = "Given the input path from device to ShipInput, when audited, then docs record where each control is sampled (Update vs FixedUpdate), every place a press can be dropped or delayed, and measured input-to-thrust latency in frames at 60 and 144 Hz"
bound = false

[[acceptance]]
text = "Given each problem found, when the audit closes, then a follow-up ticket exists for it"
bound = false
+++

Covers legacy Input vs the planned Input System port (S27-2).
