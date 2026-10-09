+++
id = "01M4GR21D4RKF5QYP2AYFDS6NN"
title = "BuilderSession.Click ignores its key argument while Orienting; facing depends on a prior Hover call"
type = "bug"
category = "done"
outcome = "fixed"
priority = "low"
reporter = "lognd"
created = "2026-10-09T16:31:06Z"
updated = "2026-10-09T16:53:26Z"
labels = ["origin:auditor", "auditor"]
scope = ["Assets/Scripts/Hullbreach.Builder/BuilderSession.cs"]

[[acceptance]]
text = "Given Orienting, when Click(key) is called without Hover, then facing derives from key"
bound = false
+++

Symbol: BuilderSession.Click / Hover (BuilderSession.cs:99-119). While Orienting, Click(key) commits using PendingModifiers left by the LAST Hover, and ignores key. Hover is a query that mutates PendingModifiers (line 103), so a caller that never hovered (BuilderController.TryPlaceAt, BuilderController.cs:77, used by play-mode tests and any scripted caller) gets facing 0 (+y) regardless of the second cell passed, a wrong-but-not-erroring result. Docs state 'facing from the last Hover' but the public TryPlaceAt hides that precondition. Fix direction: in the Orienting branch of Click compute modifiers = FacingTowards(PendingKey, key) itself (so Click(key) is self-contained and Hover is side-effect free for state), keep Hover's preview separate; update docs/reference/hullbreach-builder.md#buildersession and add a test of Click-Click with no Hover.
