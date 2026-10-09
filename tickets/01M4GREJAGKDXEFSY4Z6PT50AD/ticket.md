+++
id = "01M4GREJAGKDXEFSY4Z6PT50AD"
title = "S56-4: Continuous green-to-red stress and buckling feedback in normal play"
type = "task"
category = "todo"
priority = "medium"
points = 3
parent = "01M4GREG1Z463PSEZHTRFXDEYB"
reporter = "lognd"
created = "2026-10-09T16:37:56Z"
updated = "2026-10-09T16:37:56Z"
labels = ["owner:a-carten", "game", "physics", "milestone:0.3.0"]

[[acceptance]]
text = "Every block continuously shows its max(stress, buckling) ratio on the S37-3 green-to-red scale (or the S37-2 colorblind palette) as an edge/outline layer, so the skin stays visible."
bound = false

[[acceptance]]
text = "Ratio 0 renders no visible stress tint; ratio >= 1 matches the critical flash threshold."
bound = false

[[acceptance]]
text = "Client feedback uses the same calibrated solver scaling as the server (see audit ticket ~72GAXW7)."
bound = false
+++
