+++
id = "01M6WVRZ1MVN8NRMSAW0Q4174K"
title = "Single-player: report match results to the platform"
type = "task"
category = "todo"
priority = "medium"
reporter = "mcnairrobotics"
created = "2026-11-08T05:58:19Z"
updated = "2026-11-08T05:58:19Z"
+++

Dropped: MatchResultReporter reports server-authoritative results (admission gate plus server credentials). Single-player has no server or match state machine, so a client-submitted result would be unverifiable and would need a new platform endpoint and trust model. Needs a platform-side design first.
