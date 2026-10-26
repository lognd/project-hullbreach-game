+++
id = "01M5VA5A9YR599CEWSFN3QWRNB"
title = "Server: track per-player match stats (damage, blocks) for the report"
type = "bug"
category = "done"
outcome = "fixed"
priority = "medium"
reporter = "lognd"
created = "2026-10-26T05:15:34Z"
updated = "2026-10-26T05:43:08Z"

[[acceptance]]
text = "Given a finished match, when reported, then each player's damage, blocks destroyed/placed and time alive come from the server tally, not zeros"
bound = true
+++

found while working ~ZTVN11E: ServerSimulation.ParticipantFor reports zeros
