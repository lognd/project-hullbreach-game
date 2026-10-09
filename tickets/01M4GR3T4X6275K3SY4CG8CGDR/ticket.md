+++
id = "01M4GR3T4X6275K3SY4CG8CGDR"
title = "ProjectileSpawner.SpawnFromSink throws NRE before Start and never wires ships created later; WorldSink.SpawnProjectile silently drops shots without a spawner"
type = "bug"
category = "todo"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:32:04Z"
updated = "2026-10-09T16:32:04Z"
idempotency_key = "audit-game-spawner-lifecycle"
labels = ["origin:auditor", "audit:hullbreach-game"]
scope = ["Assets/Scripts/Hullbreach.Game/ProjectileSpawner.cs"]
+++

ProjectileSpawner.cs:137-153,189-196 and WorldSink.cs:369-372. _ships is only assigned in Start, but SpawnFromSink is public and reached via WorldSink.SpawnProjectile; FindOwner does foreach over _ships (null before Start, or if a behaviour fires in the first frame) -> NullReferenceException. Ships spawned after Start never get ShipBody.Projectile set or the ShotFired subscription, so their cannon shots (and FindOwner attribution) silently do nothing. WorldSink.SpawnProjectile (line 371) returns without any log when spawner is null. Fix: lazily initialise _ships (null-guard), expose Register(ShipController)/Refresh used by WorldSink.Refresh, and LogError once in WorldSink when spawner is missing. Integration test: spawn a second ship after Start and fire.
