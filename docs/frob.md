# frob, for teammates who have never used it

frob (v2, 0.532.0, a single Rust binary) is the tool wired into this repo
by [docs/design/frob-and-backlog.md](design/frob-and-backlog.md) (unit W).
It is three things at once, and only two of them are fully useful on a
C# repo yet:

1. **A ticket ledger.** `tickets/<ULID>/ticket.md` plus append-only
   `events/*.toml` files, git-tracked. Categories are triage, todo,
   in-progress, done; a done ticket carries an outcome (fixed, wont-fix,
   duplicate, invalid, done). Each ticket has a short handle such as
   `~XC7G41Z`; the old v1 ids (`T-0041`) are kept as aliases, so
   `frob ticket show T-0041` still works.
2. **A comment DSL.** Directives inside ordinary `//` comments that link
   code to docs, to tests, and to tickets (`frob:doc`, `frob:tests`,
   `frob:ticket <ulid>`, `frob:todo <ulid>`).
3. **Gates**: `frob check` reports rule findings. C# is fidelity F1 in
   0.532.0 (see "Known gaps" below), so several rules cannot see this code
   yet.

Install the pinned version the same way CI does:
`uv tool install frob==0.532.0`. Run `frob init` once per clone: it
installs the `frob-ledger` merge driver into your local git config (the
`.gitattributes` rules are committed).

## The comment rules (D9)

This repo follows D9 in
[docs/design/ui-port.md#2-decisions](design/ui-port.md#2-decisions): code
comments are plain `//` lines, one or two of them, WHY not WHAT -- no XML
`<summary>` blocks. Every public type or member instead carries a
`// frob:doc docs/<page>.md#<anchor>` line pointing at a docs/ heading,
and that heading lists every symbol it documents with one
`<!-- describes: Assets/Scripts/<Asm>/<File>.cs::<Type>[.<Member>] -->`
line each. For example, `Assets/Editor/Hullbreach.Editor/HudPrefabBuilder.cs`
carries:

```csharp
// Screen Space Overlay canvas, 1920x1080 reference resolution, match 0.5 (D5).
// frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
public static void BuildHudCanvasPrefab(bool force)
```

and `docs/design/ui-port.md`'s `#hudprefabbuilder-editor` heading carries
the matching `<!-- describes: ... -->` line. Until frob's check stage
can dispatch C# (see below), these links are not yet gated -- they are
just discoverable, greppable cross-references; write them anyway, the
gate is coming.

## Working a ticket

```bash
frob ticket list                       # what exists (--category todo, --label ...)
frob ticket doable                     # todo work with no open blockers
frob ticket show T-0041                # read it in full (a handle or ULID works too)
frob work T-0041                       # lease it, make its worktree and branch
frob work --here T-0041                # lease it in this checkout, no new worktree
# ... implement, adding // frob:ticket <full-ULID> where useful ...
frob ticket evidence add --provider command --ref "dotnet test ..." T-0041 --accepts 1
frob ticket close --outcome fixed T-0041   # re-verifies evidence and the definition of done
frob land                              # land the ticket's branch
```

Other verbs you will use: `frob board`, `frob requeue --reason ... T`,
`frob ticket comment T`, `frob ticket update T --points N`, `frob cycle show`
(Sprint 2 is the active cycle, 2026-10-05 to 2026-10-23), `frob doctor`,
`frob ticket doctor`.

`ref_mode = "branch"` in `frob.toml` means ledger commits land on your
working branch and travel in the pull request; `main` stays PR-only. On a
feature branch, `frob ticket doctor` reads the ledger at the checked-out
branch.

`cycle assign` needs story points. Eleven Sprint 2 stories have none and
are not in the cycle (they are containers for their pointed children);
size them with `frob ticket update T --points N` and assign them.

### Recording test evidence

`frob test` does not run C# yet and v2 has no `[[test.runner]]` table. The
plain-C# NUnit harness is `tools/plaincs/run_tests.sh` (CI runs it). To
record it against a ticket use the `command` provider, whose first word
must be in `[evidence] allowed_tools` (`dotnet` is by default):

```bash
tools/plaincs/fetch_deps.sh
frob ticket evidence add --provider command T-0058 \
  --ref "dotnet test tools/plaincs/Hullbreach.Plain.Tests/Hullbreach.Plain.Tests.csproj -c Release --filter TestCategory!=Slow"
```

`run_tests.sh` is also on the allowlist (by bare name: 0.532.0 refuses a
path with a slash), so with `tools/plaincs` on `PATH` the whole harness can
be bound, optionally narrowed to one test class:

```bash
PATH="$PWD/tools/plaincs:$PATH" frob ticket evidence add --provider command T-0058 \
  --ref "run_tests.sh --filter FullyQualifiedName~ServerHardeningTests"
```

The `dotnet` provider that `[evidence.dotnet]` configures is not offered by
`frob ticket evidence add` in 0.532.0, and the `unity` pack/provider
(batch-mode Unity test runs) is design only until a later release.

## `frob:doc` / `describes:` and docs/reference/

`docs/reference/<assembly>.md` (one page per assembly, skeletons added by
unit P) is the target of every `// frob:doc` line in the code: one
heading per type, and a `<!-- describes: ... -->` line under that
heading for each symbol the code links to it. `frob:doc` says "this
symbol is described there"; `describes:` says "this doc heading
describes that symbol". It is deliberately NOT a `frob:` directive: v2
has no such verb and reported every line as DSL001, so it is a plain
comment convention until frob adds one. The intent is that frob's graph checks both ends agree, so a heading that gets renamed or a symbol
that gets moved shows up as drift instead of a silently stale link.

## Design notes for `design/hullbreach_game.strata`

`design/hullbreach_game.strata` is the v1 architecture model (strata).
frob v2 does not read `.strata` files (its model language is grimble,
`design/model.grmb`), so the file is kept as documentation only and the
v1 `frob graph build` / `frob sys audit` steps no longer exist. The notes
below explain what it encodes; porting it to grimble is future work.

- **The hand-merge.** `frob scaffold unity-project .` writes one
  `design/unity_*.strata` fragment per `.asmdef`; none of them parse on
  their own (frob T-5198: no root module). The frob maintainers
  confirmed on 2026-09-26 that hand-merging them into one file with a
  single `module hullbreach_game` line stays compatible with their
  eventual fix, so that is what `design/hullbreach_game.strata` is:
  every fragment's node and flow, renamed to short, per-asmdef names
  (`core`, `world`, `hud`, ...) instead of the generator's
  `unity_hullbreach_*` prefix, under one module.
- **The one rule, encoded.** `docs/architecture.md`'s "one rule"
  (engine-free simulation, `Hullbreach.Game` is the only assembly that
  touches `UnityEngine`) is enforced in the model by absence: every
  `local` flow in the file points AWAY from `game`, never into it. A
  new reference from an engine-free assembly to `Hullbreach.Game` would
  show up as an unmodeled flow targeting `game`.
- **`net`'s planned platform flows.** `f_login_game`/`f_session_check`
  mirror the platform's own `f_login_game`/`f_session_check` in
  `platform/design/hullbreach.strata` (sourced there from `game_client`/
  `game_server`). Neither call site exists in this repo yet -- Jira
  SCRUM-100 (S07-2, the sign-in screen) is the tracked work -- so both
  are sourced from `net` (the assembly that already owns client/server
  networking) and their `REL200` findings are `waive`d as planned,
  using the same idiom the platform model uses for its own
  planned-vs-landed gaps. The waive has to sit on the flow's SOURCE
  node (`net`), not its destination (`hullbreach_platform_api`) --
  waiving on the destination silently fails to match the finding.
- **`editor`'s capabilities.** `HudPrefabBuilder` (D6,
  `ui-port.md#2-decisions`) writes the generated `.prefab` assets to
  disk, hence the two `may "fs.write"` grants. Its public entry points
  are also run headlessly via Unity's own `-executeMethod`, an
  out-of-process invocation with no single call-site line to `via`,
  hence the blanket `may "eval"`. That capability drags in a CWE-78
  obligation under the `owasp-top-10` audit view; there is no inbound
  flow into `editor` in this model at all, so the `assume ... noflow`
  claim (the same idiom frob's own `design/frob.strata` uses for its
  `eval` grants) discharges it honestly.
- **`demo_tests`'s capabilities.** `DemoScreenshots` reads the
  `HULLBREACH_SHOTS` env var to find its output directory, then writes
  screenshots into it -- hence its `may "env.read"`/`may "fs.write"`.
- **Known frob-side gap.** `frob sys audit` still reports `SYS114` (no
  proven config-bound host constraint) on both planned platform flows.
  Unlike `REL200`, there is no `waive`/planned-discharge idiom for
  `SYS114` yet, so a flow to a foreign node with no code behind it
  cannot pass that check today -- reported to the frob maintainers, not
  worked around here.

## Known gaps in frob 0.532.0 on this repo

- `frob check` exits 0 (errors cleared; the DSL001 findings went away
  when `frob:describes` became the plain `describes:` convention).
  Remaining are warnings only: COV001 (public methods "reached by no
  test" because C# test detection is a Gap) and opaque-file Unresolved
  notes (none required).
- C# is fidelity F1: `resolve_ref` and `test_items` are Gaps, so a
  `frob:tests` binding on a C# test is reported TEST001 ("names no test
  function in the graph"). This repo carries no such bindings now; do not
  add them until frob resolves C# tests.
- `frob:todo`/`frob:ticket` in code need a full 26-char ULID; alias
  resolution for v1 ids in directives is pending upstream.
- `frob test` and `frob coverage` do not run C#; there is no `frob
  coverage` in v2 (COV001 in `frob check` is the coverage rule) and no
  `frob ticket sweep` (use `frob ticket doctor`).
- CI runs `frob check` as a non-blocking job until these clear (see
  [TODO.md](../TODO.md)). `scripts/check_unity_tree.sh` and
  `tools/plaincs/run_tests.sh` remain what actually verify the repo.
