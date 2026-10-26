+++
id = "01M5VF1PBQZ9D0Y25J0PQB6Q8N"
title = "Wire match-end feed to the results screen"
type = "task"
category = "todo"
priority = "medium"
parent = "01M3DG5Y134BJMWZH7S1QV7EY9"
reporter = "a-carten"
created = "2026-10-26T06:40:58Z"
updated = "2026-10-26T06:40:58Z"
scope = ["Assets/Scripts/Hullbreach.Match/**", "Assets/Scripts/Hullbreach.NetCode.Entities/**", "Assets/Tests/EditMode/Hullbreach.Match.Tests/**", "docs/**", "changelog.d/**"]
+++

ResultsScreenModel and HullbreachResultsView exist but nothing calls Show; route MatchEndFeed to them.
