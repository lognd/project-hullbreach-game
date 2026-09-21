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
- [ ] Structure: `BucklingTests.SmallBlob_HasNoLowLoadFactor` still fails
      under the real Unity/Mono editor (passes under `tools/plaincs`/.NET).
      Root cause (confirmed via a captured Mono log): `BucklingAnalysis`'s
      Rayleigh-Ritz mu for a tracked slot can oscillate, sweep to sweep,
      between a normal value and a huge-but-finite one (a divide by a
      near-zero Cholesky pivot landing on ~1e4-1e6 instead of the intended
      near-zero/near-infinity sentinel; last-bit rounding differences
      between CLRs change which side of the pivot floor a given sweep lands
      on). Because `ForceConvergeAfterSweeps` eventually force-publishes
      whatever the CURRENT sweep computed regardless of this instability,
      the test's tick loop lands on the tail value of that oscillation.
      A magnitude cap on mu (tried, see `git log` on this file around
      2026-09-20) fixes this case but regresses
      `SolverHookHigh`/`ModeShape_IsOrthogonalToRigidBodyModes`, which
      legitimately produce large-but-STABLE mu for some sweeps; the two
      symptoms are only distinguishable by whether mu is stable or
      oscillating sweep to sweep, which needs `ForceConvergeAfterSweeps` to
      publish a snapshot from the last sweep that actually satisfied the
      ordinary tolerance check, not whatever the triggering sweep computed.
      That is a bigger refactor (a published vs. working copy of
      _v/_lambda) than fits opportunistically; do it deliberately.
- [ ] frob: add a C# check stage so `frob check` gates this repo the way
      it gates platform. Tracked in frob itself.
- [ ] Replace the template's FPS gameplay (weapons, character, spectator)
      with ship building and hull breaching.
- [ ] Server: report match results to the platform API over HTTPS.
- [ ] Decide on Unity Gaming Services vs. our own relay for matchmaking.

## Sprint status (as of 2026-09-20, branch physics-model)

Full detail, per-story pointers into the code, and known engineering debt
live in [docs/roadmap.md](docs/roadmap.md); this is the short version.

Done: S30-S33 (ship builder), S39 (flight), S36-S38 (structural sim,
Sprint-2 work landed early), S40 (gravity fields), S42 (basic cannon),
S43-S44 (gravity gun / anti-gravity gun / seeking thruster powerups,
Sprint-2/3 work landed early).

Partially done: S28/S47 (NetMessages and Quantization exist; no transport
or serializer yet).

Not started: S26/S27 (needs a UI scene), S29/S35/S49 (need the platform
repo), S34 (build during a fight), S41 (arena bounds), S45 (hazards),
S46 (win by breaching the core), S48, and the E14 stretch goals.

- [ ] Open RocketScene and DemoScene in Unity and confirm the hand-edited
      ShipController/BuilderController wiring, then commit the
      regenerated metas: see docs/demo-scene.md's checklist.
