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

CI installs the frob dev pre-release (x86_64 linux `frob-cli`, sha256
checked) with `scripts/install_frob_dev.sh`; locally use any build that
reports a `0.532.0-dev` version (or `uv tool install frob==0.532.0`, which
cannot bind C# tests). Run `frob init` once per clone: it
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

`frob test --base <ref>` selects C# tests by owning project; a test under a
Unity `.asmdef` assembly (all of `Assets/Tests/EditMode/**`) is routed to a
`unity` provider that frob 0.532.0 does not implement, so it refuses with
`E-TESTS-UNITY-PROVIDER`. Do not wait for it: the same EditMode sources are
compiled into `tools/plaincs`, so the `run_tests.sh` command above is the
honest, real-run evidence for them (it records the NUnit pass/fail output
verbatim; it never relabels another provider). Upstream gap: ticket ~VPQPZJ0.
A ticket with no acceptance
criteria closes with a `criteria_evidenced passed vacuously` warning, so
give a ticket criteria before relying on its evidence.

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

## Design notes for `design/hullbreach_game.grmb`

`design/hullbreach_game.grmb` is the architecture model in grimble (frob
v2's model language; registered in `grimble.toml`). It was ported from
the v1 `design/hullbreach_game.strata`, which is deleted. The notes below
explain what it encodes and what grmb could not carry over.

- **The hand-merge.** `frob scaffold unity-project .` writes one
  `design/unity_*.strata` fragment per `.asmdef`; none of them parse on
  their own (frob T-5198: no root module). The model is those fragments
  hand-merged into one file with a single `module hullbreach_game;`,
  every node and flow renamed to short per-asmdef names (`core`,
  `world`, `hud`, ...). Each local flow now binds `producer`/`consumer`
  to the two `.asmdef` files so SYS004 (flow without code) is satisfied;
  the `Assembly references` rationale sits in a `///` doc comment.
- **The one rule, encoded.** `docs/architecture.md`'s "one rule"
  (engine-free simulation, `Hullbreach.Game` is the only assembly that
  touches `UnityEngine`) used to be enforced by absence of flows into
  `game`. It is now also stated as checked claims `one_rule_<node>`
  (`noflow <node> -> game; proof L1;`) for core, world, structure, ship,
  builder, hud and net.
- **`net`'s planned platform flows.** `f_login_game`/`f_session_check`
  mirror the platform's flows of the same names. Neither call site
  exists yet (Jira SCRUM-100, S07-2), so the flows carry
  `accept SYS004` and a `planned` attr; `rate 1 req/s` is a placeholder
  needed so a foreign-source flow passes FLOW003. The v1 `REL200` and
  `SYS114` waives (ticket T-0041) have no equivalent: those rules are
  gone in v2.
- **`editor`'s capabilities.** `HudPrefabBuilder` (D6,
  `ui-port.md#2-decisions`) writes generated `.prefab` assets, hence the
  two `may fs.write` grants. Its entry points are also run headlessly
  via Unity's `-executeMethod`; v1 modelled that as `may "eval"`, which
  has no atom in v2 (see gaps). The CWE-78 discharge is now the claim
  `cwe_78_editor` (`noflow hullbreach_platform_api -> editor`), assumed
  by logan.
- **`demo_tests`'s capabilities.** `DemoScreenshots` reads the
  `HULLBREACH_SHOTS` env var and writes screenshots there; only the
  `fs.write` grant survives (the env read has no atom).

### Grimble gaps

- `because="..."` and `ticket="..."` after `may ATOM at SELECTOR` fail
  to parse (MDL009), contradicting the spec; grant reasons live in `//`
  comments instead.
- No atom for environment reads (`env.read`, `process.env`, ... all
  MDL016) or `eval`; v1's `may "env.read"` and `may "eval"` were
  dropped and are recorded as comments in the file.
- All gaps are as of grimble 0.532.0.
- No per-flow waive for the v1 REL200/SYS114 planned-flow findings;
  only `accept SYS004` remains.
- The CWE-78 claim's `review` date was moved from 2026-10-15 (already
  expired) to 2027-01-15 so it is not instantly stale.
- SYS001 warnings remain for files outside any `owns` glob
  (Hullbreach.Match, Platform, NetCode.Entities, docs, tools). Those
  assemblies were never modeled in v1; modelling them is separate work.

## Known gaps in frob 0.532.0 on this repo

- `frob check` exits 0 (errors cleared; the DSL001 findings went away
  when `frob:describes` became the plain `describes:` convention).
  Remaining are warnings only: COV001 (public methods "reached by no
  test" because C# test detection is a Gap) and opaque-file Unresolved
  notes (none required).
- C# tests bind with the dev build (not PyPI 0.532.0, which reports TEST001
  "names no test function in the graph"). Put
  `// frob:tests <path>.cs::<Namespace.Class.Method> kind="unit"` as a
  line INSIDE the `[Test]` method body (see ConnectRequestTests and
  ArenaBoundsTests). A receiver of unknown type (for example two classes
  with a `Describe` method) can still leave COV001 as Unresolved.
- `frob test --base` now selects the EditMode tests but runs them through
  the `unity` provider, which needs the Unity editor named in
  ProjectSettings/ProjectVersion.txt (E-EVIDENCE-UNITY-EDITOR without it);
  record evidence with the `command` provider and `run_tests.sh` as above.
- `frob:todo`/`frob:ticket` in code need a full 26-char ULID; alias
  resolution for v1 ids in directives is pending upstream.
- `frob test` and `frob coverage` do not run C#; there is no `frob
  coverage` in v2 (COV001 in `frob check` is the coverage rule) and no
  `frob ticket sweep` (use `frob ticket doctor`).
- CI runs `frob check` as a non-blocking job until these clear (see
  [TODO.md](../TODO.md)). `scripts/check_unity_tree.sh` and
  `tools/plaincs/run_tests.sh` remain what actually verify the repo.
