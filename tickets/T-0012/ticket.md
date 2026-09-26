---
id: T-0012
title: 'S07: Sign in from inside the game'
state: queued
kind: feature
origin: human
created: '2026-09-26'
priority: critical
parent: null
tier: story
sprint: sprint-1
runs_last: false
milestone: 0.1.0
flavour: null
due: null
rank: null
points: null
unsized_ack: false
unsized_ack_reason: null
tokens_in: null
tokens_out: null
tokens_cache_read: null
usage: null
runs_last_parallel_safe: false
runs_last_parallel_safe_reason: null
worktree: null
branch: null
scope_breadth_ack: false
scope_breadth_ack_reason: null
no_scope_declared: false
no_scope_declared_reason: null
designated_repro_test: null
acceptance:
- text: A player can sign in from the game client with the same credentials as the
    website.
  evidence: []
- text: The game client presents a valid session to the game server when joining a
    match.
  evidence: []
- text: A player who is not signed in can still build ships offline but cannot join
    ranked matches.
  evidence: []
threat: null
component: null
labels:
- jira:SCRUM-28
- owner:stevendangkhoi
- game
- platform
anchor: false
anchor_reason: null
land_commit: null
---
https://aliens-against-humanity.atlassian.net/browse/SCRUM-28

**Card**
As a player, I want to sign into my platform account from the game client, so that my matches count toward my rating and my skins appear on my ship.

**Conversation**
- Does the game show a username/password form, or open the website in a browser and receive a token (device-code style)?
- How does the game client store the token between launches?
