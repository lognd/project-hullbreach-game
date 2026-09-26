# Hullbreach.Builder reference

Per-type reference for `Assets/Scripts/Hullbreach.Builder`, linked from the code by
`// frob:doc docs/reference/hullbreach-builder.md#<anchor>`. One heading per
public type; each heading carries the `frob:describes` lines for that
type and its public members. Architecture-level context lives in
[architecture.md](../architecture.md).

### PaletteEntry

One selectable palette entry (S33 criterion 1).

<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BlockPalette.cs::PaletteEntry -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BlockPalette.cs::PaletteEntry.TypeId -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BlockPalette.cs::PaletteEntry.Name -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BlockPalette.cs::PaletteEntry.Mass -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BlockPalette.cs::PaletteEntry.Cost -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BlockPalette.cs::PaletteEntry.Symmetric -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BlockPalette.cs::PaletteEntry.PaletteEntry -->

### BlockPalette

Enumerates every entry in the BlockTypes table with the builder-facing
cost and symmetry that Core has no reason to know about. Cost is kept
here, not in Core, because cost is build economy rather than physics:
Core only knows mass and structural properties. `IsSymmetric` is true
when a block type has no facing to orient: Thruster always pushes toward
ship-local +y and RetroThruster always pushes toward -y, so neither has a
facing to choose; only Cannon and Fin point somewhere and so require the
second click.

<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BlockPalette.cs::BlockPalette -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BlockPalette.cs::BlockPalette.IsSymmetric -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BlockPalette.cs::BlockPalette.All -->

### HoverState

Preview shown to the UI for whatever cell the pointer is over.

<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::HoverState -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::HoverState.Valid -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::HoverState.Key -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::HoverState.Verdict -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::HoverState.HoverState -->

### BuilderState

The two-click placement state machine (S30). Idle is "nothing pending";
Orienting is "a cell is chosen, waiting for a facing or a symmetric
commit". Symmetric block types (Core/Hull/Armor) have no facing to
choose, so S30's UI conversation ("skip the second click") applies and
they commit on the first click.

<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::BuilderState -->

### BuilderSession

Owns the grid plus the click-driven state machine that turns palette
selection and cell clicks into placements, orientations, removals and
undo/redo. Pure C#, with no UnityEngine dependency, so it is exercised
directly in edit-mode tests; BuilderController in Hullbreach.Game is the
thin MonoBehaviour that feeds it mouse input.

The `BuilderSession(BlockGrid)` constructor builds over an EXTERNAL grid
instead of a private one, so the demo scene's builder can edit the very
same BlockGrid a ShipBody is simulating; ownership stays with the caller,
this session only mutates it. The parameterless constructor owns a
brand-new grid instead, for standalone builder tests and any caller with
no existing ship.

`Hover` previews what would happen at a cell right now: while Idle,
whether it is a legal placement for the selected type; while Orienting,
the hovered cell snaps the pending facing towards it, and the returned
verdict re-validates the pending cell with that candidate facing, so an
orientation that would block its own exhaust/muzzle/fin clearance, or
lacks a fin's hull anchor, previews as invalid.

`Click` is the main two-click gesture: Idle + valid cell commits
immediately for symmetric types and enters Orienting for asymmetric
types; while Orienting it commits the pending placement with the facing
from the last Hover.

`Remove` applies the detach rule and records the whole batch (the
requested block plus anything it strands) as one undoable action.

<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::BuilderSession -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::BuilderSession.Grid -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::BuilderSession.BuilderSession -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::BuilderSession.SelectedTypeId -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::BuilderSession.State -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::BuilderSession.PendingKey -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::BuilderSession.PendingModifiers -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::BuilderSession.Changed -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::BuilderSession.TotalMass -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::BuilderSession.BlockCount -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::BuilderSession.Select -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::BuilderSession.Hover -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::BuilderSession.Click -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::BuilderSession.Cancel -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::BuilderSession.Remove -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::BuilderSession.Undo -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::BuilderSession.Redo -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::BuilderSession.UndoDepth -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/BuilderSession.cs::BuilderSession.DescribeVerdict -->

### Clearance

Per-type "keep this cell empty" / "must be attached here" geometry for
directional and exhaust-bearing blocks. Lives apart from PlacementRules
so both "is my own footprint clear" and "am I standing in someone else's
footprint" can share the exact same cell computation.

`TryReservedCells` lists every cell that must stay empty for a block
sitting at a given key: Thruster reserves its ship-local -y (exhaust)
neighbor, Cannon and Fin reserve the cell Facing.Ahead of them, and
RetroThruster reserves its +x and -x neighbors. Plain blocks
(Core/Hull/Armor) reserve nothing. A reserved direction that falls
outside BlockKey's range is simply omitted: there is no cell there to
ever be occupied, so it is vacuously satisfied. Always returns true; the
bool return exists so a caller can read this as "the reservation set was
computed" without special-casing plain types.

`RequiredAnchor` is true when a type requires an anchoring block on some
fixed side of it (only Fin, whose anchor is Facing.Behind, the hull it
mounts on); false for every other type. When the anchor direction itself
falls outside BlockKey's range, the anchor key is -1 even though the
return value is true, so the caller sees "there is nowhere for the
required hull to be" and refuses the placement.

<!-- frob:describes Assets/Scripts/Hullbreach.Builder/Clearance.cs::Clearance -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/Clearance.cs::Clearance.TryReservedCells -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/Clearance.cs::Clearance.RequiredAnchor -->

### PlacementVerdict

Why a placement was accepted or refused (S30, S31, S32, and the
exhaust/muzzle/fin clearance rules). Ok is the only accepting value;
every other member names the specific rule that refused the cell so the
HUD can explain it instead of just flashing red.

<!-- frob:describes Assets/Scripts/Hullbreach.Builder/PlacementRules.cs::PlacementVerdict -->

### PlacementRules

Placement and removal validity for the two-click builder (S30, S31, S32).

The `CanPlace(BlockGrid, int)` overload only checks range/empty/adjacency
and never accepts an empty grid (S32: exactly one core, and it must be
the first block placed), so callers placing the very first block must go
through the type-aware overload. `CanPlace(BlockGrid, int, byte)` encodes
S32's core rule: an empty grid may ONLY accept a core, and a core may
ONLY be placed into an empty grid (there is exactly one core, ever);
every other type falls back to the ordinary adjacency rule. The full
`CanPlace` overload additionally checks clearance: the new block's own
reserved cells (Clearance.TryReservedCells) must be empty, the new block
must not sit inside any EXISTING block's reserved cell, and a Fin's
anchor cell (Clearance.RequiredAnchor) must hold a non-Fin block; `why`
names exactly which rule decided the outcome. The reserved-cell check
against existing neighbors only needs to look at the 4-connected
neighbors of the candidate cell, since every reservation relation
(exhaust, muzzle, fin-ahead, retro side nozzles) is exactly one
orthogonal step.

`CanRemove` (DECISION, S31's open question): a removal that would strand
other blocks is ALLOWED, not refused: the stranded blocks detach along
with it (see Detach). This keeps single-click removal always available
instead of silently failing near a bottleneck.

`Detach` removes a key and, per the detach rule, anything that becomes
unreachable from the core as a result; every removed key (the requested
one plus any stranded ones) is appended to `removed`. Uses Articulation
as a fast path: if the key is not an articulation point, removing it
cannot disconnect anything, so the flood fill is skipped entirely.

<!-- frob:describes Assets/Scripts/Hullbreach.Builder/PlacementRules.cs::PlacementRules -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/PlacementRules.cs::PlacementRules.CanPlace -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/PlacementRules.cs::PlacementRules.CanRemove -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/PlacementRules.cs::PlacementRules.Detach -->

### UndoStack

Undo/redo for the builder. S31 requires at least ten deep; capped at
MaxDepth so a long session cannot grow the history unbounded, dropping
the oldest action once full.

Each entry is a whole ACTION, not a single block: a placement is one
key+block, but a detach removal can strand several blocks, and all of
those must undo (and redo) together as one step, matching the single
click that caused them.

`RecordPlace` must be called AFTER the block is already in the grid, and
clears the redo stack since a new action makes the previously-undone
future unreachable. `RecordRemove` pairs each removed key with the block
that occupied it BEFORE removal, so undo can restore type, modifiers AND
damage exactly; call it AFTER the blocks are already removed from the
grid. `TryUndo` reverses the last action: a Place is undone by removing
its block, a Remove (including a detach batch) is undone by restoring
every entry exactly. `TryRedo` re-applies the most recently undone
action.

<!-- frob:describes Assets/Scripts/Hullbreach.Builder/UndoStack.cs::UndoStack -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/UndoStack.cs::UndoStack.MinimumDepth -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/UndoStack.cs::UndoStack.MaxDepth -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/UndoStack.cs::UndoStack.Depth -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/UndoStack.cs::UndoStack.RecordPlace -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/UndoStack.cs::UndoStack.RecordRemove -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/UndoStack.cs::UndoStack.TryUndo -->
<!-- frob:describes Assets/Scripts/Hullbreach.Builder/UndoStack.cs::UndoStack.TryRedo -->
