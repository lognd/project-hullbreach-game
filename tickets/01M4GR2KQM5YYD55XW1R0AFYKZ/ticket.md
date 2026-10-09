+++
id = "01M4GR2KQM5YYD55XW1R0AFYKZ"
title = "ShipContacts.Resolve applies contact damage to separating ships and uses only ship A's damage threshold"
type = "bug"
category = "todo"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:31:24Z"
updated = "2026-10-09T17:12:11Z"
labels = ["origin:auditor", "audit:ship"]
scope = ["Assets/Scripts/Hullbreach.Ship/ShipContacts.cs"]

[[acceptance]]
text = "Given overlapping ships that are separating, when Resolve runs, then no damage; and damage is gated per ship, independent of argument order"
bound = true
+++

Symbol: ShipContacts.Resolve (ShipContacts.cs:133-138). The impulse is guarded by closing<0 but impactSpeed=abs(closing) and the damage block run unconditionally, so two overlapping ships that are already separating faster than ContactDamageSpeed (e.g. after a prior step's push-out, or a thruster burn away) take damage as if they had collided. Also the threshold and ApplyContactDamage scale read a.ContactDamageSpeed for both ships while ApplyContactDamage(b) uses b's DamagePerSpeed: asymmetric, argument-order dependent result (Resolve(a,b) != Resolve(b,a)). Fix: compute impactSpeed only when closing<0 (else return true after separation), and gate each ship on its own ContactDamageSpeed. Add tests for separating-overlap (no damage) and argument-order symmetry.
