+++
id = "01M4GR3TX2DWRPZCNV69V7RAM9"
title = "HudPrefabBuilder.WireDemoScene reports success when wiring was skipped and can leave a dangling BuilderHud/null controller"
type = "bug"
category = "todo"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:32:04Z"
updated = "2026-10-09T16:32:04Z"
idempotency_key = "audit-game-editor-wire"
labels = ["origin:auditor", "audit:hullbreach-game"]
scope = ["Assets/Editor/Hullbreach.Editor/HudPrefabBuilder.cs"]
+++

HudPrefabBuilder.cs:387-460 (WireDemoScene) with Assets/Scripts/Hullbreach.Game/BuilderHud.cs:245 and Hud/*.cs. Every lookup is by magic name (GameObject.Find 'Demo', 'PlayerShip', 'HudCanvas') and silently no-ops when missing, but line 460 always logs 'wired HudCanvas + EventSystem' and saves the scene; -executeMethod then exits 0. If PlayerShip lacks BuilderController, BuilderHud.controller stays null and BuilderHud.LateUpdate (line 245: controller.Session) throws NullReferenceException every frame; likewise StatusPanelView/HullWarningBanner demoMode null. Also line 452-455 destroys the old BuilderHud even when newBuilderHud was null, leaving DemoMode.builderHud missing; OpenScene(Single) at 389 can prompt on a dirty scene. Fix: have Run/WireDemoScene return a Result with an error per missed binding and throw/ exit non-zero via EditorApplication.Exit(1) in the batch entry points; only destroy the old HUD after the new one is bound; add Awake validation (Debug.LogError + enabled=false) in BuilderHud/StatusPanelView/HullWarningBanner for unset serialized fields. Add an edit-mode test running WireDemoScene on a temp scene.
