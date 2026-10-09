+++
id = "01M4GR3RSAWAREA4S1ZQDYSEHV"
title = "Projectile.OnTriggerEnter2D applies impulse and damage once per block collider hit in the same physics step"
type = "bug"
category = "done"
outcome = "fixed"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:32:02Z"
updated = "2026-10-09T17:13:47Z"
idempotency_key = "audit-game-proj-double-hit"
labels = ["origin:auditor", "audit:hullbreach-game"]
scope = ["Assets/Scripts/Hullbreach.Game/Projectile.cs"]

[[acceptance]]
text = "Given a round overlapping two block colliders, when it triggers, then impulse and damage are applied once"
bound = false
+++

Projectile.cs:98-118. ShipCollider adds one BoxCollider2D per block (ShipCollider.cs:250), so a projectile overlapping two adjacent blocks gets OnTriggerEnter2D for each before Destroy(gameObject) at line 117 takes effect at end of frame. Each call runs ApplyImpulse and ApplyDamage and DropWellIfAny (line 113-115), so a single shot can double damage/impulse or drop two gravity wells. Also line 104 is dead code (the ownerIgnoreUntil check is subsumed by line 105). Fix: add a bool _spent field set on first valid hit and early-return when set; delete the redundant line 104 and _ownerIgnoreUntil; add a play-mode test firing at a 2-wide ship asserting damage is applied once.
