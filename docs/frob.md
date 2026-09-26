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
