+++
id = "01M5VP8KEQGKSSKWC0NFXE2KNB"
title = "ClientReplica stamps poses with arrival tick; same-tick arrivals snap instead of interpolate"
type = "bug"
category = "todo"
priority = "medium"
reporter = "mcnairrobotics"
created = "2026-10-26T08:47:04Z"
updated = "2026-10-26T08:47:04Z"
labels = ["game"]
+++

found while working ~YG974Y5: with LoopbackTransport delivering same-tick messages in send order, ThrottledLink_InterpolatedViewJumpsLessThanRawSnapping needed a 1.25x tolerance. Poses carry no server tick, so two arriving on one tick get span 0 and snap. Fix: stamp with a server tick, then restore the strict assertion.
