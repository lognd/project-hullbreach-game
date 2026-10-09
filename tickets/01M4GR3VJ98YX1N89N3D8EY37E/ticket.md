+++
id = "01M4GR3VJ98YX1N89N3D8EY37E"
title = "BuilderController.Update places/removes blocks on mouse clicks that land on HUD panels"
type = "bug"
category = "todo"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:32:05Z"
updated = "2026-10-09T16:32:05Z"
idempotency_key = "audit-game-builder-ui-click"
labels = ["origin:auditor", "audit:hullbreach-game"]
scope = ["Assets/Scripts/Hullbreach.Game/BuilderController.cs"]
+++

BuilderController.cs:112-127. Left/right clicks are handled via raw Input.GetMouseButtonDown with no UI occlusion check. The HUD canvas (HudPrefabBuilder.cs:111-112,164-165,264-265) has a GraphicRaycaster and panel Images with raycastTarget on, and an EventSystem is added, so clicking a HUD panel that overlaps the ship's grid area also edits the grid beneath. Fix: skip Click/Remove (and hover) when EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(); add a play-mode test clicking over the panel rect.
