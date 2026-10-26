+++
id = "01M5V9XQ3YKRVFW9RMPZTVN11E"
title = "Server: construct MatchStateMachine, tick it, destroy core at 255 damage"
type = "bug"
category = "done"
outcome = "fixed"
priority = "medium"
reporter = "lognd"
created = "2026-10-26T05:11:25Z"
updated = "2026-10-26T05:16:04Z"

[[acceptance]]
text = "Given two peers joined, when impacts take a core to damage 255, then MatchEndFeed is raised once with the other peer as winner"
bound = true
+++

found while working ~3Y92T1Z: ServerSimulation.ApplyProjectileImpact never destroys the core (key != CoreKey) and nothing constructs Hullbreach.Match.MatchStateMachine, so MatchEndFeed.Shared is still never raised in a live match.
