# Block economy

Goal: mid-match building should be fun and reasonable. Free building let whoever spammed hull
first win any brawl, while a hard "no building" removed the comeback story. So building spends stock.

## Rules and default numbers

- Pre-match: build freely under a 60 point budget and a 40 block cap (hull 1, fin 2, armor 3,
  retro 3, thruster 4, cannon 5). The server rejects oversized joins (OverBudget, DesignTooLarge).
- Match start: stock of 3 hull. The core can never be rebuilt.
- Mid-match placing spends one spare block, takes the 1.5 s build cooldown and starts at Damage 170,
  hardening over 8 s, so a block cannot be dropped in as an instant shield.
- Deconstruct refunds 50% (credit accumulates), so build/deconstruct loops never mint blocks.
- Destroyed and stranded blocks leave salvage with 70% chance, drifting out for 40 s.
- Crates: first at 20 s, then every 12 s. Common crates hold hull, armor, thruster, fin or retro;
  rare cannon crates sit on the storm edge, so looting costs safety.
- Pickups arm after 1.5 s, need relative speed at most 4, sit within 2.0 of a block, and cannot be
  taken through a planet. The closest ship wins; ties go to the lower peer id.
- Comeback trickle: only the trailing ship, one block per 20 s scaled up to 3x by block deficit,
  stock cap 6, 12 per match.

## Exploits considered

Self-damage farming (70% salvage, arm delay), deconstruct/rebuild loops (50% refund, tested),
placing without stock (OutOfStock, no distrust, tested), collecting through planets (tested),
ramming pickups at speed (tested), flooding salvage (cap 96, oldest culled, tested).

## Follow-ups

Unity side (not done here, no editor work): inventory bar in the build UI, pickup sprites, collect
feedback, refusal toasts from `EconomyRules.Describe`, a deconstruct key, minimap pickup dots from
`ClientEconomy.GetPickups`, wiring PlaceBlock results into HullbreachNetCodeServerSystem, and a
pickup snapshot for reconnects. The bot balance report waits on feat/bot-ai. Welded cost is not
enforced over the wire.
