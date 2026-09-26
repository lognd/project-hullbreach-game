# Design: port the IMGUI HUD to Unity UI (uGUI)

Status: accepted before implementation. This document is the spec for
the UI port work units U1-U5 below. Owners implement against it; if the
code has to deviate, update this page in the same change.

Related: [architecture](../architecture.md) (the engine-free rule this
design extends), [demo scene layout](../demo-scene.md),
[testing](../testing.md), [roadmap](../roadmap.md),
[Jira export](#jira-stories-this-touches).

## 1. Why

Every on-screen element in the game today is immediate-mode GUI
(`OnGUI`) drawn from code:

| Element | Where | What it draws |
| --- | --- | --- |
| Builder palette panel | `Assets/Scripts/Hullbreach.Game/BuilderHud.cs` (`OnGUI`) | Palette entries with mass/cost, selection marker, total mass, block count, session state, hover verdict |
| Status panel | `Assets/Scripts/Hullbreach.Game/DemoMode.cs` (`DrawStatusPanel`) | Mode, control hints, mass/blocks, speed, thrust/reverse/steer bars, active powerups |
| Hull warning banner | `Assets/Scripts/Hullbreach.Game/DemoMode.cs` (`DrawHullWarning`) | OK/STRAIN/CRITICAL banner with pulsing red, worst block, advice |

IMGUI was the right call for a first playable (no scene setup, no
assets), but it has three costs that now matter:

1. **Nobody but a programmer can change it.** Layout, colors, fonts and
   sizes are literals in C#. Angie (UI art) and Steven (client screens)
   cannot open a prefab and move a panel.
2. **It does not scale to the Sprint 2-3 screens.** Title (S26),
   settings (S27), sign-in (S07-2), lobby (S28-2), queue (S29-2),
   results (S46-3) and radar (S45-2) all need buttons, focus, navigation
   and an `EventSystem`. Building those in IMGUI would be a second
   rewrite later.
3. **Layout is hand-computed.** `BuilderHud` subtracts
   `DemoMode.StatusPanelHeight` from `Screen.height` so the two panels do
   not overlap; anchors and layout groups do this for free.

## 2. Decisions

- **D1: uGUI (`com.unity.ugui` 2.0.0, already in `Packages/manifest.json`)**,
  not UI Toolkit. uGUI is what the Unity learning material our team uses
  teaches, it is editable in the Scene view with anchors, and its
  prefabs are how the rest of the scene is already authored. UI Toolkit
  stays an option for later menu screens; nothing here blocks it.
- **D2: TextMeshPro text** (`TMPro`, shipped inside ugui 2.0 in Unity 6).
  The TMP Essential Resources are imported once and committed under
  `Assets/TextMesh Pro/`. If the import cannot be done headlessly, fall
  back to `UnityEngine.UI.Text` with the built-in `LegacyRuntime.ttf`
  and record that as a deviation here.
- **D3: split each HUD into an engine-free model and a thin view,** the
  same split [architecture.md](../architecture.md#the-one-rule-engine-free-simulation-unity-only-adapters)
  mandates for simulation. A new assembly `Hullbreach.Hud` (no
  `UnityEngine` reference) holds pure functions and value types that
  turn simulation state into exactly what is shown: strings, 0..1 fill
  fractions, warning bands, palette rows. It is compiled by
  `tools/plaincs` and covered by edit-mode tests, so every piece of HUD
  logic is tested without a Unity license. Colors live in the model as
  plain RGBA structs (`HudColor`), not `UnityEngine.Color`.
- **D4: views are `MonoBehaviour`s with `[SerializeField]` references**
  to `TMP_Text`/`Image`/`RectTransform` children of a prefab. A view
  only copies model output into those references in `LateUpdate`; it
  contains no formatting or thresholds. Views never create UI objects at
  runtime (except for list rows cloned from a serialized row template).
- **D5: prefabs are the source of truth for layout.** One root
  `Assets/Prefabs/UI/HudCanvas.prefab` (Screen Space Overlay canvas,
  `CanvasScaler` scale-with-screen-size at 1920x1080, match 0.5) with
  nested prefabs `BuilderPanel`, `StatusPanel`, `HullWarningBanner` and
  the reusable widget `ChannelBar`. `DemoScene` holds one `HudCanvas`
  instance and one `EventSystem`.
- **D6: the prefabs are generated once by an Editor script, then owned
  by hand.** `Assets/Editor/Hullbreach.Editor/HudPrefabBuilder.cs`
  (Editor-only asmdef) builds the prefabs and wires them into
  `DemoScene`, run headlessly with `-executeMethod`. This makes the first
  version reproducible and reviewable without hand-writing scene YAML.
  After that, teammates edit the prefabs in the Editor; the builder is
  kept only as a "reset to default layout" menu item
  (`Hullbreach > UI > Rebuild default HUD prefabs`), and it refuses to
  overwrite an existing prefab unless asked to.
- **D7: behavior parity first.** The port reproduces what the IMGUI HUD
  shows today (same text, same thresholds, same colors, same pulse) so
  the existing play-mode tests and screenshots stay meaningful. Visual
  redesign is Angie's follow-up (S26-3 art pass), not part of the port.
- **D8: `DemoMode` stops drawing.** `DemoMode.OnGUI`, `DrawStatusPanel`,
  `DrawHullWarning`, the bar helpers and `StatusPanelHeight` are
  deleted; `DemoMode` keeps computing `Warning`/`MaxStressRatio`/
  critical-block state (tests already read them) and exposes what the
  views need as read-only properties. `BuilderHud` keeps its class name
  and its enable/disable contract with `DemoMode.ApplyState`, so
  `docs/demo-scene.md` wiring stays recognisable.
- **D9: comments are plain, docs are linked.** Code comments in anything
  U1 or later touches are one or two `//` lines, WHY not WHAT, with no
  XML `<summary>` blocks. Every public type/member instead carries a
  `// frob:doc docs/<page>.md#<anchor>` line pointing at a docs/ heading,
  and that heading lists the symbols it documents with one
  `<!-- frob:describes Assets/Scripts/<Asm>/<File>.cs::<Type>[.<Member>] -->`
  line each (mirrors `../platform/docs/index.md`'s convention). frob will
  check these links once it is wired into this repo (see the schedule);
  until then they are just discoverable cross-references.

## 3. Module map

### `Hullbreach.Hud` (new, engine-free, `Assets/Scripts/Hullbreach.Hud/`)

#### Hullbreach.Hud module reference

<!-- frob:describes Assets/Scripts/Hullbreach.Hud/HudColor.cs::HudColor -->
<!-- frob:describes Assets/Scripts/Hullbreach.Hud/HudColor.cs::HudColor.HudColor -->
<!-- frob:describes Assets/Scripts/Hullbreach.Hud/HudColor.cs::HudColor.ThrustRed -->
<!-- frob:describes Assets/Scripts/Hullbreach.Hud/HudColor.cs::HudColor.ReverseGreen -->
<!-- frob:describes Assets/Scripts/Hullbreach.Hud/HudColor.cs::HudColor.SteerWhite -->
<!-- frob:describes Assets/Scripts/Hullbreach.Hud/HudColor.cs::HudColor.TrackDark -->
<!-- frob:describes Assets/Scripts/Hullbreach.Hud/HudColor.cs::HudColor.WarningOkGreen -->
<!-- frob:describes Assets/Scripts/Hullbreach.Hud/HudColor.cs::HudColor.WarningStrainYellow -->
<!-- frob:describes Assets/Scripts/Hullbreach.Hud/HudColor.cs::HudColor.WarningCriticalRed -->
<!-- frob:describes Assets/Scripts/Hullbreach.Hud/BuilderHudModel.cs::BuilderHudModel -->
<!-- frob:describes Assets/Scripts/Hullbreach.Hud/BuilderHudModel.cs::BuilderHudModel.Build -->

- `HudColor` -- readonly RGBA float struct; the palette constants used
  by the HUD today (thrust red, reverse green, steer white, track dark,
  OK green, STRAIN yellow, CRITICAL red).
- `BuilderHudModel` -- from a `BuilderSession`, the palette
  (`BlockPalette.All()`) and the hover verdict string: title line,
  palette rows (name, mass, cost, selected), total mass, block count,
  state, optional hover line. Format strings identical to today's.
- `FlightTelemetryModel` -- from ship telemetry (speed components,
  angular velocity, forward/reverse/steer throttle means): speed line,
  channel bar values (label text + clamped fill, steer as signed
  -1..1 with center-origin fill).
- `StatusPanelModel` -- mode line, control-hint lines for Build/Fly,
  overlay line, mass/blocks line, and the active powerup lines
  (`VariantLabel` moves here from `DemoMode`).
- `HullWarningModel` -- from (warning band, max ratio, critical block
  count, worst block name, unscaled time): headline, detail, hint,
  color including the CRITICAL pulse, and whether detail lines show.
  `HullWarning` enum moves here (the `Hullbreach.Game` name is kept via
  the tests' `using`s; update them).

### `Hullbreach.Game` views (`Assets/Scripts/Hullbreach.Game/Hud/`)

- `ChannelBar` -- `Image` fill driven by a 0..1 or centered -1..1 value
  plus label; reusable by any later meter (S45 radar warning, S27 volume).
- `BuilderHud` -- rewritten as a view over `BuilderHudModel`; palette
  rows cloned from a row template.
- `StatusPanelView` -- view over `StatusPanelModel` and
  `FlightTelemetryModel`; owns three `ChannelBar`s and the powerup list.
- `HullWarningBanner` -- view over `HullWarningModel`; shown only in Fly.

### Editor (`Assets/Editor/Hullbreach.Editor/`, Editor-only asmdef)

#### HudPrefabBuilder (Editor)

<!-- frob:describes Assets/Editor/Hullbreach.Editor/HudPrefabBuilder.cs::HudPrefabBuilder -->
<!-- frob:describes Assets/Editor/Hullbreach.Editor/HudPrefabBuilder.cs::HudPrefabBuilder.HudCanvasPrefabPath -->
<!-- frob:describes Assets/Editor/Hullbreach.Editor/HudPrefabBuilder.cs::HudPrefabBuilder.BuilderPanelPrefabPath -->
<!-- frob:describes Assets/Editor/Hullbreach.Editor/HudPrefabBuilder.cs::HudPrefabBuilder.DemoScenePath -->
<!-- frob:describes Assets/Editor/Hullbreach.Editor/HudPrefabBuilder.cs::HudPrefabBuilder.RebuildDefaultHudPrefabsMenuItem -->
<!-- frob:describes Assets/Editor/Hullbreach.Editor/HudPrefabBuilder.cs::HudPrefabBuilder.Build -->
<!-- frob:describes Assets/Editor/Hullbreach.Editor/HudPrefabBuilder.cs::HudPrefabBuilder.BuildForce -->
<!-- frob:describes Assets/Editor/Hullbreach.Editor/HudPrefabBuilder.cs::HudPrefabBuilder.Run -->
<!-- frob:describes Assets/Editor/Hullbreach.Editor/HudPrefabBuilder.cs::HudPrefabBuilder.BuildHudCanvasPrefab -->
<!-- frob:describes Assets/Editor/Hullbreach.Editor/HudPrefabBuilder.cs::HudPrefabBuilder.WireDemoScene -->

- `HudPrefabBuilder` -- builds the prefabs and wires `DemoScene` (D6).

**Adding a panel** (U2/U3): write one more `AddXxxPanel(GameObject
canvasRoot)` method following `AddBuilderPanel`'s shape (build the
hierarchy, wire any view component's serialized fields via
`SerializedObject`, save it as its own nested prefab, return the panel
root), then call it from `BuildHudCanvasPrefab` alongside the existing
`AddBuilderPanel` call.

## 4. Work units and owners

Owners follow the Jira areas in the team section of the export
(`jira-export.md`): Steven owns ship builder and client screens, Angie
owns in-game UI art and overlays, Chase owns flight physics, Derrick owns
game CI and releases, Logan owns process and course deliverables. Each
unit lands on its owner's branch, merges to `main`, and `main` is merged
back into every open branch.

| Unit | Owner (branch) | Scope | Depends on |
| --- | --- | --- | --- |
| U0 | Logan (`lognd/ui-port-design`) | This document, schedule, docs index links | - |
| U1 | Steven (`stevendangkhoi/hud-foundation`) | `Hullbreach.Hud` assembly + plaincs wiring, `HudColor`, `BuilderHudModel` + tests, TMP resources, `HudPrefabBuilder`, `HudCanvas` + `BuilderPanel` prefabs, `EventSystem` in `DemoScene`, `BuilderHud` view | U0 |
| U2 | Chase (`GingerVHS/flight-telemetry-hud`) | `FlightTelemetryModel` + tests, `ChannelBar` widget + prefab | U1 |
| U3 | Angie (`a-carten/status-and-warning-hud`) | `StatusPanelModel`, `HullWarningModel` + tests, `StatusPanelView`, `HullWarningBanner`, their prefabs, delete `DemoMode` drawing (D8) | U2 |
| U4 | Derrick (`mcnairrobotics/hud-tests-ci`) | Play-mode tests for the new HUD, `check_unity_tree.sh` rule rejecting new `OnGUI` in `Assets/Scripts`, CI and testing docs | U3 |

## 5. Acceptance criteria (whole port)

1. `grep -rn "OnGUI\|GUILayout\|GUI\." Assets/Scripts` finds nothing.
2. `tools/plaincs/run_tests.sh` passes, including new `Hullbreach.Hud`
   tests; every public model member has a test.
3. The project compiles in the Unity 6000.0.43f1 editor with zero
   errors, and every existing play-mode test passes (headless run per
   [testing.md](../testing.md)).
4. New play-mode tests prove: `DemoScene` has exactly one `HudCanvas`
   and one `EventSystem`; the builder panel is visible only in Build and
   the hull banner only in Fly; the text shown matches the model output
   after a mode switch and after thrusting.
5. `scripts/check_unity_tree.sh` passes (every asset has a `.meta`).
6. [demo-scene.md](../demo-scene.md), [architecture.md](../architecture.md),
   [testing.md](../testing.md) and [roadmap.md](../roadmap.md) describe
   the new layout; a "Editing the HUD" section in
   [getting-started.md](../getting-started.md) tells a non-programmer
   which prefab to open.

## 6. Out of scope

- Visual redesign, art, fonts beyond TMP's default (S26-3, Angie, Sprint 2).
- New screens (title, settings, lobby, results): they build on this
  foundation per the schedule below.
- Replacing the legacy `Input` class with the Input System (tracked in
  roadmap's engineering-debt section; uGUI's `EventSystem` uses
  `StandaloneInputModule` until then).
- Wiring frob into this repository and importing the Jira backlog as
  tickets: deferred, see the schedule.

## 7. Schedule

Dates are targets, not history: the port itself lands now (Sprint 1,
which ends 2026-09-28); everything after it is planned work that builds
on it. Sprint dates are from the Jira export.

| Target | Owner | Work | Jira |
| --- | --- | --- | --- |
| Sprint 1 (by 2026-09-28) | all five | U0-U4 above: IMGUI to uGUI port | SCRUM-108 (S33-2) |
| Sprint 2 week 1 (2026-10-05 to 10-09) | Derrick | Wire frob into the game repo (C# check stage, `frob.toml`, ticket queue) so `frob check` passes | - |
| Sprint 2 week 1 (2026-10-05 to 10-09) | Logan | Import the Jira backlog (epics, stories, PBIs, points, milestones) into the frob ticket queue | P2 process tasks |
| Sprint 2 week 1 (2026-10-05 to 10-09) | Steven | Title screen scene on the `HudCanvas` foundation | SCRUM-130 (S26-1), SCRUM-131 (S26-2) |
| Sprint 2 week 2 (2026-10-12 to 10-16) | Angie | Title screen art and layout; HUD art pass | SCRUM-132 (S26-3) |
| Sprint 2 week 2 (2026-10-12 to 10-16) | Steven | Settings screen; sign-in screen | SCRUM-133 (S27-1), SCRUM-100 (S07-2) |
| Sprint 2 week 3 (2026-10-19 to 10-23) | Derrick | Host/join lobby screen | SCRUM-124 (S28-2) |
| Sprint 2 week 3 (2026-10-19 to 10-23) | Angie | Colorblind-safe palette toggle (uses `HudColor`) | SCRUM-156 (S37-2) |
| Sprint 2 week 3 (2026-10-19 to 10-23) | Chase | Document the stress-to-color mapping shared by overlay and banner | SCRUM-157 (S37-3) |
| Sprint 3 (2026-11-02 to 11-20) | Steven | Queue screen; builder save/load UI | SCRUM-169 (S29-2), SCRUM-176 (S35-3) |
| Sprint 3 (2026-11-02 to 11-20) | Angie | Results screen; radar with hazard warning | SCRUM-122 (S46-3), SCRUM-200 (S45-2) |

## Jira stories this touches

From `jira-export.md` (project SCRUM,
https://aliens-against-humanity.atlassian.net/jira/software/projects/SCRUM/boards/1/backlog):
SCRUM-54 S33 "Choose from a block palette" (SCRUM-108, BuilderHud),
SCRUM-58 S37 "See where my ship is about to fail" (hull warning), and
epic SCRUM-15 E8 "Game shell" for the screens this foundation enables.
