+++
id = "01M4GR3S4C64YAF769WV4G7ZCV"
title = "Powerup.OnTriggerEnter2D can apply the powerup twice and queue two respawns when several block colliders trigger in one step"
type = "bug"
category = "done"
outcome = "fixed"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:32:03Z"
updated = "2026-10-09T17:14:20Z"
idempotency_key = "audit-game-powerup-double"
labels = ["origin:auditor", "audit:hullbreach-game"]
scope = ["Assets/Scripts/Hullbreach.Game/Powerup.cs"]

[[acceptance]]
text = "Given a pickup overlapping two block colliders, when it triggers, then the powerup applies once and one respawn is queued"
bound = false
+++

Powerup.cs:248-265 plus PowerupSpawner.cs:333-336. A ship has one BoxCollider2D per block, so multiple colliders can enter the trigger in the same physics step before Destroy(gameObject) (line 264) executes. Each pass calls Ship.ApplyPowerup (line 259) and PowerupSpawner.NotifyCollected, which starts a RespawnAfterDelay coroutine each time, leaving duplicate pickups at the same preset position and re-applying the variant. Also the NotifyCollected comment claims it is a no-op for a foreign Powerup but it never checks. Fix: add a _collected flag in Powerup set on first success and early-return; make NotifyCollected idempotent per Powerup instance. Test: ship straddling the trigger yields exactly one respawn.
