+++
id = "01M4GR2J0W7YFCJTQGQP6WQDDS"
title = "ShipBody powerup expiry survives block removal/replacement and reverts the wrong block"
type = "bug"
category = "done"
outcome = "fixed"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:31:22Z"
updated = "2026-10-09T17:09:11Z"
labels = ["origin:auditor", "audit:ship"]
scope = ["Assets/Scripts/Hullbreach.Ship/ShipBody.cs"]

[[acceptance]]
text = "Given a powered block that is removed and replaced at the same key, when the ship steps past the old expiry, then the new block's variant is untouched"
bound = true
+++

Symbol: ShipBody._powerupExpiryByKey / RebuildDerivedViews / TickPowerups (ShipBody.cs:126, 152-153, 482, 515-519). Contract gap: RebuildDerivedViews prunes per-key throttle and cooldown state for removed blocks (doc: 'Per-key ramp/cooldown state is pruned') but never prunes _powerupExpiryByKey. If a powered block is destroyed/detached and a new block is later placed at the same key (mid-match building, S34), VariantTimeLeft(key) reports a stale timer and TickPowerups strips the variant bits from the unrelated new block (e.g. a fresh gravity-gun cannon silently becomes a stock cannon). Fix: add _powerupExpiryByKey to the prune in RebuildDerivedViews (surviving = all key sets), and document in docs/reference/hullbreach-ship.md#shipbody. Add an edit-mode test: power up, remove block, re-add same key, Step past expiry, assert variant untouched.
