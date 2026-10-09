+++
id = "01M4GR3TJHGJN2NBSRN4Y6WCM4"
title = "HudPrefabBuilder.Run builds StatusPanel without ensuring the ChannelBar prefab exists; Build() throws on a clean project"
type = "bug"
category = "todo"
priority = "medium"
reporter = "lognd"
created = "2026-10-09T16:32:04Z"
updated = "2026-10-09T16:56:31Z"
idempotency_key = "audit-game-editor-channelbar"
labels = ["origin:auditor", "audit:hullbreach-game"]
scope = ["Assets/Editor/Hullbreach.Editor/HudPrefabBuilder.cs"]

[[acceptance]]
text = "Given a project without ChannelBar.prefab, when HudPrefabBuilder.Run executes, then ChannelBar is built first and no canvas leaks"
bound = false
+++

HudPrefabBuilder.cs:59-63 and 203-209. Run -> BuildHudCanvasPrefab -> AddStatusPanel does AssetDatabase.LoadAssetAtPath(ChannelBarPrefabPath) then PrefabUtility.InstantiatePrefab(channelBarPrefab,...) three times, but only BuildChannelBarPrefab (separate -executeMethod entry) creates that asset; Run never calls it. On a project without ChannelBar.prefab the null prefab throws ArgumentNullException mid-build, leaking the temporary HudCanvas GameObject (DestroyImmediate at line 94 never runs) and writing a partial BuilderPanel.prefab so the next Build() skips via the File.Exists guard (line 69) with a broken state. Fix: call BuildChannelBarPrefab(force) first in BuildHudCanvasPrefab, check for null with a returned error, wrap in try/finally to destroy canvasGo, and make the skip guard check all five prefab paths.
