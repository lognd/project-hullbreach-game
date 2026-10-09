+++
id = "01M4FDM8C8GH3HM55Z3JDCRYWQ"
title = "S54: Dodge the wreckage when a ship breaks apart"
type = "story"
category = "todo"
priority = "medium"
parent = "01M3DG5Y06HCEC2NK39DR1347N"
reporter = "lognd"
created = "2026-10-09T04:09:34Z"
updated = "2026-10-09T04:09:34Z"
persona = "player"
capability = "have pieces that break off a ship become drifting hazards, like asteroids, after a short arming delay"
outcome_text = "breaking an opponent changes the battlefield without killing anyone unfairly"
labels = ["owner:GingerVHS", "game", "physics", "milestone:0.3.0"]
+++

Card: as a player, I want fragments that break off any ship to become hazards that behave like asteroids, but not instantly, so that wreckage matters without a cheap instant death.

Today: ServerSimulation.ResolveDetachAfterDestruction emits FragmentSpawned and the groups are removed; debris bodies with their own physics are S38-3 (T-0081, partial). Asteroid hazards are S45-1 (not started); whichever lands first should build the shared hazard collision model so the two stay one implementation.

No-instant-death edge cases to cover (S54-2):
- fragment spawned overlapping its parent ship or another ship
- fragment that detaches from a ship moving fast (inherits velocity, then the ship decelerates into it)
- chain reaction: a fragment's own fuse or impact breaking the parent again during the grace window
- fragment arming while inside or touching any ship
- many fragments at once from one break (cumulative damage in one tick)
- fragment spawned next to the opponent's core
- fragment at the arena boundary (soft push-back from S41-1)

Conversation:
- Arming delay length and whether it is per-ship (parent immune longer) or global.
- Damage scaling: by relative speed and fragment mass, with a per-hit cap so no single fragment hit can destroy a core from full health.
- Lifetime and caps: despawn after N seconds or when leaving the arena; max live fragments.
