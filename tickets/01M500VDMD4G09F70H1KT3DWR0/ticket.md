+++
id = "01M500VDMD4G09F70H1KT3DWR0"
title = "S07-4: Game server validates the player's platform session at connect"
type = "task"
category = "todo"
priority = "high"
points = 3
parent = "01M3DG5Y0CBB0T10VK6X8FPXBY"
reporter = "mcnairrobotics"
created = "2026-10-15T14:53:22Z"
updated = "2026-10-15T14:53:22Z"
labels = ["owner:mcnairrobotics", "game", "platform", "milestone:0.1.0", "creates:Assets/Scripts/Hullbreach.Platform/**", "creates:Assets/Tests/EditMode/Hullbreach.Platform.Tests/**", "creates:docs/platform-link.md"]
scope = ["Assets/Scripts/Hullbreach.Platform/**", "Assets/Tests/EditMode/Hullbreach.Platform.Tests/**", "Assets/Scripts/Hullbreach.NetCode.Entities/**", "tools/plaincs/Hullbreach.Plain/Hullbreach.Plain.csproj", "docs/platform-link.md", "docs/netcode.md", "docs/README.md", "ProjectSettings/ProjectSettings.asset"]

[[acceptance]]
text = "An engine-free platform client logs in and validates a session through an injectable HTTP transport, mapping success, 401, 429 and transport failure to distinct outcomes, covered by plaincs tests."
bound = false

[[acceptance]]
text = "After connecting, the client sends its platform token by RPC; the server validates it with GET /api/v1/auth/session and admits the player, or disconnects with a reason when -hullbreachRequirePlatformAuth is set."
bound = false

[[acceptance]]
text = "Tokens, passwords and the server key never appear in logs; the server key is read only from the HULLBREACH_PLATFORM_SERVER_KEY environment variable."
bound = false

[[acceptance]]
text = "A live harness gated on HULLBREACH_PLATFORM_IT_URL logs in against a running platform API and validates the token server-side."
bound = false
+++

Server half of S07 (acceptance 2: present a valid session to the game server). Engine-free Hullbreach.Platform assembly plus a Netcode for Entities handshake; see docs/platform-link.md. Persisting the token between launches stays with S07-3; the sign-in screen is S07-2.
