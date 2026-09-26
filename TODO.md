# TODO

frob's ticket queue and strata model are wired into this repo (unit W,
docs/design/frob-and-backlog.md); `frob check` does not gate it yet (see
the frob bullet below), so deferred work still lives here instead of in
tickets.

- [x] CI: run the edit-mode tests without a Unity license. Done via
      `tools/plaincs`, which compiles the engine-free assemblies and the
      NUnit tests with the stock .NET SDK.
- [ ] CI: run play-mode tests with game-ci once we have a license secret
      (UNITY_LICENSE / UNITY_EMAIL / UNITY_PASSWORD in the repo secrets).
      Add the job to `.github/workflows/ci.yml` and to the `gate` job's
      needs list.
- [ ] CI: headless Linux dedicated-server build on every PR.
- [ ] Structure: a 500-block ship is not real-time. It costs ~100
      ms/tick (Release, this dev box) and does not converge inside
      `MaxCgIterationsPerTick` = 400 at all; 2000 blocks costs ~289
      ms/tick. Krylov continuation, the buckling budget and the coarse
      aggregate cap (perf/solver-ticks, 2026-09-21) each helped and none
      of them closes this: the remaining levers are the Burst/NativeArray
      port of the hot path and chunked solves, in that order. See
      docs/roadmap.md's Performance section for the measured table.
- [ ] Structure: `CgSolver.Converged` overstates its accuracy. The
      residual it reports is the RECURSIVE one (`r -= alpha * K p`), and
      at 810 dof in float32 it sits ~4 orders of magnitude below the true
      `|f - K u|` (5e-4 reported against 3.4 actual, at |f| = 58), which
      is PCG's attainable-accuracy floor at this conditioning. So
      "converged to 1e-5" has always really meant "converged to about 5%
      relative". Nothing downstream is obviously wrong because of it (the
      stress field is accurate to well under 1%), but `Tolerance` does
      not mean what its doc says. Fixing it honestly means double
      precision on the residual, or a restart-with-true-residual schedule
      inside a single solve. Found on perf/solver-ticks, 2026-09-21.
- [ ] Structure: the buckling tick still exceeds a 50 Hz frame on its
      own. It is down from 370-480 ms to 29-49 ms at 100 blocks
      (`BucklingAnalysis.MaxCgIterationsPerTick`), but it lands whole on
      every `BucklingEveryNTicks`-th tick instead of being spread across
      them. Splitting one sweep's per-vector solves across consecutive
      ticks, or raising `BucklingEveryNTicks`, would hide it.
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
- [x] UI: port the IMGUI HUD (`BuilderHud`, `DemoMode`'s status panel
      and hull banner) to uGUI prefabs. Done, U0-U4:
      [docs/design/ui-port.md](docs/design/ui-port.md).
- [ ] frob: `frob check` exits `CHECK001` ("unknown project type") on
      this repo until frob 0.534.0 ships the "unity"/"csharp" project
      types (frob T-6590, critical; the strata root-module
      parse gap that forced the hand-merge into
      `design/hullbreach_game.strata` is the same series, frob T-5198).
      `frob graph build` and `frob ticket` already work today; add a CI
      job that runs `frob check` once that release ships. See
      [docs/frob.md](docs/frob.md) and
      [docs/design/frob-and-backlog.md](docs/design/frob-and-backlog.md).
- [ ] frob: `frob sys audit` exits non-zero on the two ticket-bound
      SYS114 waivers (T-0041) because frob double-counts them as stale
      (frob T-6584, 0.534.0). The reliability view already
      reports them as waived; treat that exit as a known false gap.
- [ ] frob: `frob ticket new --points` drops the points (frob
      T-6586, 0.534.0); set points with `frob ticket points`.
- [ ] frob: the three Sprint 0 tickets (label `jira-status:done`) stay
      queued until frob can close pre-frob work without evidence
      (`close --historical`, frob T-6585, 0.535.0).
- [ ] frob: `frob:tests` bindings in Assets/Tests read as malformed
      because frob does not yet see Unity test asmdefs as the test side
      (frob T-6570, 0.534.0). Two bindings were removed from
      BuilderSessionTests and ShipBodyTests; re-add them after it lands.
