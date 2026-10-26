+++
id = "01M3DG5Y1KBB5BBJM3MKKFPSGV"
title = "S28-1: UDP transport and serializer for the NetMessages wire format"
type = "task"
category = "todo"
priority = "critical"
points = 5
parent = "01M3DG5Y0HT0GE1F2YVQY24SSN"
reporter = "human"
created = "2026-09-26T00:00:00Z"
updated = "2026-10-26T08:30:55Z"
aliases = ["T-0051"]
labels = ["jira:SCRUM-123", "owner:mcnairrobotics", "game", "milestone:0.1.0"]

[[acceptance]]
text = "The wire serializer round-trips every NetMessages kind, including randomized inputs"
bound = true

[[acceptance]]
text = "Decoding untrusted bytes is bounded: truncated, oversized and malformed input fails the reader without throwing"
bound = false

[[acceptance]]
text = "A server and client exchange messages end to end over an ITransport implementation under loss and reordering"
bound = false
+++

UDP transport and serializer for the NetMessages wire format
Parent story: S28 Host or join a LAN match
