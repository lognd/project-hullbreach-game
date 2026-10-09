+++
id = "01M4GR20PPZWB70FV01VXZQPGZ"
title = "BuilderSession.Select accepts any byte; invalid typeId reaches BlockTypes.Get and throws IndexOutOfRange on placement"
type = "bug"
category = "todo"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:31:05Z"
updated = "2026-10-09T16:53:23Z"
labels = ["origin:auditor", "auditor"]
scope = ["Assets/Scripts/Hullbreach.Builder/BuilderSession.cs"]

[[acceptance]]
text = "Given an unknown type id, when Select is called, then it returns false and keeps the selection"
bound = false
+++

Symbol: BuilderSession.Select (BuilderSession.cs:90-94), also BlockPalette.IsSymmetric (BlockPalette.cs:254). Select(7..255) is stored unvalidated; Hover/Click then pass it through PlacementRules.CanPlace (no type check, returns Ok), Click -> Place -> Grid.TryAdd -> BlockTypes.Get(typeId) => Table[typeId] IndexOutOfRangeException (BlockType.cs:~102, BlockGrid.cs:~128). IsSymmetric also returns true for unknown ids. BuilderController.Update bounds the loop by BlockTypes.Count, but Select is public API for any caller (tests, future save/load UI S35-3). Fix direction: Select returns bool (false and no state change when typeId >= BlockTypes.Count), and/or CanPlace returns a new PlacementVerdict (e.g. UnknownType) for out-of-table ids; document it and add a test.
