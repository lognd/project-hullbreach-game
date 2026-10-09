+++
id = "01M4GR1TZ1CAES7GEKXB62WZJV"
title = "BuilderHudModel.Build: hard-coded 'keys 1-7' title and unguarded null session"
type = "bug"
category = "todo"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:30:59Z"
updated = "2026-10-09T16:56:46Z"
labels = ["origin:auditor"]
scope = ["Assets/Scripts/Hullbreach.Hud/BuilderHudModel.cs"]

[[acceptance]]
text = "Given any palette size, when BuilderHudModel builds, then the title key range is min(9, BlockTypes.Count) and a null session throws ArgumentNullException"
bound = false
+++

BuilderHudModel.cs:53 hard-codes 'Palette (keys 1-7)' while the real binding is min(9, BlockTypes.Count) (BuilderController.cs:96) and rows come from BlockPalette.All(); adding or removing a block type makes the title lie. Fix: build the title from the palette count (min 9). Also Build(session, ...) at :38 dereferences session with no documented contract (NRE if null); BuilderHud.cs:36 guards it at the caller only. Fix: document 'session must be non-null' in the docstring or return an empty model, and add a test.
