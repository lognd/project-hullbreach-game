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

### BotObservation

<!-- describes: Assets/Scripts/Hullbreach.Bots/BotObservation.cs::BotObservation -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotObservation.cs::SelfView -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotObservation.cs::EnemyView -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotObservation.cs::ProjectileView -->

What one player could read off the screen: the own ship (pose, velocity, core damage, thrust and
turn ability, cannon cooldown), the enemy (pose, velocity, core position and damage), shots in flight,
the planets, the arena edge and, when there is one, the storm. It carries no enemy input and no enemy design.

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
   grazes; turn back at the arena edge; stay inside the storm ring (and pre-position for the next
   one) when a storm is present. With no storm the layer is simply inactive.
2. Retreat when the core is badly hurt and losing.
3. Dodge a shot whose closest approach is inside the ship's width, strafe on a timer.
4. Hold the preferred range with a closing-speed controller.
5. Aim and fire: an iterative gravity-aware lead solution against a delayed, noisy copy of the
   enemy, fire discipline (a per-cooldown roll, a tolerance cone, no shot into a planet).

`Mode` is the layer that last decided, for traces and tests.

### BotBuilder

<!-- describes: Assets/Scripts/Hullbreach.Bots/BotBuilder.cs::BotBuilder -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotBuilder.cs::BotBuilder.Build -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotBuilder.cs::BotBuilder.Validate -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotBuilder.cs::BotBuilder.BlockBudget -->

Generates a ship for a seed, difficulty and personality: a symmetric hull around a welded core,
fins on the flanks, guns on the nose, engines on the stern. `Validate` replays the placement order through
`PlacementRules.CanPlace`, runs `DesignValidator`, checks thrust, turn rate and weapons, and runs the
structural solver for 150 ticks. A design that fails is retried with the next derived seed and, after
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
<!-- describes: Assets/Scripts/Hullbreach.Bots/BotPeer.cs::DiscardOutbox -->

Stands where a network client would. Once per tick call `Drive(sim, tick, storm)`: it builds the
observation from `ServerSimulation`'s public state, asks the `IShipInputSource`, and sends the result with
`ServerSimulation.SetInput` as a real `InputMessage`. The server cannot tell it from a person and trusts it no
further. `DiscardOutbox` is the `IServerOutbox` for matches with no real clients.

To host a bot in a live match: `Join(botPeer, design.ToSnapshot(...))` like any client, then call
`Drive` every server tick (inputs hold for 0.25 s and a peer is dropped after 5 s of silence).

### ArenaMatch

<!-- describes: Assets/Scripts/Hullbreach.Bots/ArenaMatch.cs::ArenaMatch -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/ArenaMatch.cs::ArenaMatch.Run -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/ArenaMatch.cs::ArenaMatchConfig -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/ArenaMatch.cs::ArenaMatchResult -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/ArenaMatch.cs::ArenaSide -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/ArenaMatch.cs::ArenaEnding -->

One headless bot-vs-bot match through the real `ServerSimulation` (same join checks, projectiles,
contacts, structural solver and match state machine). It never throws: a crash becomes
`ArenaEnding.Crash` with the message in `Error`. A match with no core breach ends at the time limit; if the
cores are tied the server calls it a draw, and the result also carries a `PointsWinner` (the side that
dealt clearly more damage) so a balance report can still rank the two.

### BalanceReport

<!-- describes: Assets/Scripts/Hullbreach.Bots/BalanceReport.cs::BalanceReport -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BalanceReport.cs::BalanceReport.RunBatch -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BalanceReport.cs::BalanceReport.RunStandard -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BalanceReport.cs::BalanceReport.Format -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/BalanceReport.cs::ArenaBatch -->

Seed sweeps and the stdout table. Odd seeds swap the spawn slots so a slot advantage cancels, and a mirror
batch gives both sides the same ship. Run it with `tools/plaincs/bot_report.sh`.

### StormOverlay

<!-- describes: Assets/Scripts/Hullbreach.Bots/StormOverlay.cs::StormOverlay -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/StormOverlay.cs::StormOverlay.Apply -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/StormOverlay.cs::StormOverlay.CoreDamageBy -->
<!-- describes: Assets/Scripts/Hullbreach.Bots/StormOverlay.cs::StormOverlay.TotalDamageTo -->

A stand-in for the server applying storm damage, used only by the arena until `ServerSimulation` does it
itself. It marks blocks damaged but never removes them. Retire it once the storm wiring lands, or storm damage
is counted twice.

## Unity follow-up (not engine-free)

Not done here, because each needs the editor, scenes or prefabs:

- A "Play vs Bot" button on the main menu that starts a local match with one `BotPeer`.
- A bot peer in the lobby: pick difficulty and personality, show a bot display name, count it as ready.
- Queue fill: when matchmaking times out, join a `BotPeer` instead of leaving the player waiting.
- A call to `BotPeer.Drive` from the server tick loop, and `BotBuilder.Build` for the bot's `Join` snapshot.
