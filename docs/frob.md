# frob, for teammates who have never used it

frob is the tool wired into this repo by
[docs/design/frob-and-backlog.md](design/frob-and-backlog.md) (unit W). It
is three things at once, and only two of them do anything on this repo
yet:

1. **A ticket queue.** `tickets/T-####/ticket.md` files, git-tracked, one
   directory per ticket, with a small state machine (`queued` ->
   `planned` -> `in-progress` -> `done`, or `dropped`).
2. **A comment DSL.** Directives inside ordinary `//` comments that link
   code to docs, to tests, and to tickets, and that frob's graph can
   check for drift.
3. **Gates**, once C# check support lands (see "known blocker" below):
   `frob check` will refuse a commit whose comment directives, tests, or
   architecture model disagree with the code.

## The comment rules (D9)

This repo follows D9 in
[docs/design/ui-port.md#2-decisions](design/ui-port.md#2-decisions): code
comments are plain `//` lines, one or two of them, WHY not WHAT -- no XML
`<summary>` blocks. Every public type or member instead carries a
`// frob:doc docs/<page>.md#<anchor>` line pointing at a docs/ heading,
and that heading lists every symbol it documents with one
`<!-- frob:describes Assets/Scripts/<Asm>/<File>.cs::<Type>[.<Member>] -->`
line each. For example, `Assets/Editor/Hullbreach.Editor/HudPrefabBuilder.cs`
carries:

```csharp
// Screen Space Overlay canvas, 1920x1080 reference resolution, match 0.5 (D5).
// frob:doc docs/design/ui-port.md#hudprefabbuilder-editor
public static void BuildHudCanvasPrefab(bool force)
```

and `docs/design/ui-port.md`'s `#hudprefabbuilder-editor` heading carries
the matching `<!-- frob:describes ... -->` line. Until frob's check stage
can dispatch C# (see below), these links are not yet gated -- they are
just discoverable, greppable cross-references; write them anyway, the
gate is coming.

## Working a ticket

```bash
frob ticket list              # see what's queued
frob ticket doable            # queued/planned work with no open blockers
frob ticket show T-####       # read the Description/Plan/Failure log in full
frob ticket start T-####      # claim it (a write lease on its declared scope)
# ... implement, adding // frob:ticket T-#### etc. as you go ...
frob ticket evidence T-#### NODE-ID --accepts 1   # bind a passing test/check
frob ticket done-report T-#### --why-file why.md
frob ticket close T-####      # re-verifies the evidence and the report
```

No tickets exist in this repo yet (unit B, the Jira import, comes after
unit W). `frob ticket list` runs cleanly against an empty queue today;
that is expected.

## `frob:doc` / `frob:describes` and docs/reference/

`docs/reference/<assembly>.md` (one page per assembly, skeletons added by
unit P) is the target of every `// frob:doc` line in the code: one
heading per type, and a `<!-- frob:describes ... -->` line under that
heading for each symbol the code links to it. `frob:doc` says "this
symbol is described there"; `frob:describes` says "this doc heading
describes that symbol" -- frob's graph checks that both ends agree once
its check stage runs here, so a heading that gets renamed or a symbol
that gets moved shows up as drift instead of a silently stale link.

## Design notes for `design/hullbreach_game.strata` and `frob.toml`

The comment blocks in both files stay to one or two `// `/`# ` lines
each (the same D9 rule as code); this section is where the rest of the
"why" that used to live in those comments now lives.

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

## Known blocker

`frob check` exits `CHECK001` ("unknown project type") on this repo
today, because frob's check stage dispatches only python/typescript/
cpp/rust project types, not Unity/C# yet, even though its underlying
parser already understands C#. This is a real gap in frob itself, not a
misconfiguration here: the frob maintainers confirmed it on 2026-09-26.
It is tracked upstream as frob T-draft-fcfdafdf (critical, targeted for
frob 0.534.0, adding "unity" and "csharp" project types with the test
step read from this repo's own `[[test.runner]]`); the strata
root-module parse gap that made the hand-merged `design/hullbreach_game.strata`
necessary in the first place is the same series, frob T-5198. Until
0.534.0 ships, `frob check` fails fast on CHECK001 before running
anything else, so there is nothing else to gate yet -- but
`frob graph build` (the strata model) and `frob ticket` both work fully
today, and `tools/plaincs/run_tests.sh` plus
`scripts/check_unity_tree.sh` are what actually verify this repo in the
meantime. A CI job that runs `frob check` lands once a frob release
dispatches C# (see [TODO.md](../TODO.md)).
