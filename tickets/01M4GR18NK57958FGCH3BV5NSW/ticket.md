+++
id = "01M4GR18NK57958FGCH3BV5NSW"
title = "GravityWorld.OnDestroy nulls static Field unconditionally despite ownership comment"
type = "bug"
category = "todo"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:30:40Z"
updated = "2026-10-09T16:30:40Z"
labels = ["origin:auditor"]
scope = ["Assets/Scripts/Hullbreach.Game/GravityWorld.cs"]
+++

Game/GravityWorld.cs:82-87: comment says only clear if this instance set it, but code only checks Field is null then sets Field=null, so destroying any stale/duplicate GravityWorld blanks the live field (GravityWorld.Field consumed by WorldSink, Projectile, OrbitStarter, DemoMode). Fix: keep an instance field _field set in Awake and do 'if (ReferenceEquals(Field, _field)) Field = null;'; add test with two GravityWorld instances.
