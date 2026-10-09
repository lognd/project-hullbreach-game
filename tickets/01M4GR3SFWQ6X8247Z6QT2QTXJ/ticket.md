+++
id = "01M4GR3SFWQ6X8247Z6QT2QTXJ"
title = "GravityWorld.OnDestroy clears the static Field even when it belongs to another instance"
type = "bug"
category = "done"
outcome = "duplicate"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:32:03Z"
updated = "2026-10-09T16:36:20Z"
idempotency_key = "audit-game-gravity-destroy"
labels = ["origin:auditor", "audit:hullbreach-game"]
scope = ["Assets/Scripts/Hullbreach.Game/GravityWorld.cs"]
+++

GravityWorld.cs:425-431. The comment says it only clears the static if this instance set it, but the code tests ReferenceEquals(Field, null) and then unconditionally sets Field = null. On a scene reload or additive load where the new GravityWorld.Awake runs before the old one's OnDestroy, the live field is blanked, so ShipController/Projectile/OrbitStarter silently see 'no gravity' (all of them null-tolerate it, so nothing errors). Fix: keep a private field _field set in Awake and only null the static when ReferenceEquals(Field, _field). Test: two GravityWorlds, destroy the older one, assert Field still non-null.
