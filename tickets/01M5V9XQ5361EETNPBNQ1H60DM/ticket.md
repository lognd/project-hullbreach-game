+++
id = "01M5V9XQ5361EETNPBNQ1H60DM"
title = "S46 follow-up: forfeit win on disconnect and draw policy"
type = "task"
category = "todo"
priority = "medium"
reporter = "lognd"
created = "2026-10-26T05:11:25Z"
updated = "2026-10-26T05:42:59Z"

[[acceptance]]
text = "Given a peer disconnects mid-match, then the remaining peer is reported as winner; a draw is unreported with a logged warning"
bound = true
+++

found while working ~3Y92T1Z: disconnect and same-tick double-core-loss end with no winner and no platform report; decide forfeit and tiebreak policy (with S46-2).
