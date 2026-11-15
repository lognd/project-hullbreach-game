# Design: UX and game-experience (GX) pass

Status: proposal, 2026-11-15. Written without running the game (see
[What this pass could not assess](#what-this-pass-could-not-assess)).
Inputs: `docs/` (controls, demo-scene, roadmap, `design/ui-port.md`),
`Hullbreach.Hud`, `Hullbreach.Game/Hud`, `Hullbreach.Platform`
(title/sign-in/lobby models), `Hullbreach.Match`, `ServerSimulation`,
the five-or-so existing playtest screenshots (title, lobby, build, fly),
and `COORDINATOR_NOTES.md` playtests R1-R3.

Guiding rule from the owner: **it needs to be fun and reasonable.** Where
this doc has to choose, it chooses the option a first-time player
understands in under a minute and a veteran still finds interesting at
minute ten. Anything that is not on the 4-5 minute critical path of
"queue, build, fight, result, again" is P2.

Related: [ui-port](ui-port.md), [controls](../controls.md),
[demo-scene](../demo-scene.md), [platform-link](../platform-link.md),
[netcode](../netcode.md), [roadmap](../roadmap.md).

## 0. Where we are (observed)

- Title: panel with PLAY (orange), BUILD, SETTINGS, SIGN IN, QUIT, and a
  "Not signed in" corner line. Clear hierarchy, but the text is small at
  1280x720 and the panel has a lot of dead space below QUIT.
- Lobby ("HULLBREACH ONLINE"): one dense card holds host form, join by
  code, LAN host/join, a public-lobby list, QUIT GAME, and FIND MATCH
  floating below it. Five different jobs, no clear "what do I click first".
  Field labels and placeholders are about 8-9 px tall in the capture.
  "HOST WITH RELAY" is primary orange even while offline (R2 note).
- Build mode (`DemoScene`): dark-red IMGUI-looking panels top-left (palette
  with mass/cost) and bottom-left (controls). Functional, no placement
  ghost verdict colours visible in capture, no cooldown or budget bar.
- Fly mode: a "Hull: OK (max ratio 0.34)" line at top-centre, a status
  panel bottom-left with thrust/reverse/steer bars, a plain starfield.
  Nothing says where the enemy, planets, storm or objective are.
- No tutorial, no pause menu, no loading or disconnect screens in the
  tree. Settings = master/music/effects volume, resolution, fullscreen,
  colour-blind palette (`GameSettings`). No key remapping, text size,
  reduced motion, or flight-assist setting.
- Results: `ResultsScreenModel` already has headline, reason, duration,
  per-player damage / blocks destroyed / blocks placed / time alive,
  Rematch and Return-to-lobby (one-shot choice). Result kinds:
  CoreBreached, Draw, Forfeit, TimeLimit. Good bones; no view polish,
  no moment, no replay.
- Build cooldown exists (1.5 s per placement in a fight,
  `ServerSimulation.DefaultBuildCooldownSeconds`) with a HUD text line
  (`BuildCooldownModel`).

## 1. UX

### 1.1 End-to-end flow (target)

```
first launch -> Title -> (Sign in | Play offline) -> Play hub
Play hub: Quick Match | Vs Bot | Host/Join (code, LAN) | Practice
Queue/lobby -> Ready-up -> Build phase -> Match -> Results -> Rematch | Hub
Always reachable: Settings, Back, Quit; Pause inside any offline match.
```

Decisions:

1. **First launch routes into the tutorial** (section 1.5) after one
   screen asking name/guest, never a sign-in wall. Offline/guest is a
   first-class path (`SignInModel.PlayOffline`); online features show
   "Sign in to play online" inline instead of a dead end.
2. **Replace the monolithic lobby card with a Play hub** of four large
   tiles: *Quick Match* (queue, bot-fill), *Vs Bot* (instant, offline),
   *Private/LAN* (host, join by code or address), *Practice* (tutorial and
   sandbox). Host/browse forms open as a second step. Exactly one orange
   primary per screen; "HOST WITH RELAY" is orange only when signed in
   and services are ready.
3. **Every screen has: title, one primary action, a Back (Esc / B), and a
   footer with input hints.** Nothing is a dead end.
4. **Quit** confirms only when a match is in progress.

### 1.2 Screen-by-screen friction and missing states

| Screen | Friction | Missing states to add |
| --- | --- | --- |
| Title | Tiny type; SIGN IN same weight as BUILD; BUILD is not obviously a sandbox | First-run highlight on PLAY; "Continue as <name>"; version string; update/offline banner |
| Sign-in | Username/password only; failure copy is generic (`SignInModel.MessageFor`) | Loading spinner + cancel; "Service unreachable, play offline" (hint exists, make it a button); remember me; password visibility toggle |
| Play hub / lobby | See 1.1; labels unreadable small | Empty public list ("No lobbies yet - host one or Find Match"); list loading; join failure reasons (full, wrong password, code expired) via `FriendlyError` |
| Queue | `FIND MATCH` has no visible progress | Searching timer, cancel, "No opponent yet - start vs bot?" at 15 s (see GX 2.5), found-match 2 s beat with opponent name |
| Ready-up | `ReadyGate` exists as a model only | Both peers shown with ready ticks, 30 s auto-start or kick, "waiting for <name>", opponent left |
| Build phase | See 1.6 | Timer, budget, validity colours, "Ready" button |
| Match HUD | Status panel is debug-grade; no objective info | Enemy core/hull bar, storm timer, minimap, ping, kill-feed-style event lines |
| Pause | None | Offline: pause with Resume/Settings/Forfeit. Online: overlay menu that does *not* pause |
| Disconnect | None visible | Online: "Connection lost - reconnecting 8..." with 10 s grace; then results as Forfeit win/loss, clearly labelled "opponent disconnected" |
| Results | Model only | Headline moment (GX 2.6), stats, Rematch / Back, "opponent wants rematch" indicator, rematch timeout of 30 s |
| Loading | None | Any scene load > 0.3 s shows a branded loading card with tip text |
| Errors | `FriendlyError` copy exists | A single shared toast/dialog component so all errors look and behave alike |

### 1.3 Consistency system (one-page style sheet)

Define once in `Hullbreach.Hud` (engine-free, tested) and consume from
every view; `BrandPalette` already exists in `Hullbreach.Platform`, so
merge palettes rather than add a third.

- **Type scale at 1920x1080 reference** (CanvasScaler, match 0.5):
  Title 72, Heading 40, Button 32, Body 28, Caption 22, Minimum 20.
  Nothing below 20 px at reference. (R1 found 17-18 px text; the lobby
  fields in the latest capture are still below this.) Stress/telemetry
  numbers are Body.
- **Spacing:** 8 px grid; panel padding 24; button height 64, min tap
  target 48; gap between stacked buttons 16.
- **Colour roles** (names, not hexes; designers pick values):
  `Surface`, `SurfaceRaised`, `TextPrimary`, `TextMuted`, `Accent`
  (the existing orange, primary action only), `Info` (teal),
  `Friendly`, `Enemy`, `Warning`, `Danger`. Enemy vs friendly must differ
  by **shape or icon as well as hue** (see 1.4).
- **Button hierarchy:** Primary (filled orange, max one), Secondary
  (outlined cyan - PR #128 already did this), Tertiary (text), Destructive
  (outlined danger, confirm). Four states each: normal, hover/focus,
  pressed, disabled. Focus ring is a 3 px high-contrast outline, always
  visible when navigating by keyboard/pad, hidden on mouse hover only.
- **Navigation:** every screen has an explicit tab order (`TabOrder`
  exists in Platform; reuse it everywhere), arrow keys / D-pad move
  focus, Enter/A confirm, Esc/B back, and the first focus target is the
  primary button.
- **Motion:** 120-200 ms ease-out for panel transitions; nothing loops
  except the CRITICAL pulse and the storm edge. All of it honours the
  reduced-motion setting.

### 1.4 Readability and accessibility

- **Resolutions:** design at 1920x1080; verify at 1280x720 and at a
  1024x600 window. Rule: HUD uses anchors, never pixel offsets; side
  panels collapse to icon + number below 900 px width; minimap shrinks
  rather than overlaps. Add a **text size** setting (100/125/150 %) that
  scales the Body/Caption steps, not the layout.
- **Colour-blind-safe:** `StressPalette` already has an Okabe-Ito mode;
  extend the idea to *every* coded pair. Friendly = blue family with a
  solid outline; enemy = vermillion/orange with a **notched/dashed
  outline and a triangle marker** on minimap and off-screen pointers;
  storm = purple-magenta with a **hatched pattern** and a text warning,
  never red-vs-green. Damage state also shows by shape (cracks, missing
  chunks), not hue alone. Ship a "high contrast" toggle later (P2).
- **Reduced motion / shake:** setting that sets screen shake to 0,
  disables the camera zoom pulse and slow-mo kick, keeps hit flashes but
  as a static tint.
- **Remappable keys:** needed by S27 and by the Input System migration
  already flagged in `roadmap.md`. Do the migration and the rebinding UI
  together; never write a new feature against `Input.GetKey`.
- **Subtitles for audio cues:** short, optional on-screen cues for the
  cues that carry information: "Enemy nearby", "Core hit", "Storm
  closing", "Hull critical", "Match starting 3-2-1". Reuses the event
  lines in the HUD; costs almost nothing.
- **Hearing/visual redundancy rule:** every sound that signals danger has
  a visual twin and vice versa.

### 1.5 Input hints and settings

**Input hints in context.** One `ControlHints` provider (extends
`ShipControls`, which is already the single source of the key wording)
that renders a footer bar for the current mode: Build shows place /
remove / rotate / undo / ready; Fly shows thrust / steer / fire / assist;
results show Rematch / Back. The hints switch glyphs between keyboard and
gamepad by last-used device and show the *remapped* key, not the default.
They fade after the player has used the action 3 times (stored in
settings) and can be re-enabled.

**Settings** (one screen, four tabs, Apply/Revert with a 10 s confirm
for resolution changes):

- *Audio:* master, music, effects, UI (existing three plus UI), subtitles.
- *Graphics:* resolution, fullscreen, vsync, quality preset, reduced
  motion, screen shake %, colour-blind palette (existing).
- *Controls:* rebind table, device switch, mouse-aim vs keyboard-only
  fire, sensitivity, invert steer (rare, cheap).
- *Gameplay:* **flight assist** (see GX 2.8: Assist On / Brake only /
  Off), text size, hint visibility, aim assist off by default.

### 1.6 First-time tutorial ("Cadet Trial", about 2 minutes)

Interactive, scripted, offline, against a passive bot; skippable at any
step (Esc, "Skip tutorial", flagged in settings so Title can offer
"Replay tutorial"). Each step has one instruction, a large input hint,
a completion tick, and no timer pressure. A step completes when the sim
sees the action, not when a timer ends.

1. *Fly (30 s):* "Hold W to thrust, A/D to steer." Reach a glowing ring;
   a second ring needs a turn around a planet so gravity is visible.
   Flight assist is ON for the tutorial.
2. *Build (40 s):* a pre-filled starter ship with one missing
   thruster. Place it (hover shows green ghost), watch the stress overlay
   turn one block amber, add an armor block, undo once. Then "Ready".
3. *Shoot (30 s):* a stationary dummy bot ship; hit it three times,
   see hit flashes and the damage state. Then the bot's core is exposed:
   breach it. Cinematic breach plays at reduced length.
4. *Storm and map (15 s):* a short forced storm edge to glance at; point at
   the minimap.
5. Reward: a free cosmetic (section 2.9), then "Play a bot match".

Needs: a `TutorialScript` engine-free step machine (tested like
`MatchStateMachine`), a passive bot mode in the bot harness, and a scene
or `DemoScene` variant. No new net code.

### 1.7 Build-mode UX review

What exists: two-click placement, remove, Ctrl+Z / Ctrl+Shift+Z undo
(`UndoStack`), 7-entry palette (Core, Hull, Armor, Thruster, Cannon, Fin,
Retro) with mass and cost, Tab to fly, stress overlay, 1.5 s placement
cooldown (online).

Findings and fixes:

- **Placement feedback:** show a ghost block at the cursor tinted
  *valid* (cool white-blue outline), *invalid* (red with an X glyph and
  a one-line reason: "must touch the hull", "overlaps", "over budget"),
  and *blocked by cooldown* (amber, with the radial timer on the cursor).
  Reasons already exist in `PlacementRules`; surface them.
- **Orientation:** thrusters/cannons/fins have facing. Show an arrow on
  the ghost; R or scroll rotates; the preview shows the thrust direction
  and the cannon firing line.
- **Budget and mass:** cost is shown per palette row but not as a total.
  Add a budget bar (spent / cap) and a mass + centre-of-mass marker,
  because off-centre thrust is the first confusing handling surprise.
- **Undo:** keep Ctrl+Z, add on-screen undo/redo buttons (touch-friendly,
  and discoverable). Removing a block that strands others detaches them
  as one undoable action (already decided); announce it ("Removed 3
  blocks") so it is not a surprise.
- **Stress overlay legibility:** limit the ramp to 4 bands with a legend
  (OK / Strain / Critical / Will break), use `StressPalette`'s colour-blind
  mode, draw the top 1-3 worst blocks with a numeric badge, and make the
  overlay a toggle (default on in build, off in fly unless Critical). The
  "worst block" advice string in `HullWarningModel` should point to it
  with an arrow, not just text.
- **Cooldown display (in-fight building):** a circular timer around the
  selected palette slot plus the cursor, and a 1.5 s "tick" sound;
  disabled palette tiles dim, never disappear.
- **Templates / presets:** ship 4 starter designs (Brawler, Sniper,
  Tank, Glass Racer) selectable on the build screen as one click, so a
  new player never faces an empty grid with a single core. Mark the one
  recommended for the tutorial.
- **Save/load designs:** 3 local slots + "last match ship", stored as
  JSON beside `GameSettingsStore`'s file; later a share code (base64 of
  the compact block list) for pasting to friends. Validate on load
  through `PlacementRules`, so a hand-edited design cannot cheat.
  Matches roadmap S35 (`needs-platform` only for cloud save).
- **Build phase timing and the Ready button** are in GX 2.2.

## 2. GX

### 2.1 Target match shape (4-5 minutes)

| Time | Phase | Player feeling | Mechanisms |
| --- | --- | --- | --- |
| 0:00-0:50 | **Build phase** (50 s, both players, simultaneously, Ready to skip) | Planning, cleverness | Shared budget, templates, opponent's silhouette hidden until the end |
| 0:50-1:00 | Countdown + spawn | Anticipation | Ships start on opposite orbits, 3-2-1 beat |
| 1:00-1:45 | **Approach / first contact** (target first shots by ~1:30, i.e. about 40 s after spawn) | Positioning, orbit management | Gravity assist, planet cover |
| 1:45-3:30 | **Mid-game duel** | Trades, repair-vs-attack decisions | 1.5 s place cooldown lets players patch or add a block mid-fight (S34) |
| 3:30-4:30 | **Storm endgame** (storm starts shrinking ~3:00, hard edge ~4:30) | Rising pressure, forced engagement | Shrinking zone pushes both toward the centre |
| 4:30-5:00 | Sudden death / time limit | Climax | Last 30 s: storm damage doubles, core exposed marker |

These numbers are *starting values*, all exposed in `ArenaConfig` /
`MatchConfig`, to be tuned from bot-harness data (section 2.10). The two
risks are a build phase that feels long on a rematch (mitigate with Ready
and "reuse last ship") and a first contact that comes too late (mitigate
with spawn distance and storm start).

### 2.2 Build phase, then "build in fight"

The 50 s pre-match build is the core fantasy. Keep it short, always
skippable by both-ready, and use an escalating timer colour for the last
10 s. During the fight the 1.5 s cooldown means building is a tactical
action, not free repair: make sure it **cannot out-heal damage faster
than a single cannon deals it**, otherwise stalls (see 2.9). Give the
player a visible *spare budget* that refills slowly (e.g. 1 block-worth
per 15 s) rather than an unlimited bank.

### 2.3 Tension and release

- Tension sources, in order: storm edge closing, hull Critical pulse,
  enemy projectile warning (incoming indicator), low spare budget.
- Release: a clean kill shot, escaping the storm edge with a gravity
  slingshot, a block saving a core. Use brief audio/visual lulls after a
  big hit so the next hit lands harder.
- Never let two warnings play at once at the same loudness: priority
  order Core hit > Hull Critical > Storm > Enemy lock.

### 2.4 Readable combat

- **Hit feedback:** projectile hit = 80 ms white flash on the block,
  small debris puff, 40 ms hit-stop at most for the *attacker's* camera
  only (reduced-motion off), a distinct sound per block type
  (armor "clank", hull "crack", thruster "pop").
- **Ship damage states:** four visual states per block driven by existing
  damage/stress: pristine, scratched, cracked + sparking, venting
  (small smoke trail). A ship should read "this is 60 % dead" at a
  glance without a number.
- **Core danger:** the core is the lose condition, so it needs its own
  language: a ring around the core that fills red as hull around it
  breaches, a heartbeat sound at <= 25 %, and an HUD icon for **both**
  cores (own core bottom centre, enemy core on a small enemy panel).
  Always show the enemy's remaining core-adjacent protection as a
  coarse bar, not exact HP, to keep bluffing alive.
- **Off-screen threats:** edge arrows for the enemy ship, incoming
  projectiles that are going to hit, and the storm edge direction.
- **Kill-feed-style event lines** (3 max, 4 s each): "Enemy lost a
  thruster", "Core exposed!".

### 2.5 Comeback and anti-snowball

A duel where the first lost thruster decides the match is not fun.

1. **Spare-budget refill** (2.2) lets a damaged player recover.
2. **Storm pushes the leader too:** the leader cannot just kite; the
   shrinking zone forces contact, so being ahead means attacking, not
   camping.
3. **Core exposure rule:** when a core loses most of its neighbours it
   emits a visible "exposed" marker and the *attacker's* shots do
   normal damage; this is the dramatic last stand, not a free kill. Add
   a 3 s **last-stand shield** (partial damage reduction) the first time a
   core drops below 25 %. Once per match, so it creates a beat not a
   stall.
4. **Debris as ammunition:** blocks that break off are physical objects
   with gravity; the losing player can use them. Cheap, funny, and the
   physics already exists.
5. **Respawn is not offered** (single-life duel); the answer to snowball
   is the three rules above, not respawns.

### 2.6 Win / lose / draw moments

- **Win:** cinematic core breach: 0.4 s freeze, camera pushes to the core
  at 20 % speed for 1.5 s, shockwave ring, ship fragments scatter under
  gravity, then slow-mo ramps back up. Under reduced motion: a 0.5 s
  white flash and cut to results. Total <= 4 s, skippable with any key.
- **Lose:** the same camera, framed on *your* breach, no taunting text;
  headline "Core breached" with the *reason* line (`ResultsScreenModel.Reason`).
- **Draw / time limit / forfeit:** each has distinct copy and art
  (`MatchResult.Draw`, `TimeLimit`, `Forfeit`): "Both cores fell at the
  same moment", "Time up - lowest damage taken wins" (define the
  tiebreak), "Opponent left - you win" (marked as unranked-safe).
- **Results screen:** headline, reason, duration, the existing per-player
  stats, plus: biggest hit, accuracy, blocks lost vs placed, and a ship
  portrait strip (final silhouette of each ship). **Highlights:** the sim
  already records deterministic input; store the last 8 s before the
  breach as an input log and replay it in the results background
  (P2, needs deterministic replay; the bot harness likely proves it).
- Rematch is the default focused button; Back is secondary; a rematch
  offered by the opponent shows a small banner and a 30 s timer.

### 2.7 Matchmaking feel

- Queue shows elapsed time and a rotating tip; after **12-15 s** with no
  human it offers "Fight a bot now?" with a bot ship of similar rating,
  and *auto-fills* at 25 s with a clear label ("Opponent: BOT Rook").
  Never disguise a bot as a human.
- Bot-fill matches are marked **unranked** and do not post to platform
  leaderboards (`MatchResultReporter` should carry an `IsBot` flag).
- Found-match beat: 2 s card with both names, ship silhouettes hidden, a
  "Build starts in 3" countdown.
- Wait-time messaging says what is happening, not just a spinner
  ("Searching public lobbies", "Waiting for relay", "Trying LAN").

### 2.8 Skill expression, difficulty, flight assist

- **Flight assist** (new setting; coordinated with the in-progress
  flight-assist work): *Assist On* (damped rotation and auto-brake to
  prograde, the default for newcomers and the tutorial), *Brake only*
  (rotation is free, a brake key stabilises), *Off* (full Newtonian).
  The mode shown on the HUD with its key. Rule: assist must reduce
  *chore* inputs, never reduce the skill ceiling; Off-mode should have a
  small advantage (e.g. +10 % turn rate) so experts have a reason.
- **Bot difficulty:** Easy (slow reactions, does not use the storm),
  Normal, Hard (leads shots, uses gravity); selectable in Vs Bot; the
  tutorial uses a *passive* bot (no fire).
- **Skill sources:** orbit and slingshot control, aim leading, building
  for a role, and mid-fight patching. Keep all four visible: the HUD
  shows orbit prediction only for the player's own ship (P1) and aim
  leading is *not* shown (that is the skill).

### 2.9 Progression and replay reasons (class-project sized)

All local-first; platform sync is opt-in.

1. **Cosmetic unlocks** by playing (not paying): hull paint, trail
   colours, core glow, 12 items, unlock at 1/3/5/10 matches and via the
   tutorial. Ship skin data already has a placeholder need (see
   `~NDDWXNE` "no skin data source"); this gives it a source.
2. **Daily challenge:** one seed per UTC day: a fixed arena modifier
   (heavy gravity, early storm, low budget) vs a bot; reward a cosmetic
   once per day; streak counter. Cheap because the bot harness already
   runs seeded matches.
3. **Ship-design sharing:** share code (see 1.7) plus a "copy last
   opponent's ship" button on results (view-only silhouette, rebuild as
   new design - avoid exact cloning of paid cosmetics).
4. **Ranked via platform repo:** a Quick Match win/loss/draw posts
   through `MatchResultReporter` to the platform; show rating and a
   leaderboard link on results. Keep ranked human-vs-human only, and
   clamp rating swings for forfeits to discourage rage-quit abuse.
5. **Stats page:** matches, wins, favourite block, longest survival.

### 2.10 Fun risks and the metrics to watch

| Risk | Symptom | How to detect (bot harness) | Mitigation |
| --- | --- | --- | --- |
| **Stalls** | Neither side attacks; storm decides | median time of first hit, % matches ended by TimeLimit/storm | Spawn distance, storm start, spare-budget cap, core exposure |
| **Frustration deaths** | Instant loss at spawn, one-shot cores, planet-crash on handoff | % matches ending < 45 s after spawn, deaths with < 25 % damage dealt | Spawn invulnerability 2 s, crash-rule grace, core buffer blocks |
| **Solved ship design** | One archetype wins > 60 % | win rate per design hash / block mix, mean thrust:mass | Rebalance costs, counter archetypes, rotate daily modifier |
| **One dominant block** | Block X in > 90 % of winning ships or usage ratio per cost | block usage per match vs win delta, damage dealt per cost | Cost tuning, diminishing returns, cooldown on special blocks |
| **Snowball** | First 3 kills predict result > 85 % | P(win | first core-adjacent loss) | Section 2.5 rules |
| **Structure fragility** | Wing snaps off within 1 s of thrust (R3 note) | % ships losing blocks with no hit in first 10 s | Structure tuning (GingerVHS area), stress tutorial copy |
| **Too long/short** | Matches outside 3-6 min | match duration histogram, p10/p50/p90 | Tune storm timeline |
| **Build-phase fatigue** | Players spam Ready | median build time, % using presets | Presets, last ship, shorter timer |

Track as a per-match CSV row from the harness: `duration`, `firstHitAt`,
`winnerArchetype`, `timeToCoreExposed`, `blocksPlacedInFight`,
`stormDamageShare`, `endedBy`, `botDifficulty`. Gate balance changes on a
200-match bot sweep before human playtests.

## 3. Prioritised backlog

Size: S <= 1 day, M 2-4 days, L > 4 days. **Editor** = needs the Unity
editor (scenes/prefabs). Owners are the teammates whose earlier work the
item touches, from `git log` (UI: `stevendangkhoi`, with `a-carten` on
HUD views/art; net: `mcnairrobotics`; structure/physics and some HUD
model code: `GingerVHS`). Credit and review go to them. Frob tickets were
not created: 20+ items is not cheap, and the in-flight storm / minimap /
bot / flight-assist work by other agents will rewrite several of these.
Convert the P0 items to tickets once those land.

### P0 (do first; the loop is not fun or complete without these)

| ID | Item | Acceptance test | Size | Editor | Touches |
| --- | --- | --- | --- | --- | --- |
| P0-1 | Match-critical HUD: enemy/own core danger, storm timer, off-screen enemy arrow, event lines (type scale >= 20 px) | In a bot match, damaging the enemy core visibly moves its bar and the storm timer counts down at 1080p and 720p | M | yes | stevendangkhoi, a-carten (`Hullbreach.Hud`) |
| P0-2 | Results screen view + wired Rematch/Back with Draw/Forfeit/TimeLimit copy | Each of the 4 `MatchResult` kinds shows a distinct headline and the focused button is Rematch | M | yes | stevendangkhoi (`ResultsScreenModel`, `ResultsFlow`), mcnairrobotics (net handoff) |
| P0-3 | Build-phase UX: valid/invalid ghost with reason, orientation arrow, budget bar, Ready button, phase timer | Hover over an overlapping cell shows red + "overlaps"; placing under cooldown shows amber + timer | M | yes | stevendangkhoi (`BuilderHud`), GingerVHS (`PlacementRules` reasons) |
| P0-4 | Interactive tutorial "Cadet Trial" vs passive bot with skip | Fresh profile reaches Title -> tutorial; completes fly, build, shoot, breach in < 3 min with no instructions beyond on-screen text | L | yes | stevendangkhoi (UI), a-carten (scene), bot harness |
| P0-5 | Queue and bot-fill states: searching timer, cancel, 15 s "fight a bot" offer, labelled bot, unranked flag | With no peer, queue offers a bot at 15 s and the result is not posted to ranked | M | partly | mcnairrobotics (`MatchmakingMachine`), stevendangkhoi |
| P0-6 | Disconnect / error / loading states with shared toast + dialog component and `FriendlyError` copy | Killing the peer mid-match yields "Opponent disconnected" results as a win within 10 s | M | yes | mcnairrobotics (net), stevendangkhoi |
| P0-7 | Ship damage states + core danger language (visuals + heartbeat) | At 25 % core protection the core ring is red and a warning plays once | M | yes | GingerVHS (damage data), a-carten (`ShipRenderer`) |

### P1

| ID | Item | Acceptance test | Size | Editor | Touches |
| --- | --- | --- | --- | --- | --- |
| P1-1 | Style sheet in `Hullbreach.Hud` (type scale, roles, spacing) + migrate Title, Lobby, Settings, Hud | A test fails if any text style is < 20 px at reference; screens reviewed at 1280x720 | M | yes | stevendangkhoi |
| P1-2 | Play hub replaces the monolithic lobby card; one primary per screen | Title -> Play shows 4 tiles; Host/Join forms open as second step | M | yes | stevendangkhoi |
| P1-3 | Input hints footer (context + device + remapped key) | Switching to gamepad changes glyphs; rebinding Fire changes the hint | M | yes | stevendangkhoi, GingerVHS (`ShipControls`) |
| P1-4 | Settings: key remap, text size, reduced motion/shake, subtitles, flight-assist, with Input System migration | Rebind Fire, restart, binding persists; reduced motion zeroes shake | L | yes | stevendangkhoi (`GameSettings`), GingerVHS (input) |
| P1-5 | Colour-blind-safe enemy/storm/friendly (shape + hue) | Under a deuteranopia filter, enemy vs friendly marker and storm edge stay distinguishable | S | yes | a-carten (`ShipRenderer`), stevendangkhoi |
| P1-6 | Win/lose cinematic breach (<= 4 s, skippable) with reduced-motion fallback | Breach plays and results appear in <= 4 s; any key skips | M | yes | a-carten, stevendangkhoi |
| P1-7 | Combat feedback: hit flash/sound per block type, incoming projectile warning, off-screen arrows | Hit on armor vs hull has distinct audio and a flash visible at 720p | M | yes | a-carten, GingerVHS |
| P1-8 | Templates (4 presets) and 3 save slots + share code with validation | Load a hand-edited design that violates `PlacementRules` and it is rejected | M | partly | stevendangkhoi (builder UI), GingerVHS (`PlacementRules`) |
| P1-9 | Anti-snowball: spare-budget refill, last-stand shield, debris physics check | In a 200-bot sweep, P(win after first core-adjacent loss) < 80 % | M | no | GingerVHS (structure), mcnairrobotics (`ServerSimulation`) |
| P1-10 | Bot-harness metrics CSV (section 2.10) and a first 200-match tuning report | CSV has the listed columns and a report lists duration p10/p50/p90 | S | no | mcnairrobotics (harness) |

### P2

| ID | Item | Acceptance test | Size | Editor | Touches |
| --- | --- | --- | --- | --- | --- |
| P2-1 | Highlights replay of last 8 s in the results background | Replay starts automatically and is deterministic across 5 runs | L | yes | mcnairrobotics (net), GingerVHS (sim) |
| P2-2 | Cosmetic unlocks (12 items) + stats page | Win 3 matches and a new item unlocks and appears on the ship | M | yes | stevendangkhoi, mcnairrobotics (skin sync `~NDDWXNE`) |
| P2-3 | Daily challenge with seeded modifier + streak | Same date gives identical modifier on two clients | M | partly | mcnairrobotics, stevendangkhoi |
| P2-4 | Ranked link: rating and leaderboard on results via platform | A human win posts and the result screen shows the new rating | M | no | mcnairrobotics (`MatchResultReporter`) |
| P2-5 | High-contrast mode, controller full navigation pass | Whole flow completable with a gamepad only | M | yes | stevendangkhoi |
| P2-6 | Pause menu (offline) and ping/connection indicator | Pause freezes sim; ping turns amber at > 120 ms | S | yes | stevendangkhoi, mcnairrobotics |
| P2-7 | Opponent ship copy as view-only silhouette/design import | Import button creates a valid design from results | S | no | stevendangkhoi |

## What this pass could not assess

Without playing the game I could not judge: how handling actually feels
(thrust curve, gravity pull, whether assist is needed), real first-contact
and storm timings, whether the 50 s build phase feels long or short,
bot difficulty, audio (there was no sound to review), whether text is
legible on the owner's monitor (only 1280x720 captures were available),
sign-in and queue behaviour against a live platform, and the in-flight
storm/minimap/bot/crash/flight-assist work being added now. All numbers
in section 2.1 are starting values for the 200-match bot sweep and a
human playtest, not conclusions.
