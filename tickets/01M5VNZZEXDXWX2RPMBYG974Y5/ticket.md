+++
id = "01M5VNZZEXDXWX2RPMBYG974Y5"
title = "LoopbackTransport.Tick releases same-tick reliable messages in reverse send order"
type = "bug"
category = "todo"
priority = "medium"
reporter = "mcnairrobotics"
created = "2026-10-26T08:42:22Z"
updated = "2026-10-26T08:47:55Z"
labels = ["game"]
+++

found while working 01M5VNXAT5CA5MSTM07TYX3ATQ: Tick iterates the in-flight list backwards, so two reliable sends delivered on the same tick arrive newest-first, contradicting reliable-ordered semantics. Fix: iterate forward (stable) and add a test.
