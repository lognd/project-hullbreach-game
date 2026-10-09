+++
id = "01M4GR2JQW22CZF6E9GVMS93RN"
title = "ShipBody.Step early return on zero mass leaves ContactsThisStep and accumulators stale"
type = "bug"
category = "done"
outcome = "fixed"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:31:23Z"
updated = "2026-10-09T17:09:48Z"
labels = ["origin:auditor", "audit:ship"]
scope = ["Assets/Scripts/Hullbreach.Ship/ShipBody.cs"]

[[acceptance]]
text = "Given an empty ship, when Step runs, then ContactsThisStep and accumulated forces are cleared"
bound = true
+++

Symbol: ShipBody.Step (ShipBody.cs:221-231). Doc/comment on ContactsThisStep says 'Cleared and repopulated every Step', but the mass<=0 early return skips ResolvePlanetContacts (the only place that clears it), so renderer/audio re-consume the last contacts every tick once the ship is emptied; _forceAccum/_torqueAccum set by external AddForceAtPoint are also kept and applied to a later re-populated ship, and TickPowerups is skipped. Fix: clear ContactsThisStep and zero _forceAccum/_torqueAccum before the early return (or restructure so clears happen first), and state the empty-ship contract in the reference doc.
