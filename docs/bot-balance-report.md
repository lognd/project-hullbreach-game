# Bot balance report (flight-assist physics)

Produced by `tools/plaincs/bot_report.sh`. Every row is real `ServerSimulation` matches between two
`BotPeer`s, demo arena, no storm, 16 seeds, cut at 20 s. Ships are built within the economy budget (60 points,
40 blocks). A core breach decides a match; otherwise the side that dealt clearly more damage (15 percent and 50
points) takes a points win. A time-limit core-health tiebreak is not counted, because one lucky core hit is a
noisy measure of skill. Odd seeds swap spawn slots; mirrors use the same ship on both sides.

| scenario | n | A wins | B wins | even | A share |
|---|---|---|---|---|---|
| Hard vs Easy | 16 | 12 | 4 | 0 | 75% |
| Normal vs Easy | 16 | 8 | 3 | 5 | 73% |
| Hard vs Normal | 16 | 8 | 6 | 2 | 57% |
| Mirror Normal | 16 | 5 | 10 | 1 | 33% |
| Mirror Hard | 16 | 8 | 5 | 3 | 62% |

Zero crashes and zero stalls. Runs are deterministic: the same seed gives the same match. An earlier run under
the pre-assist physics (60 s matches) gave Hard vs Easy 78 percent and Normal vs Easy 85 percent; it was not
repeated here, and neither were the storm and personality sweeps, so treat those as unverified on the current physics.

## Reading it

- The skill ladder holds at the bottom: Hard and Normal beat Easy roughly three to one. Between runs Hard vs Easy
  moved between 67 and 93 percent, so 16 seeds is a wide band (plus or minus about 24 points).
- Hard is only slightly ahead of Normal (57 percent, inside the noise). Reaction and aim differ, but fights are
  short on hits.
- Mirror Normal at 33 percent is at the edge of the noise band; the unit test accepts 15 to 85 percent for that reason.
- Strafing hurt: with the assist physics a strafing pilot spent its time turning instead of shooting, so Normal and
  Hard no longer strafe (StrafePeriod 0) and only dodge lightly.
- No match ends by core breach in 20 s. Cores sit behind armour and hull; the storm's sudden death is what forces a kill.
- Personalities were not re-measured on this physics.

## Cost

About 0.15 s of wall time per simulated second, nearly all of it the structural solver in
`ServerSimulation.Tick` (the bots take about 0.5 percent). The unit tests use 20 s matches. Run scenarios as
separate processes with `--only` (not in parallel with a build; concurrent `dotnet run` builds collide).

## Update: real storm, economy and planet crash (follow-up PR)

The arena now runs the server's own storm, economy and planet crash; `StormOverlay` is gone. The harness
configures the arena before the peers join, because the match, its storm and its economy start on the second
join. (Before this fix the default storm ran in every match, including the ones labelled "no storm".)

300 s limit, 40 seeds per scenario, 240 matches each, zero crashes:

| Run | Core breach | Storm sudden death | Time limit | Draw | Stall | Median length |
|---|---|---|---|---|---|---|
| No storm | 45% | 0% | 32% | 23% | 13% | 300 s |
| Default storm | 14% | 70% | 0% | 17% | 78% | 285 s |

Scripted held thrust, held steer, both together, tap and alternating inputs, and a real bot match with firing
off, never push a bot design past 0.64 of its stress limit, so no flight-model change was needed. With the
default storm, sudden death (at 284 s) decides most matches, so the storm schedule is the next thing to tune.

## Storm tuning

Goal: most default matches end by core breach in about two to five minutes, and the storm only breaks genuine
stalemates. With no storm at 600 s (the server time limit) 72% of 120 matches end by core breach, median 313 s,
so the ring must not close before about 500 s and must not strip blocks early enough to disarm the ships.

Three schedules, all with the 20 s minimum warning kept (every hold is at least 25 s). Sample sizes differ, so
read the trends: the first two are 20 seeds per scenario over all six scenarios (120 matches), the third is 10
seeds in three scenarios (30 matches), because the machine was short of memory. 600 s limit.

| Schedule (grace, damage per phase, sudden death, closes at) | Matches | Core breach | Storm sudden death | Draw | Stall | Median length |
|---|---|---|---|---|---|---|
| Old: 30 s, 8/16/32/64/128, 200 dps, 284 s (300 s limit, 240 matches) | 240 | 14% | 70% | 17% | 78% | 285 s |
| A: 90 s, 4/8/16/32/64, 150 dps, 460 s | 120 | 38% | 48% | 14% | 59% | 460 s |
| B: 120 s, 2/4/8/16/32, 120 dps, 490 s (Hard vs Normal, Mirror Normal, Hard vs Easy) | 30 | 47% | 50% | 3% | 40% | 490 s |
| C (chosen): 180 s, 1/2/4/8/16, 100 dps, 550 s (same three scenarios) | 30 | 73% | 27% | 0% | 27% | 217 s |
| No storm, 600 s (reference) | 120 | 72% | n/a | 12% | 24% | 313 s |

C per scenario (10 seeds): Hard vs Easy 70% breach, Hard vs Normal 80%, Mirror Normal 70%; the rest ended by
sudden death at 552 s. Gentle early damage matters as much as the late ring: early block loss disarms both ships
and turns fights into stalemates.

Not fixed here, to be honest about it: Hard vs Normal is close to even (Hard won 4 of 10 here, 5 of 10 in
the 600 s no-storm run, 8 of 20 in the earlier one), so the Hard over Normal ordering is not established at
these sample sizes. Mirror matches sit near 50%. Bots spend about half of all ticks in the planet-avoidance
layer; shrinking its margin from 6 to 3 made no clear difference and was not kept.

## Update: storm-aware pilot

The pilot now reads the ring ahead of time (per-difficulty lead, footprint and steering error), keeps fighting when the
enemy is in range and the ring is only coming, and presses the attack in sudden death. Small sweep, default storm, 600 s
limit, 6 seeds per scenario (36 matches each run), same seeds before and after, no crashes in either:

| Scenario | Before: A wins (core / sudden death) | After: A wins (core / sudden death) |
|---|---|---|
| Hard vs Easy | 3 of 6 (2 / 4) | 4 of 6 (2 / 4) |
| Hard vs Normal | 1 of 6 (3 / 3) | 2 of 6 (4 / 2) |
| Normal vs Easy | 4 of 6 (3 / 3) | 4 of 6 (3 / 2, 1 draw) |
| Mirror Easy | 3 of 6 (0 / 3) | 4 of 6 (0 / 2) |
| Mirror Normal | 3 of 6 (4 / 2) | 2 of 6 (4 / 2) |
| Mirror Hard | 1 of 6 (3 / 2) | 2 of 6 (4 / 2) |
| All matches ending by core breach | 15 of 36 (42%) | 17 of 36 (47%) |
| All matches ending by sudden death | 17 of 36 (47%) | 14 of 36 (39%) |

Mirror matches: 7 of 18 A wins before, 8 of 18 after (slots alternate, so a 6-seed mirror cannot sit at exactly 50%).
Six seeds cannot separate Hard from Normal (Hard won 1 of 6 before and 2 of 6 after), so, as before, that ordering is not
established at this size; Hard and Normal both beat Easy 4 of 6. The ordering the change does guarantee is in the storm
skill itself (`StormAwarenessTests`): Hard reads the ring 12 s ahead, Normal 8 s, Easy 2 s.

## Update: deterministic re-sweep on current main

`ArenaMatch` is now deterministic per seed (PR #181; it used a random match id before, so the earlier before/after
tables above carry run-to-run noise and are not exactly reproducible). This sweep is the first reproducible one:
default storm (180 s grace, closes at 550 s), storm-aware bots, 600 s limit, 12 seeds per scenario (seeds 1 to 12, slots
alternate on every other seed), 72 matches, zero crashes. Rerun with
`tools/plaincs/bot_report.sh --storm --seeds 12 --seconds 600 --only "<scenario>"`; the same seeds give the same rows.

| Scenario | A wins | B wins | even | A share | Core breach | Sudden death | Draw | Median length | p10 / p90 |
|---|---|---|---|---|---|---|---|---|---|
| Hard vs Easy | 9 | 3 | 0 | 75% | 7 | 5 | 0 | 299 s | 155 / 552 s |
| Hard vs Normal | 7 | 5 | 0 | 58% | 9 | 3 | 0 | 214 s | 138 / 550 s |
| Normal vs Easy | 12 | 0 | 0 | 100% | 7 | 5 | 0 | 347 s | 167 / 552 s |
| Mirror Easy | 5 | 5 | 2 | 50% | 0 | 4 | 8 | 552 s | 551 / 552 s |
| Mirror Normal | 7 | 4 | 1 | 64% | 8 | 3 | 1 | 294 s | 102 / 552 s |
| Mirror Hard | 7 | 5 | 0 | 58% | 8 | 4 | 0 | 165 s | 80 / 552 s |
| All 72 | 47 | 22 | 3 | | 39 (54%) | 24 (33%) | 9 (13%) | 341 s | 114 / 552 s |

Core breaches take a median of 202 s overall. Of the 24 sudden-death matches the first-listed (higher-skill or A) side won 16 and the other 8, so sudden death
is a weak signal of skill, not a coin flip.

Reading it:

- Most matches end by core breach (54%), in about 3 to 4 minutes (median 202 s; per scenario 135 to 262 s). Sudden death
  at about 552 s decides a third; the storm is a backstop and not the main ending, as intended by the 180 s grace.
- Ladder: Normal beat Easy 12 of 12, Hard beat Easy 9 of 12 (it has 3 sudden-death losses, all in matches where
  Easy kept its core alive until the ring closed). Hard over Normal is 7 to 5, still inside the noise at n=12 (a 95%
  interval is roughly 30 to 83%), so Hard over Normal is visible in direction only. Hard mirrors end fastest (median
  165 s), so Hard is the most aggressive tier.
- Mirrors: Easy 50%, Normal 64%, Hard 58% of the decided matches, all consistent with 50% at this size.
- Draws: 9 of 72, 8 of them in Mirror Easy. Two Easy bots rarely reach each other's core (0 breaches in 12), so the
  match sits until the storm closes and both die or time out. That is passivity, but only of the Easy tier, which is
  meant to be weak; Normal and Hard mirrors have 1 draw in 24.
- No tuning was made: the evidence (small samples, Hard over Normal within noise, draws confined to Easy mirrors) is not
  clear enough for a change. A candidate if it is wanted later: make Easy attack when its target is in range during the
  grace period, which would turn Easy mirror draws into core breaches; it needs its own before/after run.

Compared with the earlier tables: breach share is 54% (the 6-seed run had 47%, the 30-match schedule C run 73%), and
Hard vs Normal remains close to even in every run so far. The earlier numbers should be read with the randomised match
id in mind.

## Update: Easy engages (planet clearance and closer range)

Cause of the Easy mirror draws (8 of 12, 0 core breaches). It was not passivity in the fight logic. The Easy ship has
two thruster pairs (acceleration about 0.8 against about 2.0 for Normal), and the main planet (radius 18, mu 900) pulls
harder than that near its surface. With a 3 s, 6-block planet check an Easy ship grazed the planet late, crash-landed
or fell into plunging orbits, and then spent about 65% of its ticks in avoid-planet (Normal: 25 to 30%) with 70 to 130
shots in a whole match. Easy also held 22 blocks, where its loose aim (0.075 rad, lead 0.35) scatters over the hull
instead of the core.

Change (Easy profile only): planet clearance 12 blocks (was 6) over a 6 s coast (was 3), and an engage range scale of 0.6
(13 blocks for Balanced instead of 22). Normal and Hard values are untouched. Same seeds 1 to 12, 600 s, default storm,
before is the sweep above:

| Scenario | Before | After |
|---|---|---|
| Mirror Easy | 0 breaches, 4 storm, 8 draws | 2 breaches, 7 storm, 3 draws |
| Normal vs Easy (Normal wins) | 12 of 12 | 10 of 12 (9 breaches, 1 storm win, 2 Easy storm wins) |
| Hard vs Easy (Hard wins) | 9 of 12 (7 breaches) | 9 of 12 (7 breaches) |
| Mirror Normal (A / B / draw) | 7 / 4 / 1, 8 breaches | 7 / 4 / 1, 8 breaches, rows identical |

Honest reading: draws fall from 8 to 3 and Easy no longer spends the match stuck on a planet, but "most Easy mirrors end
by core breach" is not reached (2 of 12; breaches at about 5.5 min). Easy shots mostly land on outer blocks and Easy
never flanks. Normal and Hard results are unchanged, as intended (the Normal mirror is row-for-row identical). Cost:
Normal beats Easy 10 of 12 instead of 12, because a ship that no longer crash-lands now survives to the ring more often.

Tried and rejected: raising Easy trigger discipline to 0.7 and adding pressure 0.3 gave 5 of 12 mirror breaches but
Normal beat Easy only 8 of 12 (Easy too strong); a range scale of 0.45 gave 1 of 12; a clearance of 1 to 3 blocks made
the landing trap worse (up to 96% of ticks in avoid-planet). A better fix is probably in the arena (spawn orbits that do
not plunge into the planet) or in giving Easy one more thruster pair, which is not done here.
