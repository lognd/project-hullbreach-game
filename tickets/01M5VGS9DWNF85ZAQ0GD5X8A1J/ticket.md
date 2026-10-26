+++
id = "01M5VGS9DWNF85ZAQ0GD5X8A1J"
title = "Replicate match end to clients with the real MatchResult"
type = "task"
category = "todo"
priority = "medium"
parent = "01M3DG5Y134BJMWZH7S1QV7EY9"
reporter = "mcnairrobotics"
created = "2026-10-26T07:11:19Z"
updated = "2026-10-26T07:11:19Z"
scope = ["Assets/Scripts/**", "Assets/Tests/**", "docs/**", "changelog.d/**"]
+++

MatchEndFeed is server-only, draws are not raised, and ResultsFlow cannot know the real result. Carry MatchResult in the args, raise for draws (not reported), add a MatchEnded reliable message and a client hook.
