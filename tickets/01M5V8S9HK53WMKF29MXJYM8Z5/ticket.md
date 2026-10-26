+++
id = "01M5V8S9HK53WMKF29MXJYM8Z5"
title = "S46-1 follow-up: raise MatchEndFeed.Shared when the match ends"
type = "task"
category = "done"
outcome = "fixed"
priority = "medium"
reporter = "lognd"
created = "2026-10-26T04:51:31Z"
updated = "2026-10-26T05:12:14Z"
+++

The platform reporter is attached to MatchEndFeed.Shared (S16-4 seam). The S46-1 match state machine must build MatchEndedArgs (fresh uuid match id, winner/peers/stats/duration) and call Raise once at match end. See docs/platform-link.md#reporting-a-finished-match. Found while working S16-4.
