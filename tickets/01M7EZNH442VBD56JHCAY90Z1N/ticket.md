+++
id = "01M7EZNH442VBD56JHCAY90Z1N"
title = "Flight feel: assist, ramps, brake, fire buffer"
type = "story"
category = "todo"
priority = "medium"
reporter = "lognd"
created = "2026-11-15T06:52:41Z"
updated = "2026-11-15T12:13:42Z"

[[acceptance]]
text = "Flight assist turns steer input into a capped rate command with heading hold, and holding Left Shift (input flag) gives raw spin"
bound = true

[[acceptance]]
text = "Thrust ramps and the S brake stop the ship without reversing it, and the server and prediction share one FlightTuning"
bound = true

[[acceptance]]
text = "A fire press during cooldown still fires once the cannon is ready"
bound = false
+++
