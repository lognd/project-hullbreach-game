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
