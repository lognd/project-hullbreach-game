+++
id = "01M7EXX75QK41EKPV5392HXCAX"
title = "Bot opponents: engine-free pilot, builder and balance arena"
type = "story"
category = "done"
outcome = "fixed"
priority = "medium"
reporter = "lognd"
created = "2026-11-15T06:21:56Z"
updated = "2026-11-15T12:14:09Z"

[[acceptance]]
text = "A bot builds a valid ship through the normal placement rules and flies it through the server input boundary (BotPeer), deterministically per seed"
bound = true

[[acceptance]]
text = "Bot difficulty tiers are ordered by skill and a harder bot beats an easier one in the headless arena"
bound = true
+++
