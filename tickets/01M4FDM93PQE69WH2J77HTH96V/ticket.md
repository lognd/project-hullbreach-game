+++
id = "01M4FDM93PQE69WH2J77HTH96V"
title = "S55: Audit controls and sluggishness"
type = "story"
category = "todo"
priority = "medium"
parent = "01M3DG5Y05HP0YSRB6819FBK2S"
reporter = "lognd"
created = "2026-10-09T04:09:35Z"
updated = "2026-10-09T04:10:20Z"
persona = "player"
capability = "have controls that respond immediately and predictably"
outcome_text = "flying feels like piloting, not steering a barge"
labels = ["owner:GingerVHS", "game", "physics", "milestone:0.3.0"]
+++

Card: as a player, I want the ship to respond to input without perceptible lag or sluggishness, so that flying is skill-based.

Audit spike split by area; each audit produces a findings section in docs/ with measurements, then files follow-up tickets for every fix (fixes are not in these tickets).

Leads already visible: DemoInput reads Input.GetKeyDown/GetAxisRaw (legacy Input); edge-triggered reads consumed in FixedUpdate can drop or delay presses; mass/inertia scale with block count; no client-side prediction yet (S48-1), so networked play adds a full round trip to every input.

Conversation:
- Target numbers: e.g. input-to-visible-response under 50 ms locally, time to turn 90 degrees for the reference ship. The audits propose them; the team agrees them.
