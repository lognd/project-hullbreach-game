+++
id = "01M4GR19JQASCFF47CTPSDV17J"
title = "BlockGrid.KeyAt returns stale data for i >= KeyCount and public Mass field is externally mutable"
type = "bug"
category = "todo"
priority = "low"
reporter = "lognd"
created = "2026-10-09T16:30:41Z"
updated = "2026-10-09T16:30:41Z"
labels = ["origin:auditor", "audit:hullbreach-core"]
scope = ["Assets/Scripts/Hullbreach.Core/Grid/BlockGrid.cs"]
+++

(1) KeyAt (BlockGrid.cs:79-83) indexes _sortedKeys, whose length can exceed _sortedKeyCount after removals (buffer only grows, l.101-102); i in [KeyCount, Length) silently returns a stale removed key instead of failing. Fix: bounds-check against _sortedKeyCount (throw ArgumentOutOfRange as programmer bug, or return -1) and document. (2) public MassProperties Mass field (l.33) is writable/mutating-method-callable by any dependent (grid.Mass.Add(...) compiles, and as a field of a class the struct mutators act in place), breaking the 'maintained only by TryAdd/TryRemove/TrySet' O(1) invariant; no current caller mutates it (checked Ship, Structure, Builder, Net, Game, Hud). Fix: expose as a get-only property returning a copy (or readonly view), keep mutators internal.
