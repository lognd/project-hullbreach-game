+++
id = "01M3DG5Y31PY0644PP1AYDPSFS"
title = "S44-2: Builder rule preventing removal of a welded thruster"
type = "task"
category = "todo"
priority = "medium"
points = 1
parent = "01M3DG5Y114RNPPMJWHSPVHVQC"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-11-15T04:37:25Z"
aliases = ["T-0097"]
labels = ["jira:SCRUM-206", "owner:stevendangkhoi", "game", "physics", "milestone:0.3.0"]

[[acceptance]]
text = "Given a block with the welded flag, when the builder tries to remove it, then CanRemove and BuilderSession.Remove refuse and the grid is unchanged"
bound = false
+++

Builder rule preventing removal of a welded thruster
Parent story: S44 Fire the Inconvenient Thruster
