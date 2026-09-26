# Design: wire frob in, sweep the comments, import the Jira backlog

Status: accepted before implementation. This page specs three pieces of
work that were deferred at the end of the [UI port](ui-port.md#7-schedule)
and are now being pulled forward: W (frob wiring), C (comment sweep) and
B (backlog import). Related: [architecture](../architecture.md),
[testing](../testing.md), the platform repo's
[picking-up-work guide](https://github.com/lognd/project-hullbreach-platform/blob/main/docs/picking-up-work.md),
and the Jira board
(https://aliens-against-humanity.atlassian.net/jira/software/projects/SCRUM/boards/1/backlog).

## 1. Owners and branches

Authorship follows the Jira areas (team section of `jira-export.md`).
Each unit lands on its owner's branch, merges to `main`, and `main` is
merged back into every open branch.

| Unit | Owner (branch) | Repo |
| --- | --- | --- |
| P | Logan (`lognd/frob-backlog-plan`) | game: this page, reference-page skeletons |
| W | Derrick (`mcnairrobotics/frob-wiring`) | game |
| C1 | Chase (`GingerVHS/comment-sweep`) | game: Structure, Ship, World + their tests |
| C2 | Steven (`stevendangkhoi/comment-sweep`) | game: Core, Builder, Editor + their tests |
| C3 | Angie (`a-carten/comment-sweep`) | game: Game (except NetDemo) |
| C4 | Derrick (`mcnairrobotics/comment-sweep`) | game: Net, NetDemo, Net tests, play-mode tests |
| B0 | Derrick (`mcnairrobotics/backlog-epics`) | game: game epics and stories |
| B1-B5 | each person (`<handle>/backlog-pbis`) | game: that person's PBIs |
| B6 | Logan (`lognd/jira-reconcile`) | platform: map Jira onto the existing tickets, add what is missing |

## 2. W: frob in the game repo

- `frob scaffold unity-project .` produces `frob.toml` and one
  `design/unity_*.strata` fragment per asmdef. The fragments do not parse
  yet (frob T-5198: no root module), so W hand-merges them into one
  `design/hullbreach_game.strata` with `module hullbreach_game` and
  deletes the fragments.
- The strata model encodes [the one rule](../architecture.md#the-one-rule-engine-free-simulation-unity-only-adapters):
  one node per asmdef, `local` flows only along the asmdef references, so
  a new reference from an engine-free assembly to `Hullbreach.Game` is a
  model violation. The platform is a `foreign` node
  (`hullbreach_platform_api`) with the two flows the platform's own model
  already declares from its side (`f_login_game`, `f_session_check`, see
  the platform's `design/hullbreach.strata`); both are marked planned
  until S07-2 lands.
- `[[test.runner]]` for csharp runs `tools/plaincs/run_tests.sh`, not the
  scaffold's `dotnet test .` (a Unity repo has no project file at the
  root).
- `.gitignore` gains the global block (`.frob/`, `FROBLEMS.md`, `.env`,
  build caches).
- `docs/frob.md`: what frob is, the comment directives this repo uses
  (D9 in [ui-port.md](ui-port.md#2-decisions)), and the ticket workflow
  for teammates.
- Known blocker: `frob check` exits with CHECK001 "unknown project type"
  on any Unity repo, because frob's check stage dispatches only
  python/typescript/cpp/rust. The frob maintainers confirmed it on
  2026-09-26 and are adding "unity" and "csharp" project types (frob
  0.534.0) with the test step read only from `[[test.runner]]`. W lands everything else; CI gets a `frob check` job once
  a frob release dispatches C#. Until then, `frob graph build` must
  complete with zero strata parse failures, and `frob ticket` must work.

## 3. C: comment sweep

The rule is D9 in [ui-port.md](ui-port.md#2-decisions): no XML doc comments,
`//` comments of one or two lines and only for WHY, and
`// frob:doc docs/<page>.md#<anchor>` above every public symbol, with a
matching `<!-- frob:describes Assets/Scripts/<Asm>/<File>.cs::<Sym> -->`
in the docs page.

- Each owner gets one reference page, `docs/reference/<assembly>.md`
  (skeletons added by P), with one heading per type. Long rationale that
  lives in a `<summary>` today moves there, under the type's heading,
  instead of being deleted. Nothing true is lost; it moves to the page
  the code links to.
- Private members lose their `<summary>` blocks. Keep a one- or two-line
  `//` only where the WHY is not obvious from the code.
- Tests need no `frob:doc`; summaries on tests become a one-line `//`
  or disappear.
- No behavior change: `tools/plaincs/run_tests.sh` passes with the same
  count before and after, and the diff contains only comment lines and
  docs.

## 4. B: Jira to frob tickets

### Mapping

| Jira | frob |
| --- | --- |
| Epic (E1-E14) | `--tier epic` |
| Story (Sxx) | `--tier story`, `--parent` = its epic |
| Subtask / PBI (Sxx-n) | `--tier ticket`, `--parent` = its story, `--points` = Jira points |
| Process task (Pn-m) | `--tier ticket` under a "Process" epic in the platform repo |
| Sprint 1 / 2 / 3 | `--sprint sprint-1/2/3` and `--milestone 0.1.0/0.2.0/0.3.0` (the platform's existing convention) |
| Backlog / stretch | no sprint, no milestone, label `stretch` |
| Jira key | label `jira:SCRUM-<n>`; the body starts with the browse link `https://aliens-against-humanity.atlassian.net/browse/SCRUM-<n>` |
| Assignee (or "X per backlog doc") | label `owner:<handle>` |
| Jira labels (game, physics, ...) | the same labels |
| Story card and conversation | ticket body |
| Confirmation criteria | one `--acceptance` each |
| Priority Highest/High/Medium/Low | critical/high/medium/low |

Points go on the leaf PBIs only. A story's points are the sum of its
PBIs, the same convention the Jira export uses, and frob's
`ticket epic` rollup shows that sum. All Jira points are Fibonacci
(1, 2, 3, 5, 8), which frob requires.

### Routing

- Stories labelled `game` (E8-E14) go to the game repo.
- Stories labelled `platform` (E1-E7) go to the platform repo.
- A story labelled `game, platform` gets a game-side story here and keeps
  (or gains) a "(platform half)" story in the platform repo, which is the
  pattern the platform already uses (e.g. T-0086 for S29). Each PBI goes
  to the half its summary describes.
- Sprint 0 (SCRUM-5/6/7, Done in Jira) goes to the game repo and is
  closed immediately with `--no-behavior-change` and the reason "done in
  Jira before frob".
- Jira status is the source of truth for state: everything else is
  `queued`. Where [roadmap.md](../roadmap.md) says the code already
  exists, that is noted in the ticket body. The owner closes the ticket
  with test evidence once `frob check` runs here; B does not close it.
- The platform repo already holds the Module 4 backlog (T-0004 to
  T-0094). B6 maps each Jira PBI onto an existing ticket where the scope
  matches: it adds the labels and points, and fixes sprint and milestone
  drift. It files new tickets only for PBIs with no match, and adds a
  `jira:none` label to existing tickets that have no Jira counterpart.
  It never renumbers or deletes.

### Authorship and order

`frob ticket` commits each change itself. Every command runs with
`GIT_AUTHOR_NAME/EMAIL` and `GIT_COMMITTER_NAME/EMAIL` set to the owner
of that item. Epics and stories are authored by the Product Owner
(Derrick) in the game repo and by the Scrummaster (Logan) in the
platform repo. PBIs are authored by their `owner:` handle; "Company of
Theseus" process tasks are authored by Logan. Ticket ids are allocated
sequentially per repo, so the B branches run one after another: B0,
then B1-B5 (Chase, Steven, Angie, Derrick, Logan), each merged before
the next starts. This way no two branches ever allocate the same id.

### Output

`docs/backlog.md` in each repo is one table: Jira key (linked), frob
id, tier, parent, points, sprint, owner. It is generated from
`frob ticket list` at the end of B, and it is the cross-reference the
course deliverables cite.

## 5. Acceptance

1. W: `frob graph build` reports 0 parse failures, `frob ticket list`
   works, `tools/plaincs/run_tests.sh` and `scripts/check_unity_tree.sh`
   pass. `frob check` passes as soon as frob can dispatch C#; until then
   the CHECK001 blocker is recorded in `TODO.md` with the frob ticket id.
2. C: `grep -rn "<summary>" Assets` is empty. Every public symbol in
   `Assets/Scripts` has a `frob:doc` whose anchor exists and names it in
   a `frob:describes`. The plaincs test count is unchanged.
3. B: every Jira issue in `jira-export.md` except the deleted SCRUM-4 has
   exactly one frob ticket carrying its `jira:` label, across the two
   repos. The per-sprint point totals per repo add up to the export's
   (155 / 129 / 82 / 20 stretch). Both `docs/backlog.md` tables exist.
