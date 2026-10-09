+++
id = "01M4GR2MYDBW9GJHKJJNK5ZGTZ"
title = "ServerSimulation projectile hit test ignores ProjectileSpec.Radius and can hit the shooter's own ship"
type = "bug"
category = "todo"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:31:25Z"
updated = "2026-10-09T17:05:57Z"
labels = ["origin:auditor", "audit:ship"]
scope = ["Assets/Scripts/Hullbreach.Net/ServerSimulation.cs"]
+++

Symbol: ServerSimulation.TryHitAnyShip / StepProjectiles (ServerSimulation.cs:~200-235). ProjectileSpec.Radius is documented 'Collision radius for hit testing' but the server tests only the single cell containing the projectile centre. Also no owner is carried (ShotRequest.Key identifies the cannon but ProjectileBody drops it and ship): a cannon facing an adjacent own block, or a ship rotating/moving into its own muzzle point (CannonBehaviourBase muzzle = 0.6 along facing), registers a hit on the shooter on the first step. Fast projectiles (speed 20 at 50 Hz = 0.4/tick) are fine for cells but the Radius contract is unmet. Fix: store the owning peer in ProjectileBody and skip it for a short grace period or until clear of the muzzle; test a circle of Spec.Radius against block cells (or document that Radius is unused by the server and remove it from the spec). Add an integration test firing a cannon with a block in front of it.
