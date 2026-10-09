+++
id = "01M3DG5Y1JR30SDC29Y142APTZ"
title = "S27-3: Persist settings to a local file across launches"
type = "task"
category = "todo"
priority = "medium"
points = 1
parent = "01M3DG5Y0GHE7Z7ZF6PK8WR4RY"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-10-09T04:09:36Z"
aliases = ["T-0050"]
labels = ["jira:SCRUM-135", "owner:stevendangkhoi", "game", "milestone:0.1.0", "creates:docs/reference/hullbreach-settings.md"]
scope = ["Assets/Scripts/Hullbreach.Settings/**", "Assets/Scripts/Hullbreach.Settings.meta", "Assets/Tests/EditMode/Hullbreach.Settings.Tests/**", "Assets/Tests/EditMode/Hullbreach.Settings.Tests.meta", "tools/plaincs/Hullbreach.Plain/Hullbreach.Plain.csproj", "docs/testing.md", "docs/reference/hullbreach-settings.md"]

[[acceptance]]
text = "Given settings saved through SettingsStore, when the store is loaded again (a new launch), then master/music/effects volume, resolution, fullscreen and key-binding placeholders are identical"
bound = false

[[acceptance]]
text = "Given a missing settings file, when loaded, then defaults are returned with status Missing and no error"
bound = false

[[acceptance]]
text = "Given a corrupt or unreadable settings file, when loaded, then defaults are returned with status Corrupt and warnings, nothing throws, and the next save replaces the file"
bound = false

[[acceptance]]
text = "Given a failing disk, when saving, then Save reports the error in its result instead of throwing, and file I/O is behind ISettingsStorage so it is testable"
bound = false
+++

Persist settings to a local file across launches
Parent story: S27 Adjust settings
