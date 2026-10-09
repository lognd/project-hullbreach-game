+++
id = "01M4GR2X3S16BTTFXCJS13QGF8"
title = "Bound the client reliable reorder window"
type = "security"
category = "done"
outcome = "fixed"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:31:34Z"
updated = "2026-10-09T17:08:34Z"
labels = ["origin:auditor", "security"]
scope = ["Assets/Scripts/Hullbreach.Net/ClientReplica.cs"]

[[acceptance]]
text = "ApplyReliable drops sequences beyond a fixed MaxReliableWindow past the last applied sequence; no INV-002 policy failure"
bound = true
+++

origin: auditor. Invariant INV-002; policy rule POL-net-reliable-window-bounded. _pendingReliable grows without bound on far-ahead sequences and a skipped sequence stalls delivery forever. Fix direction: MaxReliableWindow constant, drop or resync outside it. Leave frob:invariant INV-002 anchor.
