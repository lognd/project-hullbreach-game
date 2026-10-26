The server now replicates the match end to both clients (a reliable `MatchEnded` message), the feed args carry the `MatchResult`, and a draw is raised on the feed but never reported to the platform.
