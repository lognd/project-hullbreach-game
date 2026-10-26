+++
id = "01M5VP8KEQGKSSKWC0NFXE2KNB"
title = "ClientReplica stamps poses with arrival tick; same-tick arrivals snap instead of interpolate"
type = "bug"
category = "in-progress"
priority = "medium"
reporter = "mcnairrobotics"
created = "2026-10-26T08:47:04Z"
updated = "2026-10-26T08:53:13Z"
labels = ["game", "creates:changelog.d/01M5VP8KEQGKSSKWC0NFXE2KNB.fixed.md"]
scope = ["Assets/Scripts/Hullbreach.Net/**", "Assets/Scripts/Hullbreach.Game/NetDemo.cs", "Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachNetCodeClientSystem.cs", "Assets/Tests/EditMode/Hullbreach.Net.Tests/**", "docs/reference/hullbreach-net.md", "docs/netcode.md", "changelog.d/01M5VP8KEQGKSSKWC0NFXE2KNB.fixed.md"]

[[acceptance]]
text = "Poses are stamped with the server tick; the strict ThrottledLink smoothing assertion passes"
bound = true
+++

found while working ~YG974Y5: with LoopbackTransport delivering same-tick messages in send order, ThrottledLink_InterpolatedViewJumpsLessThanRawSnapping needed a 1.25x tolerance. Poses carry no server tick, so two arriving on one tick get span 0 and snap. Fix: stamp with a server tick, then restore the strict assertion.
