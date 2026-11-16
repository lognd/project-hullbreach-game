# Hullbreach.Bots reference

Per-type reference for `Assets/Scripts/Hullbreach.Bots`, linked from the code by
`// frob:doc docs/reference/hullbreach-bots.md#<anchor>`. The module is engine-free
(`noEngineReferences`): a bot is another peer of `ServerSimulation`, fed from public
match state and answering with the same `InputMessage` a person's client sends.
The first balance numbers are in [bot-balance-report.md](../bot-balance-report.md).

```
ServerSimulation --public state--> BotPeer.Observe --> BotObservation
                                                           |
                                              IShipInputSource.Next   (BotPilot)
                                                           |
ServerSimulation.SetInput <-- InputMessage <-- BotPeer.Drive <-- ShipInput
```

### BotRng

<!-- describes: Assets/Scripts/Hullbreach.Bots/BotRng.cs::BotRng -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotRng.cs::BotRng.NextULong -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotRng.cs::BotRng.NextFloat -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotRng.cs::BotRng.Range -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotRng.cs::BotRng.Chance -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotRng.cs::BotRng.Gaussian -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotRng.cs::BotRng.Fork -->

The only source of randomness in the module: a splitmix64 stream, so a bot is a pure function
of its seed and what it sees. `Fork(salt)` derives an independent stream (one for aim noise, one
for decisions) without disturbing the parent.

### BotDifficulty

<!-- describes: Assets/Scripts/Hullbreach.Bots/BotProfile.cs::BotDifficulty -->

`Easy`, `Normal`, `Hard`. Difficulty changes how well the bot plays the same controls, never what it can
see or press.

### BotPersonality

<!-- describes: Assets/Scripts/Hullbreach.Bots/BotProfile.cs::BotPersonality -->

`Balanced`, `Aggressive` (close range, stays in the fight), `Cautious` (long range, retreats early,
armoured ships), `Skirmisher` (medium range, frequent strafing, more fins).

### BotProfile

<!-- describes: Assets/Scripts/Hullbreach.Bots/BotProfile.cs::BotProfile -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotProfile.cs::BotProfile.For -->

The tuning table, from `BotProfile.For(difficulty, personality)`. Human-bounded on purpose: the
fastest reaction is 9 ticks (180 ms at 50 Hz), the tightest aim error about 0.7 degrees, and no
profile fires on a perfect solution every time. The bot sees the enemy as a delayed copy, so
`ReactionTicks` is real latency and not a cooldown on decisions.

| | Easy | Normal | Hard |
|---|---|---|---|
| reaction | 30 ticks | 14 | 9 |
| decision interval | 18 | 9 | 5 |
| aim error (sigma, rad) | 0.075 | 0.03 | 0.012 |
| lead quality | 0.35 | 0.85 | 1.0 |
| dodge skill | 0 | 0.5 | 0.9 |
| engage range scale | 0.6 | 1 | 1 |
| planet clearance (blocks) | 12 | 6 | 6 |
| planet lookahead (s) | 6 | 4 | 4 |

Easy holds 0.6 of the personality's engage range (13 blocks for Balanced) so its loose aim still lands on the core,
and keeps 12 blocks (not 6) above a planet over a 6 s coast. Its ship has two thruster pairs and cannot climb out of a
planet's well, so a graze that is only corrected late ends with the ship landed and stuck in avoid-planet for the rest
of the match. Normal and Hard are unchanged. See `docs/bot-balance-report.md`.

### BotObservation

<!-- describes: Assets/Scripts/Hullbreach.Bots/BotObservation.cs::BotObservation -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotObservation.cs::SelfView -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotObservation.cs::EnemyView -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotObservation.cs::ProjectileView -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotObservation.cs::EnemyCell -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotObservation.cs::PickupView -->

What one player could read off the screen: the own ship (pose, velocity, exact core damage from its own HUD,
thrust and turn ability, cannon cooldown, spare blocks), the enemy as drawn (pose, velocity, the core marker the
minimap shows, its blocks with a four-step damage sprite), shots in flight, pickups, the planets, the arena edge
and, when there is one, the storm. The enemy's core damage is rounded down to quarters, because the sprite shows a
state, not a number. It carries no enemy input, no exact enemy health and no number a spectator could not read.

### IShipInputSource

<!-- describes: Assets/Scripts/Hullbreach.Bots/BotObservation.cs::IShipInputSource -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotObservation.cs::IShipInputSource.Next -->

The boundary: `ShipInput Next(BotObservation)`, called once per server tick, returning the same
`ShipInput(thrustAxis, steer, firePressed)` a human control produces. Anything that implements it
can drive a `BotPeer`, which is how a scripted test pilot or a replay would plug in.

### BotPilot

<!-- describes: Assets/Scripts/Hullbreach.Bots/BotPilot.cs::BotPilot -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotPilot.cs::BotPilot.Next -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotPilot.cs::BotPilot.Mode -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotPilot.cs::BotPilot.Profile -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotPilot.cs::BotPilot.FireRequests -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotPilot.cs::BotMode -->

The layered pilot. Layers, highest priority first:

1. Survive: predict a coasting path under the visible gravity and burn away from any planet it
   grazes; turn back at the arena edge; keep the ship's blocks inside the storm ring when a storm is
   present. The pilot reads the ring `StormLeadSeconds` ahead (a shrink that starts inside that window
   is already the edge, so it pre-positions), keeps `StormExtentFactor` of the ship's radius plus
   `StormMargin` and its coasting speed inside the edge, and steers to the safe centre with a heading
   error of `StormSteerSigma`. Hard reads furthest and steers exactly, Easy only notices the ring when
   it moves and steers loosely. Outside the ring the bot always flies back (still firing if the nose
   happens to cover the enemy); merely about to be caught with the enemy in range it keeps fighting
   and gives ground toward the centre. In sudden death nobody runs or retreats: the bot closes to half
   range and presses the attack. With no storm the layer is simply inactive.
2. Scavenge: when the enemy is over 24 units away and nothing is shooting, fly to a nearby pickup (not near a planet, the edge or the storm ring, and not about to expire) with a velocity-matching burn.
3. Retreat when the core is badly hurt and losing.
4. Dodge a shot whose closest approach is inside the ship's width (Easy never, Normal and Hard lightly; they do not strafe, because turning away from the target costs shots).
5. Flank: after 30 s, every few seconds look at the enemy's on-screen blocks and circle toward a side with at least two fewer blocks between the guns and its core.
6. Hold the preferred range, closing in when the enemy core sprite looks hurt with a closing-speed controller.
7. Aim and fire: an iterative gravity-aware lead solution against a delayed, noisy copy of the
   enemy, fire discipline (a per-cooldown roll, a tolerance cone, no shot into a planet).

`Mode` is the layer that last decided, for traces and tests.

### BotBuilder

<!-- describes: Assets/Scripts/Hullbreach.Bots/BotBuilder.cs::BotBuilder -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotBuilder.cs::BotBuilder.Build -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotBuilder.cs::BotBuilder.Validate -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotBuilder.cs::BotBuilder.FromTemplate -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotBuilder.cs::BotBuilder.BlockBudget -->

Generates a ship for a seed, difficulty and personality: a symmetric hull around a welded core,
fins on the flanks, guns on the nose, engines on the stern. `Validate` replays the placement order through
`PlacementRules.CanPlace`, runs `DesignValidator`, checks thrust, turn rate and weapons, and runs the
structural solver for 150 ticks. `Build` takes an optional `BlockCosts` (default `BlockCosts.Default()`: 60 points, 40 blocks, welds surcharged) so a bot ship always passes the server's economy check in `Join`. A design that fails is retried with the next derived seed and, after
24 failures, replaced by a known-good dart. Easy ships are slow and light on guns; Normal and Hard
get the same competent build, so the gap between them is the pilot.

### BotShip

<!-- describes: Assets/Scripts/Hullbreach.Bots/BotBuilder.cs::BotShip -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotBuilder.cs::BotShip.BlockCount -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotBuilder.cs::BotShip.RebuildSeconds -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotBuilder.cs::BotShip.ToSnapshot -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotBuilder.cs::BotPlacement -->

A finished design plus the order it is built in. `ToSnapshot` makes the `ShipSnapshot` a client would send
to `Join`. The weld flag is not part of a snapshot, so welds only matter for the live build order and
`RebuildSeconds` (a mid-match rebuild costs one build cooldown per block).

### ShipCapabilities

<!-- describes: Assets/Scripts/Hullbreach.Bots/ShipCapabilities.cs::ShipCapabilities -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/ShipCapabilities.cs::ShipCapabilities.Measure -->

Forward and retro acceleration, turn acceleration, gun axis and radius, read off the ship's own blocks:
what a player learns by flying it. Recomputed when the block count changes.

### IStormSource

<!-- describes: Assets/Scripts/Hullbreach.Bots/BotPeer.cs::IStormSource -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotPeer.cs::IStormSource.TryGetState -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotPeer.cs::StormZoneSource -->

Where a bot reads the storm. Returning false means no storm, and every storm behaviour switches off.
`StormZoneSource` serves a `StormZone` schedule, the same pure function every client evaluates.

### BotPeer

<!-- describes: Assets/Scripts/Hullbreach.Bots/BotPeer.cs::BotPeer -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotPeer.cs::BotPeer.Drive -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotPeer.cs::BotPeer.Observe -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotPeer.cs::BotPeer.PeerId -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotPeer.cs::BotPeer.Source -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotPeer.cs::BotPeer.Quartermaster -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotPeer.cs::DiscardOutbox -->

Stands where a network client would. Once per tick call `Drive(sim, tick, storm)`: it builds the
observation from `ServerSimulation`'s public state, asks the `IShipInputSource`, and sends the result with
`ServerSimulation.SetInput` as a real `InputMessage`. The server cannot tell it from a person and trusts it no
further. `DiscardOutbox` is the `IServerOutbox` for matches with no real clients.

To host a bot in a live match: `Join(botPeer, design.ToSnapshot(...))` like any client, then call
`Drive` every server tick (inputs hold for 0.25 s and a peer is dropped after 5 s of silence).

### BotQuartermaster

<!-- describes: Assets/Scripts/Hullbreach.Bots/BotQuartermaster.cs::BotQuartermaster -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotQuartermaster.cs::BotQuartermaster.Act -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotQuartermaster.cs::BotQuartermaster.Placed -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotQuartermaster.cs::BotQuartermaster.Deconstructed -->

The economy half of play, through the calls a build menu makes: `SetInput` with the build-mode flag, then
`PlaceBlock` or `Deconstruct`. Every ten ticks, when the enemy is over 18 units away and no hostile shot is
within 25, it puts the highest-priority lost part of its own design back from stock (engines, guns, fins,
armour, hull in that order) and, when nothing is missing, deconstructs the most damaged hull or armour block
(damage 215 or more, never a fresh one) for the refund. Build mode freezes the ship for that tick and the server
adds its own cooldown, which is the price of doing it. Collecting crates and salvage is movement, so the pilot
does it (`BotMode.Scavenge`); the server collects on contact. `ArenaMatchConfig.Quartermaster = false` turns the
whole economy side off to measure what it is worth.

### ArenaMatch

<!-- describes: Assets/Scripts/Hullbreach.Bots/ArenaMatch.cs::ArenaMatch -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/ArenaMatch.cs::ArenaMatch.Run -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/ArenaMatch.cs::ArenaMatchConfig -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/ArenaMatch.cs::ArenaMatchResult -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/ArenaMatch.cs::ArenaSide -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/ArenaMatch.cs::ArenaEnding -->

One headless bot-vs-bot match through the real `ServerSimulation` (same join checks, projectiles,
contacts, structural solver and match state machine). It never throws: a crash becomes
`ArenaEnding.Crash` with the message in `Error`. The storm, economy and planet crash are the
server's own (`ArenaMatchConfig.Storm`, `Economy`, `Crash`); the harness adds no damage of its own. A breach after the last
ring has closed is classed `Storm` (sudden death). A match with no breach ends at the time limit; the result also
carries a `PointsWinner` (the side that dealt clearly more damage) so a balance report can still rank the two.

### BalanceReport

<!-- describes: Assets/Scripts/Hullbreach.Bots/BalanceReport.cs::BalanceReport -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BalanceReport.cs::BalanceReport.RunBatch -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BalanceReport.cs::BalanceReport.RunStandard -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BalanceReport.cs::BalanceReport.Format -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BalanceReport.cs::ArenaBatch -->

Seed sweeps and the stdout table. Odd seeds swap the spawn slots so a slot advantage cancels, and a mirror
batch gives both sides the same ship. Run it with `tools/plaincs/bot_report.sh`.

## Unity follow-up (not engine-free)

Not done here, because each needs the editor, scenes or prefabs:

- A "Play vs Bot" button on the main menu that starts a local match with one `BotPeer`.
- A bot peer in the lobby: pick difficulty and personality, show a bot display name, count it as ready.
- Queue fill: when matchmaking times out, join a `BotPeer` instead of leaving the player waiting.
- A call to `BotPeer.Drive` from the server tick loop, and `BotBuilder.Build` for the bot's `Join` snapshot.
