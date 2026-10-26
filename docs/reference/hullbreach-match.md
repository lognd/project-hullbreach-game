# Hullbreach.Match reference

Per-type reference for `Assets/Scripts/Hullbreach.Match`, linked from the code by
`// frob:doc docs/reference/hullbreach-match.md#<anchor>`. Engine-free (it
references `Hullbreach.Core` and `Hullbreach.Platform` only), so the plain-C#
harness compiles and tests it.

### MatchStateMachine

<!-- describes: Assets/Scripts/Hullbreach.Match/MatchStateMachine.cs::MatchStateMachine -->
<!-- describes: Assets/Scripts/Hullbreach.Match/MatchStateMachine.cs::MatchStateMachine.Phase -->
<!-- describes: Assets/Scripts/Hullbreach.Match/MatchStateMachine.cs::MatchStateMachine.Result -->
<!-- describes: Assets/Scripts/Hullbreach.Match/MatchStateMachine.cs::MatchStateMachine.WinnerPeer -->
<!-- describes: Assets/Scripts/Hullbreach.Match/MatchStateMachine.cs::MatchStateMachine.MatchId -->
<!-- describes: Assets/Scripts/Hullbreach.Match/MatchStateMachine.cs::MatchStateMachine.MatchStateMachine -->
<!-- describes: Assets/Scripts/Hullbreach.Match/MatchStateMachine.cs::MatchStateMachine.Start -->
<!-- describes: Assets/Scripts/Hullbreach.Match/MatchStateMachine.cs::MatchStateMachine.Tick -->
<!-- describes: Assets/Scripts/Hullbreach.Match/MatchStateMachine.cs::MatchStateMachine.Disconnect -->

The win condition (roadmap S46-1). Phases: `Lobby` -> `Running` (`Start`, which
mints a fresh uuid `MatchId` and notes the clock) -> `Ended` (terminal). The
server calls `Tick` each step; a ship's core counts as breached when its
`BlockGrid.CoreKey` is gone, the core cell is gone, or the core block's damage
is 255. Endings:

- One core breached: `CoreBreached`, the other peer wins, and the machine calls
  `MatchEndFeed.Raise` exactly once with a `MatchEndedArgs` (match id, winner,
  both peers' `PlayerMatchStats`, duration in whole seconds). The platform
  reporter attached to `MatchEndFeed.Shared` turns that into `POST /api/v1/matches`.
- Both cores breached in the same tick: `Draw`, no winner, nothing raised. The
  platform's `MatchReport` requires `winner_id` to be one of the two players, so
  a draw cannot be reported; the machine logs a warning saying so.
- The optional `timeLimitSeconds` constructor argument (S46-2; 0 or less means
  no limit) is checked in `Tick` after the core checks: once the clock has run
  that long since `Start` with both cores alive, the tiebreak is core health
  (255 minus the core block's damage). Higher health wins as `TimeLimit` and is
  reported like a core win; equal health is a `Draw` (unreported).
- A participant `Disconnect`s while running: `Forfeit`, the remaining peer wins,
  and the feed is raised like a core win (same warning log names the leaver).

`ServerSimulation` constructs, starts and ticks the machine itself (see
`hullbreach-net.md#serversimulation`).

After `Ended`, `Tick` and `Disconnect` do nothing, so the feed cannot fire twice.
The clock, feed, match-id factory and log are injected; the feed defaults to
`MatchEndFeed.Shared`.

### MatchPhase

<!-- describes: Assets/Scripts/Hullbreach.Match/MatchStateMachine.cs::MatchPhase -->

`Lobby`, `Running`, `Ended`.

### MatchResult

<!-- describes: Assets/Scripts/Hullbreach.Match/MatchStateMachine.cs::MatchResult -->

`None` (not ended), `CoreBreached`, `Forfeit` and `TimeLimit` (all have a winner and are
reported), `Draw` (unreported).

### MatchParticipant

<!-- describes: Assets/Scripts/Hullbreach.Match/MatchStateMachine.cs::MatchParticipant -->
<!-- describes: Assets/Scripts/Hullbreach.Match/MatchStateMachine.cs::MatchParticipant.Peer -->
<!-- describes: Assets/Scripts/Hullbreach.Match/MatchStateMachine.cs::MatchParticipant.Grid -->
<!-- describes: Assets/Scripts/Hullbreach.Match/MatchStateMachine.cs::MatchParticipant.Stats -->
<!-- describes: Assets/Scripts/Hullbreach.Match/MatchStateMachine.cs::MatchParticipant.MatchParticipant -->

One side: network peer id, its ship's `BlockGrid`, and a `Func<PlayerMatchStats>`
read once when the match ends.

### ResultsScreenModel

<!-- describes: Assets/Scripts/Hullbreach.Match/ResultsScreenModel.cs::ResultsPlayerRow -->
<!-- describes: Assets/Scripts/Hullbreach.Match/ResultsScreenModel.cs::ResultsPlayerRow.ResultsPlayerRow -->
<!-- describes: Assets/Scripts/Hullbreach.Match/ResultsScreenModel.cs::ResultsScreenModel -->
<!-- describes: Assets/Scripts/Hullbreach.Match/ResultsScreenModel.cs::ResultsScreenModel.Headline -->
<!-- describes: Assets/Scripts/Hullbreach.Match/ResultsScreenModel.cs::ResultsScreenModel.Reason -->
<!-- describes: Assets/Scripts/Hullbreach.Match/ResultsScreenModel.cs::ResultsScreenModel.Duration -->
<!-- describes: Assets/Scripts/Hullbreach.Match/ResultsScreenModel.cs::ResultsScreenModel.HasWinner -->
<!-- describes: Assets/Scripts/Hullbreach.Match/ResultsScreenModel.cs::ResultsScreenModel.PlayerA -->
<!-- describes: Assets/Scripts/Hullbreach.Match/ResultsScreenModel.cs::ResultsScreenModel.PlayerB -->
<!-- describes: Assets/Scripts/Hullbreach.Match/ResultsScreenModel.cs::ResultsScreenModel.CanChoose -->
<!-- describes: Assets/Scripts/Hullbreach.Match/ResultsScreenModel.cs::ResultsScreenModel.RematchRequested -->
<!-- describes: Assets/Scripts/Hullbreach.Match/ResultsScreenModel.cs::ResultsScreenModel.ReturnToLobbyRequested -->
<!-- describes: Assets/Scripts/Hullbreach.Match/ResultsScreenModel.cs::ResultsScreenModel.ResultsScreenModel -->
<!-- describes: Assets/Scripts/Hullbreach.Match/ResultsScreenModel.cs::ResultsScreenModel.FromEnded -->
<!-- describes: Assets/Scripts/Hullbreach.Match/ResultsScreenModel.cs::ResultsScreenModel.Rematch -->
<!-- describes: Assets/Scripts/Hullbreach.Match/ResultsScreenModel.cs::ResultsScreenModel.ReturnToLobby -->
<!-- describes: Assets/Scripts/Hullbreach.Match/ResultsScreenModel.cs::ResultsScreenModel.FormatClock -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachResultsView.cs::HullbreachResultsView -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachResultsView.cs::HullbreachResultsView.Show -->
<!-- describes: Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachResultsView.cs::HullbreachResultsView.Hide -->

S46-3. The post-match results screen. `ResultsScreenModel` (engine-free,
tested in `ResultsScreenModelTests`) is built from the ended match
(`FromEnded(result, MatchEndedArgs, localPeer, nameOf)`; a draw has no feed
event, so use the constructor with `winnerPeer = -1`) and exposes:

- `Headline`: `VICTORY` / `DEFEAT` for the local peer, `<name> WINS` for a
  spectator or server view, `DRAW` when nobody won.
- `Reason`: `Core breached`, `Opponent left the match (forfeit)`,
  `Both cores breached`, `Time limit reached`. A `MatchResult` without
  wording is shown as its spaced-out enum name and logged, never
  thrown on, so adding a reason needs no change here beyond nicer text.
- `Duration` (`m:ss`, `h:mm:ss` from an hour) and one `ResultsPlayerRow`
  per side: name (display name from `nameOf`, else `Player 1` / `Player 2`),
  winner flag, damage dealt, blocks destroyed, blocks placed, time alive.
- `Rematch()` / `ReturnToLobby()`: raise `RematchRequested` /
  `ReturnToLobbyRequested` once; the first pick wins and later ones are
  ignored (`CanChoose` goes false). The owner decides what each means.

`HullbreachResultsView` (`Hullbreach.NetCode.Entities`, inspector-wired
like `HullbreachSignInView`) mirrors the model onto TMP texts and two
buttons: `Show(model)` / `Hide()`. No prefab or scene wiring exists yet,
and nothing calls `Show` until the server/client flow is connected.
