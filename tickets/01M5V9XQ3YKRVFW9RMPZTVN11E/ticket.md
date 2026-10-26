+++
id = "01M5V9XQ3YKRVFW9RMPZTVN11E"
title = "Server: construct MatchStateMachine, tick it, destroy core at 255 damage"
type = "bug"
category = "todo"
priority = "medium"
reporter = "lognd"
created = "2026-10-26T05:11:25Z"
updated = "2026-10-26T05:11:25Z"
+++

found while working ~3Y92T1Z: ServerSimulation.ApplyProjectileImpact never destroys the core (key != CoreKey) and nothing constructs Hullbreach.Match.MatchStateMachine, so MatchEndFeed.Shared is still never raised in a live match.
