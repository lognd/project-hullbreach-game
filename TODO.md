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
- [ ] Structure: `CgSolver.Solve` restarts its Krylov subspace (`r`/`p`)
      from scratch every `Tick` call, warm-starting only the displacement
      `u`. This is the suspected reason the 500- and 2000-block
      plate+arm `SolverBenchmarks` cases plateau (residual oscillates
      instead of trending toward zero across ticks) instead of eventually
      converging under `MaxCgIterationsPerTick`'s carried-over budget, even
      with `CoarsePreconditioner`'s deflated coarse correction in place
      (perf/preconditioner branch, 2026-09-20; see docs/roadmap.md's
      Performance section). Preserving `r`/`p`/`rzOld` across ticks
      (re-validated against the current `f`, since the load can change
      tick to tick) is the next thing to try before reaching for a bigger
      iteration cap or a wall-clock budget.
- [ ] Structure: `SolverBenchmarks.Benchmark_100Blocks` shows a periodic
      per-tick spike (~370-460ms every `BucklingEveryNTicks`-th tick vs.
      ~19-40ms otherwise) even though the benchmark's load never puts
      anything into compression, so `RunBuckling` should be hitting its
      cheap early-out on every one of those ticks, not the subspace
      machinery. Suspected GC pause, not confirmed (perf/preconditioner
      branch, 2026-09-20; see docs/roadmap.md's Performance section).
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
