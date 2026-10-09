+++
id = "01M4GR20BWW7F0148E0F58A1YS"
title = "UndoStack.Apply swallows TryAdd/TryRemove failures; Undo of the first core placement reports true but changes nothing"
type = "bug"
category = "todo"
priority = "high"
reporter = "lognd"
created = "2026-10-09T16:31:04Z"
updated = "2026-10-09T16:53:21Z"
labels = ["origin:auditor", "auditor"]
scope = ["Assets/Scripts/Hullbreach.Builder/UndoStack.cs"]

[[acceptance]]
text = "Given the first core placement, when undone, then TryUndo returns false and nothing changes"
bound = false
+++

Symbol: UndoStack.TryUndo/TryRedo/Apply (UndoStack.cs:77-127), surfaced via BuilderSession.Undo/Redo (BuilderSession.cs:181-194). Contract gap: Apply ignores the bool from grid.TryAdd (line 117) and grid.TryRemove (line 124). BlockGrid.TryRemove refuses the core (BlockGrid.cs:144), so Undo of the core-seeding Place pops the action, returns true, fires Changed, yet the core stays; Redo then no-ops too. The same silent divergence happens when the shared grid (BuilderSession(BlockGrid) over a ShipBody grid, BuilderController.cs:42) was mutated externally (damage removal, TrySet). Doc says 'false when there is nothing to undo' only. Fix direction: make Apply return bool/count of applied entries; TryUndo/TryRedo return false (and keep the action on its stack) when an entry cannot be applied, or explicitly skip the core Place in the undo stack; document the failure result in docs/reference/hullbreach-builder.md#undostack and add an EditMode test for undoing the first core.
