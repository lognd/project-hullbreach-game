+++
id = "01M4GRDPS7SKGCBQ6V24Q6459R"
title = "S12-5: Replicate the cosmetic loadout at join and validate it against owned items on the server"
type = "task"
category = "todo"
priority = "medium"
points = 3
parent = "01M3DG5Y0DCWYQ5H93E6DQMXG2"
reporter = "lognd"
created = "2026-10-09T16:37:28Z"
updated = "2026-10-09T16:37:28Z"
labels = ["owner:mcnairrobotics", "game", "platform", "milestone:0.3.0"]

[[links]]
kind = "blocked-by"
target = "01M4GR2XS54Y86KS8TBWF1E9G1"

[[acceptance]]
text = "The join message and ship snapshot carry the loadout so both players see each other's cosmetics."
bound = false

[[acceptance]]
text = "The server replaces any cosmetic id the platform session does not list as owned with the default, and logs it."
bound = false

[[acceptance]]
text = "Wire decoding of the loadout is bounds-checked (INV-001) and capped at the number of block types."
bound = false
+++
