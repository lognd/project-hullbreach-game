+++
id = "01M5XACF0K75AVMM0MCNDDWXNE"
title = "S29-3c: wire the queue match id: client presents it on join, server calls ServerSimulation.AssignQueueMatch, skin source"
type = "task"
category = "todo"
priority = "high"
parent = "01M3DG5Y0J805WP05DZV8GWAWA"
reporter = "mcnairrobotics"
created = "2026-10-26T23:57:57Z"
updated = "2026-11-15T12:44:31Z"
labels = ["game", "owner:mcnairrobotics"]

[[acceptance]]
text = "A client presents the allocated match id in a JoinTicket when joining (wire message round-trips, bad ids rejected)"
bound = true

[[acceptance]]
text = "A dedicated server takes its expected match id from -hullbreachMatchId and refuses a peer with the wrong or missing ticket"
bound = true
+++

found while working ~RHV8HRV. Remaining: (1) a net message field so a client presents ConnectRequest.MatchId when joining (changes NetMessages wire format, needs its own tests); (2) the allocation channel that tells a dedicated server its expected match id, then AssignQueueMatch; (3) equipped skins have no data source in the repo yet, ConnectRequest takes ids from the caller.
