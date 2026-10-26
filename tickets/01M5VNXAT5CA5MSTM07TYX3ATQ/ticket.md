+++
id = "01M5VNXAT5CA5MSTM07TYX3ATQ"
title = "S28-3b: Host decodes incoming ReadyUp into ServerSimulation.SetReady"
type = "task"
category = "todo"
priority = "high"
parent = "01M3DG5Y0HT0GE1F2YVQY24SSN"
reporter = "mcnairrobotics"
created = "2026-10-26T08:40:55Z"
updated = "2026-10-26T08:40:55Z"
labels = ["game", "owner:mcnairrobotics"]
+++

Follow-up to S28-3: NetDemo decoded only InputMessage, so nothing called SetReady outside tests. Add engine-free HostInbox.Dispatch in Hullbreach.Net, use it from NetDemo, test over LoopbackTransport.
