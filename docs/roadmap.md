# Roadmap

Sprint status against the GitHub story numbers (`gh issue list --state all`;
all 32 stories/epics are open issues: there is no "closed" workflow on
this tracker, status lives here instead), each with a pointer to the code
it would build on. Sprint 1 is 9/16-9/27, Sprint 2 is 10/5-10/23, Sprint 3
is 11/2-11/20 (`README.md`). Branch `physics-model` (53 commits ahead of
`main` as of 2026-09-20) already contains Sprint-1 work plus a large chunk
of Sprint-2 groundwork landed early.

## Sprint 1 (Game shell / Ship builder / Flight physics / early Combat)

| Story | Status | Code |
| --- | --- | --- |
| #3 S26 title screen | Not started | Needs a UI scene; nothing under `Assets/Scenes` but `RocketScene.unity`/`DemoScene.unity`. |
| #4 S27 settings | Not started | Same UI-scene dependency as S26; also the natural point to migrate off the legacy `Input` class (see engineering debt below). |
| #5 S28 LAN match | Partially done | `Hullbreach.Net.NetMessages`/`Quantization` exist as wire formats and quantization helpers; no transport (Netcode for Entities/UGS relay/etc.) is wired to anything. See `docs/architecture.md`'s "server authority" section for the design NetMessages already commits to ("send causes, not effects"). |
| #6 S29 online opponent | Not started, `needs-platform` | Depends on the platform repo's matchmaking API. |
| #8 S30 place a block, two clicks | **Done** | `Hullbreach.Builder.PlacementRules`/`BuilderSession`, `Hullbreach.Game.BuilderController`. Edit-mode tests: `Assets/Tests/EditMode/Hullbreach.Builder.Tests/PlacementRulesTests.cs`, `BuilderSessionTests.cs`. |
| #9 S31 remove and undo | **Done** | `PlacementRules.Detach` + `Hullbreach.Builder.UndoStack`. Decision recorded in `TODO.md`/`docs/architecture.md`: a removal that strands blocks detaches them too, undone as one action. Tests: `UndoStackTests.cs`. |
| #10 S32 build around a core | **Done** | `PlacementRules.CanPlace`'s core-seeding rule (empty grid accepts only a core). |
| #11 S33 block palette | **Done** | `Hullbreach.Builder.BlockPalette`, `Hullbreach.Game.BuilderHud`. Tests: `BlockPaletteTests.cs`. |
| #19 S39 fly a ship that handles like it was built | **Done** | `Hullbreach.Ship.ShipBody`/`SteeringModel`/`BlockFacing`, `Hullbreach.Game.ShipController` (replaces `PlayerSingle`/`RocketScene`'s prototype). Decision recorded in `TODO.md`: fins are reaction-wheel style (steer while drifting). Tests: `Hullbreach.Ship.Tests/ShipBodyTests.cs`, `ThrusterUpgradesTests.cs`. |
| #23 S42 shoot a basic cannon | **Done** (TODO.md is stale on this one, see below) | `Hullbreach.Ship.Behaviours.CannonBehaviour`/`CannonBehaviourBase` records a `ShotRequest`; `Hullbreach.Game.ProjectileSpawner`/`Projectile` actually spawn and simulate the round, apply damage via `ShipCollider`, and despawn on lifetime/impact. Wired into `DemoScene.unity` (`Demo`'s `WorldSink`/`ProjectileSpawner`). |
| #27 S46 win by breaching the core | Not started | No match state machine exists. The building block is there: `Hullbreach.Core.BlockGrid.CoreKey` plus `Hullbreach.Core.Topology.Connectivity`/`Articulation` can already detect "the core is gone" or "the core is isolated from the rest of the ship": a win condition would poll `CoreKey.HasValue` (or watch for it going stranded) per ship and end the match, but nothing currently calls that. |
| #29 S47 run matches on an authoritative server | Partially done | `Hullbreach.Net.NetMessages`'s doc comment already specifies the wire protocol design (`ShipSnapshot`/`ShipState`/`BlockPlaced`/`BlockDestroyed`/`FragmentSpawned`, all TODOs, no serializer written yet) and `Quantization` has the position/angle quantization helpers. `ShipBody`/`StructuralSolver` are engine-free by design specifically so a headless server process can run them (`docs/architecture.md`'s "the one rule"), and `StructuralSolver.BuckledBlocks` / `ShipStructure.Authoritative` already encode the server-authority split. What is missing: an actual transport, a server loop that ticks `ShipBody`/`StructuralSolver` without Unity's scene, and the message serialization itself. |

## Sprint 2 (Structural simulation / more physics / real internet)

| Story | Status | Code |
| --- | --- | --- |
| #12 S34 build during a fight | Not started | `DemoMode` treats Build and Fly as mutually exclusive states (`Tab` toggles), not simultaneous; `BuilderController`/`ShipController` would both need to run at once, which nothing currently supports. |
| #13 S35 save/load ship designs | Not started, `needs-platform` | No serialization of a `BlockGrid` to/from a design format exists yet; would likely reuse `NetMessages`'s planned `ShipSnapshot` layout. |
| #14/#15/#16/#17 E10 Structural simulation (S36 compute stress, S37 see failure coming, S38 break apart) | **Done, landed early** | The entire `Hullbreach.Structure` assembly: `Q8Element`/`NodeLattice`/`StiffnessAssembly`/`CgSolver`/`LoadVector` (FE core), `StressCriteria`/`DamageModel` (S36/S37 criteria), `BucklingAnalysis`/`GeometricStiffness` (buckling), tied together by `StructuralSolver`. S37's "see it coming" is the Stress/LoadBearing/Buckling overlays in `ShipRenderer`. S38's "break apart" is `Hullbreach.Game.ShipStructure` detaching blocks once a ratio exceeds 1 or a block is in a sub-critical buckling mode. Full pipeline described in `docs/architecture.md`. |
| #20 S40 fight inside a gravity field | **Done** | `Hullbreach.World.GravityField`/`GravityBody`/`OrbitHelper`, `Hullbreach.Game.GravityWorld`, `ShipBody.ApplyGravityForces`/`ResolvePlanetContacts`. Two planets in `DemoScene.unity`. |
| #21 S41 stay inside the arena | Not started | No arena-bounds concept anywhere in `Hullbreach.World`/`Hullbreach.Game`: a ship or projectile can fly arbitrarily far from the planets with nothing stopping it (aside from `BlockKey`'s own -128..127 grid-coordinate range, which bounds a *ship's own blocks*, not world-space position). |
| #24 S43 gravity gun / anti-gravity gun | **Done, landed early** | `Hullbreach.Ship.Behaviours.GravityGunBehaviour`/`AntiGravityGunBehaviour` (Cannon variants 1/2), `IWorldSink.AddTemporaryGravity`, powerup pickups in `DemoScene.unity`'s `Powerups` object. |
| #25 S44 fire the Inconvenient Thruster | **Done, landed early** | `Hullbreach.Ship.Behaviours.SeekingThrusterBehaviour` (Thruster variant 1), the third `Powerups` preset. |
| #26 S45 dodge telegraphed hazards | Not started | No hazard system exists; would likely be a new `Hullbreach.World` or `Hullbreach.Game` component analogous to `GravityBody`/temporary gravity wells, with a telegraph/warning phase before it activates. |
| #30 S48 keep the game fluid over the internet | Not started | Depends on S47's transport landing first; `Quantization`'s helpers are the piece already in place. |

## Sprint 3 (Store, admin, hazards, polish, stretch)

| Story | Status |
| --- | --- |
| #31 S49 favor the defender / forgive lag | Not started, `needs-platform` |
| #33/#34 S50/S51 (E14 stretch goals) | Not started |

## Known engineering debt

- **CG iteration cap and per-tick cost.** `CgSolver` (`Hullbreach.Structure.Fem.CgSolver.cs`)
  has a fixed iteration cap and no adaptive early-out beyond a stagnation
  guard (`fix(structure): raise CgSolver's iteration cap and add
  stagnation guard`, commit `8a17c34`); a large enough ship's per-tick
  solve cost has not been profiled against a real frame budget.
- **Buckling uses element-center stress**, not a proper stress-recovery
  extrapolation to nodes (`StructuralSolver.ComputeBlockStress` evaluates
  at `xi = eta = 0`); see `docs/architecture.md`'s structural-pipeline
  section. This is a known simplification, not a bug, but it means the
  buckling/stress tints are coarser than a full FE post-process would give.
- **No Input System migration.** `activeInputHandler: 2` ("Both") lets the
  legacy `Input`/`GetButton` calls in `ShipController`/`DemoMode` keep
  working alongside the installed `com.unity.inputsystem` package; S27
  (rebindable keys) is the natural moment to migrate, and has not happened.
- **Scene YAML is hand-authored**, never opened in the Unity editor.
  `RocketScene.unity`'s `ShipController`/`BuilderController` wiring and
  the entirety of `DemoScene.unity` were hand-written (`TODO.md`, commit
  `f680856`'s body). See `docs/demo-scene.md`'s "first open in Unity"
  checklist.
- **`Rigidbody2D` is driven, not simulated.** `ShipBody` is the actual
  physics (semi-implicit Euler integration, its own gravity and contact
  resolution); the `Rigidbody2D` component on a ship's GameObject exists
  for Unity's own collision/trigger events (`ShipCollider`, `Powerup`'s
  `OnTriggerEnter2D`) and is driven from `ShipBody`'s state rather than
  contributing its own physics. If Unity Physics2D and `ShipBody` ever
  disagree about position/velocity, `ShipBody` wins; nothing currently
  guards against the two drifting apart other than "one writes, one reads".
- **Damage-only K rescale, not implemented as a separate pass.**
  `BlockTypes.EffectiveStiffness` folds damage softening directly into the
  stiffness lookup every tick (floored at 5% of nominal `E`); there is no
  standalone "rescale K only when damage actually changed" optimization,
  so a fully-undamaged ship pays the same softening-lookup cost as a
  heavily damaged one. Noted as a placeholder in `BlockTypes.cs`'s `TODO
  [D3]` comment on the floor value.
