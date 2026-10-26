+++
id = "01M5VNZZEXDXWX2RPMBYG974Y5"
title = "LoopbackTransport.Tick releases same-tick reliable messages in reverse send order"
type = "bug"
category = "done"
outcome = "fixed"
priority = "medium"
reporter = "mcnairrobotics"
created = "2026-10-26T08:42:22Z"
updated = "2026-10-26T08:48:40Z"
labels = ["game"]
scope = ["Assets/Scripts/Hullbreach.Net/LoopbackTransport.cs", "Assets/Tests/EditMode/Hullbreach.Net.Tests/*.cs", "docs/reference/hullbreach-net.md", "changelog.d/*"]

[[acceptance]]
text = "Reliable sends arrive in send order, including same-tick and under jitter"
bound = true
+++

found while working 01M5VNXAT5CA5MSTM07TYX3ATQ: Tick iterates the in-flight list backwards, so two reliable sends delivered on the same tick arrive newest-first, contradicting reliable-ordered semantics. Fix: iterate forward (stable) and add a test.
