# Bot balance report (first run)

Produced by `tools/plaincs/bot_report.sh`. Every row is real `ServerSimulation` matches between two
`BotPeer`s, demo arena, no storm unless stated. "A share" is A's wins among decided matches. A win is a core
breach or a time-limit tiebreak; when the server calls a draw (tied cores) the side that dealt clearly more
damage (15 percent and 50 points) takes it as a points win. Odd seeds swap spawn slots; mirrors use the same ship
on both sides. Matches are cut at the time limit shown.

| scenario | n | A wins | B wins | even | A share | limit |
|---|---|---|---|---|---|---|
| Hard vs Easy | 20 | 14 | 4 | 2 | 78% | 60 s |
| Normal vs Easy | 20 | 17 | 3 | 0 | 85% | 60 s |
| Hard vs Normal | 20 | 8 | 10 | 2 | 44% | 60 s |
| Mirror Easy | 20 | 8 | 11 | 1 | 42% | 60 s |
| Mirror Normal | 20 | 10 | 9 | 1 | 53% | 60 s |
| Mirror Hard | 20 | 12 | 7 | 1 | 63% | 60 s |
| Storm on: Hard vs Easy | 12 | 9 | 2 | 1 | 82% | 90 s |
| Storm on: Mirror Normal | 12 | 5 | 5 | 2 | 50% | 90 s |

Zero crashes, zero stalls in all 196 matches. The runs are deterministic: the same seed gives the same match.

Personality round robin (Normal, 8 seeds each, 60 s): Aggressive beat Balanced 6-1, Skirmisher beat Aggressive 8-0
and Cautious 5-2, Balanced beat Cautious 4-2.

## Reading it

- The skill ladder works at the bottom: Hard and Normal both beat Easy by about 4 to 1 or better.
- Mirrors are within noise of 50/50 (the 20-seed 95 percent band is roughly 28 to 72 percent), so the slot
  swap does cancel the spawn bias.
- Hard does not yet beat Normal. Hard has faster reactions and tighter aim, but the fights are short on
  hits (a 60 s match deals a few hundred damage), so the extra accuracy rarely decides them.
- Personalities are not balanced: Skirmisher is the strongest and Cautious the weakest.
- No match in this run ended by core breach. Cores sit behind armour and hull, so a 60 s fight ends on the time
  limit. The storm's sudden death (after about 284 s) is what forces a kill; the 90 s storm rows only
  confirm bots follow the ring without dying early.

## Cost

About 0.15 s of wall time per simulated second, nearly all of it the structural solver in
`ServerSimulation.Tick` (the bots take about 0.5 percent). The unit tests therefore use 20 s matches; this report used
parallel processes (`--only`).
