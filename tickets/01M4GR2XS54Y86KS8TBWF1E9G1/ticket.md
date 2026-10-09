+++
id = "01M4GR2XS54Y86KS8TBWF1E9G1"
title = "Validate client-supplied designs and inputs on the server"
type = "security"
category = "done"
outcome = "fixed"
priority = "high"
reporter = "lognd"
created = "2026-10-09T16:31:34Z"
updated = "2026-10-09T17:08:35Z"
labels = ["origin:auditor", "security"]
scope = ["Assets/Scripts/Hullbreach.Net/ServerSimulation.cs", "Assets/Scripts/Hullbreach.Net/NetMessages.cs"]

[[acceptance]]
text = "Join validates block count, types, mods, damage, pose and velocity; SetInput clamps axes; NetId no longer truncates peer; no INV-003 policy failure"
bound = true
+++

origin: auditor. Invariant INV-003; policy rule POL-server-validates-client-design. Join trusts the client ShipSnapshot wholesale (client-authoritative ship state, rebroadcast to all peers); SetInput axis -128 decodes to -1.008; NetId=(ushort)peer truncates. Fix direction: ValidateDesign against block budget and type table, clamp axes, bound-check peer id. Leave frob:invariant INV-003 anchor.
