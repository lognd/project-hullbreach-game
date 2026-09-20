# TODO

frob does not run on this repo yet (its check stage dispatches python,
typescript, cpp and rust only, even though it can parse C#), so deferred
work lives here instead of in tickets.

- [x] CI: run the edit-mode tests without a Unity license. Done via
      `tools/plaincs`, which compiles the engine-free assemblies and the
      NUnit tests with the stock .NET SDK.
- [ ] CI: run play-mode tests with game-ci once we have a license secret
      (UNITY_LICENSE / UNITY_EMAIL / UNITY_PASSWORD in the repo secrets).
      Add the job to `.github/workflows/ci.yml` and to the `gate` job's
      needs list.
- [ ] CI: headless Linux dedicated-server build on every PR.
- [ ] frob: add a C# check stage so `frob check` gates this repo the way
      it gates platform. Tracked in frob itself.
- [ ] Replace the template's FPS gameplay (weapons, character, spectator)
      with ship building and hull breaching.
- [ ] Server: report match results to the platform API over HTTPS.
- [ ] Decide on Unity Gaming Services vs. our own relay for matchmaking.

## Sprint 1 status (as of 2026-09-20, branch physics-model)

Done, with edit-mode tests under `Assets/Tests/EditMode`:

- S32 build around a core, S30 two-click placement, S31 remove and undo,
  S33 block palette: `Hullbreach.Builder` (PlacementRules, UndoStack,
  BuilderSession, BlockPalette) plus BuilderController and BuilderHud in
  `Hullbreach.Game`. Decision on S31: removing a block that strands others
  detaches the stranded blocks too; undo restores them as one action.
- S39 fly a ship that handles like it was built: `Hullbreach.Ship`
  (ShipBody, SteeringModel, BlockFacing) and ShipController, which
  replaces PlayerSingle in RocketScene. Decision on S39: fins are
  reaction-wheel style, so a drifting ship can still aim.
- Sprint-2 groundwork landed early: `Hullbreach.Structure` (Q8 FE,
  inertia relief, PCG, stress criteria, damage hysteresis, S36/S37 API).

Open for the rest of sprint 1:

- [ ] S26 title screen and S27 settings (needs a UI scene).
- [ ] S42 basic cannon: ShipBody records FireRequested; no projectile yet.
- [ ] S46 win by breaching the core: no match state machine yet.
- [ ] S28 LAN match and S47 authoritative server: NetMessages and
      Quantization exist; no transport is wired up.
- [ ] Open RocketScene in Unity and confirm the hand-edited ShipController
      and BuilderController wiring, then commit the regenerated metas.
